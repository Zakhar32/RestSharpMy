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
/// Flags the legacy RestSharp serialization attributes on model members:
/// <list type="bullet">
/// <item>RSM010 — <c>[SerializeAs]</c>/<c>[DeserializeAs]</c>, which only affect XML; maps the name to a JSON attribute.</item>
/// <item>RSM011 — a member that mixes a legacy attribute with a modern JSON attribute (an ambiguous conflict).</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SerializationAttributeAnalyzer : DiagnosticAnalyzer {
    /// <summary>Property key carrying the chosen JSON attribute name (JsonPropertyName/JsonProperty) to the code fix.</summary>
    public const string TargetAttributeProperty = "target";

    static readonly string[] LegacyNames = ["SerializeAs", "SerializeAsAttribute", "DeserializeAs", "DeserializeAsAttribute"];
    static readonly string[] ModernNames = ["JsonPropertyName", "JsonPropertyNameAttribute", "JsonProperty", "JsonPropertyAttribute"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MigrationDiagnostics.SerializationAttribute,
        MigrationDiagnostics.SerializationAttributeConflict
    );

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    static void AnalyzeAttribute(SyntaxNodeAnalysisContext context) {
        var attribute = (AttributeSyntax)context.Node;
        var name      = SimpleName(attribute.Name);
        if (Array.IndexOf(LegacyNames, name) < 0) return;

        if (!MigrationContext.IsRestSharpAttribute(context.SemanticModel, attribute, context.CancellationToken)) return;

        var displayName = name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
        var member      = attribute.Parent?.Parent;

        if (member != null && HasModernJsonAttribute(member)) {
            context.ReportDiagnostic(Diagnostic.Create(MigrationDiagnostics.SerializationAttributeConflict, attribute.GetLocation(), displayName));
            return;
        }

        var target     = DetermineTarget(context.SemanticModel.Compilation);
        var properties = target == null
            ? ImmutableDictionary<string, string?>.Empty
            : ImmutableDictionary<string, string?>.Empty.Add(TargetAttributeProperty, target);

        context.ReportDiagnostic(
            Diagnostic.Create(MigrationDiagnostics.SerializationAttribute, attribute.GetLocation(), properties, displayName, target ?? "a JSON property attribute")
        );
    }

    static bool HasModernJsonAttribute(SyntaxNode member)
        => member is MemberDeclarationSyntax declaration &&
            declaration.AttributeLists.SelectMany(l => l.Attributes).Any(a => Array.IndexOf(ModernNames, SimpleName(a.Name)) >= 0);

    static string? DetermineTarget(Compilation compilation)
        => compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonPropertyNameAttribute") != null ? "JsonPropertyName"
            : compilation.GetTypeByMetadataName("Newtonsoft.Json.JsonPropertyAttribute") != null ? "JsonProperty"
            : null;

    internal static string SimpleName(NameSyntax name)
        => name switch {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            SimpleNameSyntax simple       => simple.Identifier.Text,
            _                             => ""
        };
}
