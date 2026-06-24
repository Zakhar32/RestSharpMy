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
/// Flags usages of the interfaces removed in RestSharp v107: <c>IRestResponse</c>/<c>IRestResponse&lt;T&gt;</c>
/// (RSM001), <c>IRestRequest</c> (RSM002) and <c>IHttp</c> (RSM003).
/// <para>
/// These types no longer exist in modern RestSharp, so they cannot be bound through the semantic model when the
/// consumer references the current package. Detection is therefore syntactic (by identifier name) and guarded by a
/// RestSharp-context check to avoid flagging unrelated identifiers.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RemovedInterfaceAnalyzer : DiagnosticAnalyzer {
    /// <summary>Property key used to pass the replacement type name to the code fix.</summary>
    public const string ReplacementProperty = "replacement";

    static readonly Dictionary<string, (DiagnosticDescriptor Descriptor, string? Replacement)> Targets = new() {
        ["IRestResponse"] = (MigrationDiagnostics.RestResponseInterface, "RestResponse"),
        ["IRestRequest"]  = (MigrationDiagnostics.RestRequestInterface, "RestRequest"),
        ["IHttp"]         = (MigrationDiagnostics.HttpInterface, null)
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MigrationDiagnostics.RestResponseInterface,
        MigrationDiagnostics.RestRequestInterface,
        MigrationDiagnostics.HttpInterface
    );

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
    }

    static void AnalyzeName(SyntaxNodeAnalysisContext context) {
        var name = (SimpleNameSyntax)context.Node;

        if (!Targets.TryGetValue(name.Identifier.Text, out var target)) return;
        if (!MigrationContext.IsRestSharpType(context.SemanticModel, name, context.CancellationToken)) return;

        var properties = ImmutableDictionary<string, string?>.Empty;
        if (target.Replacement != null) properties = properties.Add(ReplacementProperty, target.Replacement);

        context.ReportDiagnostic(
            target.Replacement != null
                ? Diagnostic.Create(target.Descriptor, name.Identifier.GetLocation(), properties, name.Identifier.Text, target.Replacement)
                : Diagnostic.Create(target.Descriptor, name.Identifier.GetLocation(), properties)
        );
    }
}
