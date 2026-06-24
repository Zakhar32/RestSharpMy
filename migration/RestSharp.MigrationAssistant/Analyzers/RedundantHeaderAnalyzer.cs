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

using Microsoft.CodeAnalysis.Diagnostics;

namespace RestSharp.MigrationAssistant.Analyzers;

/// <summary>
/// Flags manual <c>Content-Type</c> (RSM006) and <c>Accept</c> (RSM007) headers added to a RestSharp request or client.
/// RestSharp derives both automatically from the request body and the registered serializers, so the explicit calls are
/// redundant and can be removed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RedundantHeaderAnalyzer : DiagnosticAnalyzer {
    static readonly string[] HeaderMethods = [
        "AddHeader", "AddOrUpdateHeader", "AddDefaultHeader", "AddOrUpdateDefaultHeader"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RedundantContentTypeHeader,
        MigrationDiagnostics.RedundantAcceptHeader
    );

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member) return;

        var methodName = member.Name.Identifier.Text;
        if (Array.IndexOf(HeaderMethods, methodName) < 0) return;
        if (invocation.ArgumentList.Arguments.Count == 0) return;

        var headerName = GetStringLiteral(invocation.ArgumentList.Arguments[0].Expression);
        var descriptor = headerName switch {
            not null when IsHeader(headerName, "Content-Type") => MigrationDiagnostics.RedundantContentTypeHeader,
            not null when IsHeader(headerName, "Accept")       => MigrationDiagnostics.RedundantAcceptHeader,
            _                                                  => null
        };
        if (descriptor == null) return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method) return;
        if (!MigrationContext.IsInRestSharp(method.ContainingType)) return;

        context.ReportDiagnostic(Diagnostic.Create(descriptor, member.Name.GetLocation(), methodName));
    }

    static string? GetStringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { Token.Value: string value } ? value : null;

    static bool IsHeader(string value, string header) => string.Equals(value, header, StringComparison.OrdinalIgnoreCase);
}
