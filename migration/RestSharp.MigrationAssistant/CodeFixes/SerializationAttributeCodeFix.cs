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
using Microsoft.CodeAnalysis.Editing;
using RestSharp.MigrationAssistant.Analyzers;

namespace RestSharp.MigrationAssistant.CodeFixes;

/// <summary>
/// Rewrites legacy <c>[SerializeAs]</c>/<c>[DeserializeAs]</c> attributes (RSM010) to the equivalent JSON property
/// attribute, collapsing a matching serialize/deserialize pair into a single attribute. The fix is offered only when it
/// is safe and meaningful: the attributes carry only a string-literal <c>Name</c> (no XML-only options), the serialize
/// and deserialize names agree, and a JSON serializer attribute is available. The replacement is fully qualified so it
/// compiles without adding a using directive. RSM011 (conflict) has no automatic fix.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializationAttributeCodeFix)), Shared]
public class SerializationAttributeCodeFix : CodeFixProvider {
    static readonly Dictionary<string, string> QualifiedTargets = new() {
        ["JsonPropertyName"] = "System.Text.Json.Serialization.JsonPropertyName",
        ["JsonProperty"]     = "Newtonsoft.Json.JsonProperty"
    };

    static readonly string[] XmlOnlyOptions = ["Attribute", "Content", "Index", "NameStyle", "Culture"];

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(MigrationDiagnostics.SerializationAttribute.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics) {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute) continue;
            if (attribute.Parent?.Parent is not MemberDeclarationSyntax member) continue;
            if (!diagnostic.Properties.TryGetValue(SerializationAttributeAnalyzer.TargetAttributeProperty, out var target) || target == null) continue;
            if (!QualifiedTargets.TryGetValue(target, out var qualifiedTarget)) continue;

            var legacyAttributes = LegacyAttributes(member).ToList();
            if (!TryResolveName(legacyAttributes, out var name)) continue;   // XML-only options / asymmetric names -> manual

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use [{target}(\"{name}\")]",
                    ct => RewriteAsync(context.Document, member, legacyAttributes, qualifiedTarget, name, ct),
                    equivalenceKey: diagnostic.Id
                ),
                diagnostic
            );
        }
    }

    static async Task<Document> RewriteAsync(
        Document            document,
        MemberDeclarationSyntax member,
        List<AttributeSyntax>   legacyAttributes,
        string                  qualifiedTarget,
        string                  name,
        CancellationToken       ct
    ) {
        var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

        foreach (var attribute in legacyAttributes) editor.RemoveNode(attribute);

        var argument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(name))
        );
        var jsonAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(qualifiedTarget))
            .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(argument)));

        editor.AddAttribute(member, jsonAttribute);

        return editor.GetChangedDocument();
    }

    static IEnumerable<AttributeSyntax> LegacyAttributes(MemberDeclarationSyntax member)
        => member.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(a => SerializationAttributeAnalyzer.SimpleName(a.Name) is "SerializeAs" or "SerializeAsAttribute" or "DeserializeAs" or "DeserializeAsAttribute");

    /// <summary>Resolves the single JSON name to use, or returns false when the legacy attributes cannot be mapped
    /// (XML-only options present, or the serialize/deserialize names disagree, or no name is set).</summary>
    static bool TryResolveName(List<AttributeSyntax> legacyAttributes, out string name) {
        name = "";
        string? resolved = null;

        foreach (var attribute in legacyAttributes) {
            foreach (var argument in attribute.ArgumentList?.Arguments ?? default) {
                var argumentName = argument.NameEquals?.Name.Identifier.Text;

                if (Array.IndexOf(XmlOnlyOptions, argumentName) >= 0) return false;

                if (argumentName == "Name") {
                    if (argument.Expression is not LiteralExpressionSyntax { Token.Value: string value }) return false;
                    if (resolved != null && resolved != value) return false;   // serialize/deserialize names disagree
                    resolved = value;
                }
            }
        }

        if (resolved == null) return false;
        name = resolved;
        return true;
    }
}
