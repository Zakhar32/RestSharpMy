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
/// Flags synchronous calls to the RestSharp <c>Execute</c> family (RSM009). The synchronous overloads block on the
/// async API (<c>AsyncHelpers.RunSync</c>), so they are detected by their synchronous <c>RestResponse</c> /
/// <c>RestResponse&lt;T&gt;</c> return type (the async overloads return <c>Task&lt;...&gt;</c>) and the migration is to
/// the matching <c>ExecuteAsync</c> method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SyncExecuteAnalyzer : DiagnosticAnalyzer {
    /// <summary>Property key carrying the async method name (e.g. <c>ExecuteAsync</c>) to the code fix.</summary>
    public const string AsyncNameProperty = "asyncName";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MigrationDiagnostics.SynchronousExecute);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member) return;

        var methodName = member.Name.Identifier.Text;
        if (!methodName.StartsWith("Execute", StringComparison.Ordinal) || methodName.EndsWith("Async", StringComparison.Ordinal)) return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method) return;
        if (!MigrationContext.IsInRestSharp(method.ContainingType)) return;

        // Synchronous overloads return RestSharp's RestResponse / RestResponse<T>; the async ones return Task<...>.
        if (method.ReturnType is not INamedTypeSymbol { Name: "RestResponse" } returnType) return;
        if (!MigrationContext.IsInRestSharp(returnType.ContainingNamespace)) return;

        var asyncName  = methodName + "Async";
        var properties = ImmutableDictionary<string, string?>.Empty.Add(AsyncNameProperty, asyncName);

        context.ReportDiagnostic(Diagnostic.Create(MigrationDiagnostics.SynchronousExecute, member.Name.GetLocation(), properties, methodName, asyncName));
    }
}
