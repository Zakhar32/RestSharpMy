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
/// Flags <c>new NtlmAuthenticator(...)</c> (RSM008). The authenticator was removed in v107; NTLM is now configured
/// through <c>RestClientOptions.UseDefaultCredentials</c> or <c>RestClientOptions.Credentials</c>. No automatic fix is
/// offered because the replacement moves configuration to the client options, which the developer must review.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NtlmAuthenticatorAnalyzer : DiagnosticAnalyzer {
    const string TypeName = "NtlmAuthenticator";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MigrationDiagnostics.NtlmAuthenticator);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        var name = creation.Type switch {
            SimpleNameSyntax simple        => simple,
            QualifiedNameSyntax qualified  => qualified.Right,
            _                              => null
        };
        if (name is not { Identifier.Text: TypeName }) return;
        if (!MigrationContext.IsRestSharpType(context.SemanticModel, name, context.CancellationToken)) return;

        context.ReportDiagnostic(Diagnostic.Create(MigrationDiagnostics.NtlmAuthenticator, name.GetLocation()));
    }
}
