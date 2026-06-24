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
/// Rewrites a synchronous <c>Execute</c> call (RSM009) to <c>await ExecuteAsync</c>. The fix is offered only when the
/// call sits in an <c>async</c> method, local function or lambda, so the result always compiles; outside an async
/// context the diagnostic is reported without an automatic fix, because making the enclosing member async (and its
/// callers) is a refactor the developer must drive.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncExecuteCodeFix)), Shared]
public class SyncExecuteCodeFix : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(MigrationDiagnostics.SynchronousExecute.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics) {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;
            if (!IsInAsyncContext(invocation)) continue;
            if (!diagnostic.Properties.TryGetValue(SyncExecuteAnalyzer.AsyncNameProperty, out var asyncName) || asyncName == null) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use 'await {asyncName}'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation, member, asyncName)))),
                    equivalenceKey: diagnostic.Id
                ),
                diagnostic
            );
        }
    }

    static SyntaxNode Rewrite(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member, string asyncName) {
        SimpleNameSyntax newName = member.Name is GenericNameSyntax generic
            ? generic.WithIdentifier(SyntaxFactory.Identifier(asyncName))
            : SyntaxFactory.IdentifierName(asyncName);

        var newInvocation = invocation.WithExpression(member.WithName(newName)).WithoutTrivia();
        var awaitKeyword  = SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        ExpressionSyntax rewritten = SyntaxFactory.AwaitExpression(awaitKeyword, newInvocation);
        if (NeedsParentheses(invocation)) rewritten = SyntaxFactory.ParenthesizedExpression(rewritten);

        return rewritten
            .WithLeadingTrivia(invocation.GetLeadingTrivia())
            .WithTrailingTrivia(invocation.GetTrailingTrivia());
    }

    // The await result is consumed by a further access (e.g. Execute(r).Content), so it must be parenthesised.
    static bool NeedsParentheses(InvocationExpressionSyntax invocation)
        => invocation.Parent is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax or ElementAccessExpressionSyntax or PostfixUnaryExpressionSyntax;

    static bool IsInAsyncContext(SyntaxNode node) {
        foreach (var ancestor in node.Ancestors()) {
            switch (ancestor) {
                case MethodDeclarationSyntax method:                return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax localFunction:    return localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case AnonymousFunctionExpressionSyntax anonymous:   return anonymous.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case AccessorDeclarationSyntax:                     return false;
            }
        }

        return false;
    }
}
