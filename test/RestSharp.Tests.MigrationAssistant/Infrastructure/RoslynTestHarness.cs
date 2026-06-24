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
using System.IO;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RestSharp.Tests.MigrationAssistant.Infrastructure;

/// <summary>
/// A small self-contained Roslyn harness for exercising the migration analyzers and code fixes against source strings.
/// It deliberately avoids the Microsoft.CodeAnalysis.Testing packages to keep a single Roslyn version across the repo.
/// </summary>
public static class RoslynTestHarness {
    static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    static ImmutableArray<MetadataReference> BuildReferences() {
        var tpa     = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var byName  = new Dictionary<string, string>();
        foreach (var path in tpa.Split(Path.PathSeparator).Where(p => p.Length > 0)) {
            byName[Path.GetFileNameWithoutExtension(path)] = path;
        }

        // Make sure the project's modern RestSharp is the one referenced.
        byName["RestSharp"] = typeof(global::RestSharp.RestClient).Assembly.Location;

        return byName.Values.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray();
    }

    static CSharpCompilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            "MigrationTests",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    /// <summary>Runs the given analyzers over <paramref name="source"/> and returns the RSM diagnostics, ordered by position.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, params DiagnosticAnalyzer[] analyzers) {
        var withAnalyzers = CreateCompilation(source).WithAnalyzers(ImmutableArray.Create(analyzers));
        var diagnostics   = await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        return diagnostics
            .Where(d => d.Id.StartsWith("RSM", StringComparison.Ordinal))
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    /// <summary>Applies <paramref name="fix"/> one diagnostic at a time, re-analysing after each edit, until none remain fixable.</summary>
    public static async Task<string> ApplyFixAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider fix) {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var project   = workspace.AddProject(
            ProjectInfo.Create(
                projectId, VersionStamp.Default, "MigrationTests", "MigrationTests", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: References
            )
        );
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));

        for (var iteration = 0; iteration < 50; iteration++) {
            var compilation   = await document.Project.GetCompilationAsync(CancellationToken.None);
            var withAnalyzers = compilation!.WithAnalyzers(ImmutableArray.Create(analyzer));
            var diagnostics   = (await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None))
                .Where(d => fix.FixableDiagnosticIds.Contains(d.Id))
                .OrderBy(d => d.Location.SourceSpan.Start)
                .ToImmutableArray();
            if (diagnostics.Length == 0) break;

            var changed = false;
            foreach (var diagnostic in diagnostics) {
                var actions = new List<CodeAction>();
                var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
                await fix.RegisterCodeFixesAsync(context);
                if (actions.Count == 0) continue;

                var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
                if (operations.OfType<ApplyChangesOperation>().FirstOrDefault() is not { } apply) continue;

                document = apply.ChangedSolution.GetDocument(document.Id)!;
                changed  = true;
                break;
            }

            if (!changed) break;   // every remaining diagnostic is informational-only (no fix offered)
        }

        return (await document.GetTextAsync(CancellationToken.None)).ToString();
    }
}
