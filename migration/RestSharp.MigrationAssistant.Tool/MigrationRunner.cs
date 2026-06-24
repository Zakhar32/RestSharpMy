//  Copyright (c) .NET Foundation and Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace RestSharp.MigrationAssistant.Tool;

/// <summary>
/// Loads a solution or project with MSBuildWorkspace, runs every RSM analyzer over each targeted project, applies the
/// available code fixes in a single batch per document, and records the outcome in a <see cref="MigrationReport"/>.
/// </summary>
public static class MigrationRunner {
    public static async Task<int> RunAsync(CliOptions options) {
        var analyzers = AnalyzerCatalog.Analyzers();
        var fixers    = AnalyzerCatalog.CodeFixesByDiagnosticId();
        var report    = new MigrationReport();

        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) => {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) Console.Error.WriteLine($"  workspace: {e.Diagnostic.Message}");
        };

        Solution                 solution;
        IReadOnlyList<ProjectId> targets;
        try {
            (solution, targets) = await LoadAsync(workspace, options.Path);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to load '{options.Path}': {ex.Message}");
            return 1;
        }

        foreach (var projectId in targets) {
            var project = solution.GetProject(projectId);
            if (project is null || project.Language != LanguageNames.CSharp) continue;

            Console.WriteLine($"Analyzing {project.Name}...");
            solution = await MigrateProjectAsync(solution, projectId, analyzers, fixers, report);
        }

        if (!options.DryRun && (report.Applied.Count > 0) && !workspace.TryApplyChanges(solution))
            Console.Error.WriteLine("Warning: some changes could not be written to disk.");

        var markdown = report.Render(options.Path, options.DryRun, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        await File.WriteAllTextAsync(options.ReportPath, markdown);

        Console.WriteLine();
        Console.WriteLine(report.ConsoleSummary());
        Console.WriteLine($"Report written to {Path.GetFullPath(options.ReportPath)}");
        if (options.DryRun) Console.WriteLine("(dry run — no files were modified)");

        return 0;
    }

    static async Task<Solution> MigrateProjectAsync(
        Solution                                  solution,
        ProjectId                                 projectId,
        ImmutableArray<DiagnosticAnalyzer>        analyzers,
        IReadOnlyDictionary<string, CodeFixProvider> fixers,
        MigrationReport                           report
    ) {
        var project     = solution.GetProject(projectId)!;
        var compilation = await project.GetCompilationAsync();
        if (compilation is null) return solution;

        var diagnostics = (await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync())
            .Where(d => d.Id.StartsWith("RSM", StringComparison.Ordinal) && d.Location.IsInSource)
            .ToImmutableArray();

        foreach (var byTree in diagnostics.GroupBy(d => d.Location.SourceTree)) {
            var documentId = solution.GetDocumentId(byTree.Key);
            if (documentId is null) continue;

            var document     = solution.GetDocument(documentId)!;
            var originalText = await document.GetTextAsync();
            var changes      = new List<TextChange>();

            foreach (var diagnostic in byTree.OrderBy(d => d.Location.SourceSpan.Start)) {
                var lineSpan = diagnostic.Location.GetLineSpan();
                var file     = lineSpan.Path;
                var line     = lineSpan.StartLinePosition.Line + 1;
                var info     = RuleCatalog.For(diagnostic.Id);

                var fixChanges = fixers.TryGetValue(diagnostic.Id, out var fixer)
                    ? await TryComputeFixAsync(document, diagnostic, fixer)
                    : null;

                if (fixChanges is { Length: > 0 } && !Overlaps(changes, fixChanges)) {
                    changes.AddRange(fixChanges);
                    report.Applied.Add(new AppliedFix(diagnostic.Id, file, line, diagnostic.GetMessage(), info.Confidence));
                }
                else {
                    report.Manual.Add(new ManualAction(diagnostic.Id, file, line, diagnostic.GetMessage(), info.ManualGuidance));
                }
            }

            if (changes.Count > 0) {
                var newText = originalText.WithChanges(changes.OrderBy(c => c.Span.Start));
                solution = solution.WithDocumentText(documentId, newText);
            }
        }

        return solution;
    }

    /// <summary>Computes the text changes a fix would make, without committing them, so independent fixes in one document
    /// can be merged into a single edit. Returns null when the provider declines (e.g. an unsafe shape).</summary>
    static async Task<TextChange[]?> TryComputeFixAsync(Document document, Diagnostic diagnostic, CodeFixProvider fixer) {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await fixer.RegisterCodeFixesAsync(context);
        if (actions.Count == 0) return null;

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        if (operations.OfType<ApplyChangesOperation>().FirstOrDefault() is not { } apply) return null;

        var changedDocument = apply.ChangedSolution.GetDocument(document.Id);
        if (changedDocument is null) return null;

        return [..await changedDocument.GetTextChangesAsync(document)];
    }

    static bool Overlaps(List<TextChange> existing, TextChange[] candidate)
        => candidate.Any(c => existing.Any(e => c.Span.OverlapsWith(e.Span)));

    static async Task<(Solution Solution, IReadOnlyList<ProjectId> Targets)> LoadAsync(MSBuildWorkspace workspace, string path) {
        var full = Path.GetFullPath(path);

        if (Directory.Exists(full)) {
            var solutionFile = Directory.GetFiles(full, "*.sln").Concat(Directory.GetFiles(full, "*.slnx")).FirstOrDefault();
            if (solutionFile is not null) return await OpenSolutionAsync(workspace, solutionFile);

            var projectFile = Directory.GetFiles(full, "*.csproj").FirstOrDefault();
            if (projectFile is not null) return await OpenProjectAsync(workspace, projectFile);

            throw new FileNotFoundException($"No .sln, .slnx or .csproj found in '{full}'.");
        }

        return Path.GetExtension(full).ToLowerInvariant() switch {
            ".sln" or ".slnx" => await OpenSolutionAsync(workspace, full),
            ".csproj"         => await OpenProjectAsync(workspace, full),
            _                 => throw new ArgumentException($"Unsupported path '{path}'. Provide a .sln, .slnx, .csproj or a directory.")
        };
    }

    static async Task<(Solution, IReadOnlyList<ProjectId>)> OpenSolutionAsync(MSBuildWorkspace workspace, string file) {
        var solution = await workspace.OpenSolutionAsync(file);
        return (solution, solution.ProjectIds);
    }

    static async Task<(Solution, IReadOnlyList<ProjectId>)> OpenProjectAsync(MSBuildWorkspace workspace, string file) {
        // Only migrate the project pointed at, not the projects it references.
        var project = await workspace.OpenProjectAsync(file);
        return (project.Solution, [project.Id]);
    }
}
