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

using System.Composition;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RestSharp.MigrationAssistant.Analyzers;

namespace RestSharp.MigrationAssistant.CodeFixes;

/// <summary>
/// Rewrites the removed RestSharp interfaces to their concrete replacement types (RSM001 → RestResponse,
/// RSM002 → RestRequest), preserving any generic type arguments and surrounding trivia. RSM003 (IHttp) has no
/// direct replacement and is intentionally not fixable.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemovedInterfaceCodeFix)), Shared]
public class RemovedInterfaceCodeFix : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RestResponseInterface.Id,
        MigrationDiagnostics.RestRequestInterface.Id
    );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics) {
            if (!diagnostic.Properties.TryGetValue(RemovedInterfaceAnalyzer.ReplacementProperty, out var replacement) || replacement == null) continue;
            if (root.FindNode(diagnostic.Location.SourceSpan) is not SimpleNameSyntax name) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use '{replacement}'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(name, Rename(name, replacement)))),
                    equivalenceKey: diagnostic.Id
                ),
                diagnostic
            );
        }
    }

    static SyntaxNode Rename(SimpleNameSyntax name, string replacement)
        => name switch {
            GenericNameSyntax generic => generic.WithIdentifier(SyntaxFactory.Identifier(replacement)).WithTriviaFrom(name),
            _                         => SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(name)
        };
}
