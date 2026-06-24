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

namespace RestSharp.MigrationAssistant.CodeFixes;

/// <summary>
/// Removes redundant <c>Content-Type</c> (RSM006) and <c>Accept</c> (RSM007) header calls. A standalone statement is
/// deleted entirely; a link inside a fluent chain is spliced out, leaving the rest of the chain intact (the header
/// methods return the same request/client instance, so removing the link preserves the result).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantHeaderCodeFix)), Shared]
public class RedundantHeaderCodeFix : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RedundantContentTypeHeader.Id,
        MigrationDiagnostics.RedundantAcceptHeader.Id
    );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics) {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove redundant header",
                    _ => Task.FromResult(Remove(context.Document, root, invocation, member)),
                    equivalenceKey: diagnostic.Id
                ),
                diagnostic
            );
        }
    }

    static Document Remove(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member) {
        var newRoot = invocation.Parent is ExpressionStatementSyntax statement
            ? root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia)!
            : root.ReplaceNode(invocation, member.Expression.WithTriviaFrom(invocation));

        return document.WithSyntaxRoot(newRoot);
    }
}
