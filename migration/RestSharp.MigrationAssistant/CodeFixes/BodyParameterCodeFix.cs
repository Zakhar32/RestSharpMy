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
/// Rewrites legacy body calls to the modern equivalents:
/// <list type="bullet">
/// <item>RSM004 — <c>AddParameter(name, value, ParameterType.RequestBody)</c> → <c>AddBody(value, name)</c>, offered only
/// when <c>name</c> is a string-literal content type (contains '/'), for which the modern <c>AddBody</c> overload is
/// behaviourally identical to the legacy call.</item>
/// <item>RSM005 — <c>AddJsonBody(str)</c> → <c>AddStringBody(str, DataFormat.Json)</c>, the intent-revealing form of what
/// modern <c>AddJsonBody(string)</c> already does internally.</item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BodyParameterCodeFix)), Shared]
public class BodyParameterCodeFix : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RequestBodyParameter.Id,
        MigrationDiagnostics.JsonBodyWithString.Id
    );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics) {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;

            var rewrite = diagnostic.Id == MigrationDiagnostics.RequestBodyParameter.Id
                ? RewriteAddParameter(invocation, member)
                : RewriteAddJsonBody(invocation, member);
            if (rewrite == null) continue;

            var title = diagnostic.Id == MigrationDiagnostics.RequestBodyParameter.Id ? "Use 'AddBody'" : "Use 'AddStringBody'";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, rewrite))),
                    equivalenceKey: diagnostic.Id
                ),
                diagnostic
            );
        }
    }

    static InvocationExpressionSyntax? RewriteAddParameter(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member) {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3) return null;

        // Legacy positional shape: AddParameter(name, value, ParameterType.RequestBody[, encode]).
        var nameArg  = arguments[0];
        var valueArg = arguments[1];

        // Only auto-fix the provably-equivalent case: a content-type literal (contains '/').
        if (nameArg.Expression is not LiteralExpressionSyntax { Token.Value: string contentType } || !contentType.Contains('/')) return null;

        var newArguments = SyntaxFactory.SeparatedList([valueArg.WithoutTrivia(), nameArg.WithoutTrivia()]);

        return invocation
            .WithExpression(member.WithName(SyntaxFactory.IdentifierName("AddBody")))
            .WithArgumentList(invocation.ArgumentList.WithArguments(newArguments));
    }

    static InvocationExpressionSyntax RewriteAddJsonBody(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member) {
        var stringArg     = invocation.ArgumentList.Arguments[0].WithoutTrivia();
        var dataFormatArg = SyntaxFactory.Argument(SyntaxFactory.ParseExpression("DataFormat.Json"));
        var newArguments  = SyntaxFactory.SeparatedList([stringArg, dataFormatArg]);

        return invocation
            .WithExpression(member.WithName(SyntaxFactory.IdentifierName("AddStringBody")))
            .WithArgumentList(invocation.ArgumentList.WithArguments(newArguments));
    }
}
