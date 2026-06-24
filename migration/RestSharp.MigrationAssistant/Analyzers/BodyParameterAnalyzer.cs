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
/// Detects legacy body-parameter patterns and points at the modern body helpers:
/// <list type="bullet">
/// <item>RSM004 — <c>AddParameter(name, value, ParameterType.RequestBody)</c> should be <c>AddBody(value, name)</c>.</item>
/// <item>RSM005 — <c>AddJsonBody("&lt;string&gt;")</c> should be <c>AddStringBody(str, DataFormat.Json)</c>.</item>
/// </list>
/// Both rules bind through the semantic model against the modern RestSharp package.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BodyParameterAnalyzer : DiagnosticAnalyzer {
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RequestBodyParameter,
        MigrationDiagnostics.JsonBodyWithString
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
        if (methodName is not ("AddParameter" or "AddJsonBody")) return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method) return;
        if (!MigrationContext.IsInRestSharp(method.ContainingType)) return;

        if (methodName == "AddParameter") AnalyzeAddParameter(context, invocation, member);
        else AnalyzeAddJsonBody(context, invocation, member, method);
    }

    static void AnalyzeAddParameter(
        SyntaxNodeAnalysisContext     context,
        InvocationExpressionSyntax    invocation,
        MemberAccessExpressionSyntax  member
    ) {
        var requestBodyArg = invocation.ArgumentList.Arguments
            .FirstOrDefault(arg => IsRequestBody(context.SemanticModel, arg.Expression, context.CancellationToken));
        if (requestBodyArg == null) return;

        context.ReportDiagnostic(Diagnostic.Create(MigrationDiagnostics.RequestBodyParameter, member.Name.GetLocation(), "AddBody"));
    }

    static void AnalyzeAddJsonBody(
        SyntaxNodeAnalysisContext     context,
        InvocationExpressionSyntax    invocation,
        MemberAccessExpressionSyntax  member,
        IMethodSymbol                 method
    ) {
        // Only the single-argument AddJsonBody(string) overload double-handles a raw string.
        if (invocation.ArgumentList.Arguments.Count != 1) return;

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        var argType  = context.SemanticModel.GetTypeInfo(argument, context.CancellationToken).Type;
        if (argType?.SpecialType != SpecialType.System_String) return;

        context.ReportDiagnostic(Diagnostic.Create(MigrationDiagnostics.JsonBodyWithString, member.Name.GetLocation()));
    }

    static bool IsRequestBody(SemanticModel model, ExpressionSyntax expression, CancellationToken ct)
        => model.GetSymbolInfo(expression, ct).Symbol is IFieldSymbol { ContainingType.Name: "ParameterType", Name: "RequestBody" } field &&
            MigrationContext.IsInRestSharp(field.ContainingType);
}
