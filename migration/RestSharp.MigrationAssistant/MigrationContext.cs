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

namespace RestSharp.MigrationAssistant;

/// <summary>
/// Shared helpers for deciding whether a syntax node belongs to RestSharp, used to keep the migration
/// analyzers from flagging unrelated code that happens to use similar names.
/// </summary>
static class MigrationContext {
    const string RestSharpRoot = "RestSharp";

    /// <summary>
    /// Returns true when <paramref name="name"/> refers to a RestSharp type. When the symbol binds (e.g. via a
    /// legacy shim) the containing namespace is checked; when it does not bind (legacy code compiled against the
    /// modern package, where the type was removed) the file is required to import or qualify the RestSharp namespace.
    /// </summary>
    public static bool IsRestSharpType(SemanticModel model, SimpleNameSyntax name, CancellationToken ct) {
        var symbol = model.GetSymbolInfo(name, ct).Symbol;
        if (symbol is INamedTypeSymbol named) return IsInRestSharp(named.ContainingNamespace);

        // Unresolved (the type was removed): fall back to a syntactic RestSharp-context check.
        return IsQualifiedWithRestSharp(name) || HasRestSharpUsing(name.SyntaxTree);
    }

    /// <summary>
    /// Returns true when <paramref name="attribute"/> is a RestSharp attribute. The constructor symbol's containing type
    /// is checked when it binds (the attribute may live in RestSharp.Serializers.Xml); otherwise the file is required to
    /// import or qualify the RestSharp namespace.
    /// </summary>
    public static bool IsRestSharpAttribute(SemanticModel model, AttributeSyntax attribute, CancellationToken ct) {
        if (model.GetSymbolInfo(attribute, ct).Symbol?.ContainingType is { } type) return IsInRestSharp(type);

        return IsQualifiedWithRestSharp(attribute.Name) || HasRestSharpUsing(attribute.SyntaxTree);
    }

    /// <summary>Returns true if the symbol is declared anywhere under the <c>RestSharp</c> namespace.</summary>
    public static bool IsInRestSharp(ISymbol? symbol) => symbol is { } && IsInRestSharp(GetNamespace(symbol));

    static bool IsInRestSharp(INamespaceSymbol? ns) {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace) {
            if (current is { ContainingNamespace.IsGlobalNamespace: true, Name: RestSharpRoot }) return true;
        }

        return false;
    }

    static INamespaceSymbol? GetNamespace(ISymbol symbol)
        => symbol as INamespaceSymbol ?? (symbol as ITypeSymbol)?.ContainingNamespace ?? symbol.ContainingNamespace;

    static bool IsQualifiedWithRestSharp(SyntaxNode name) {
        // RestSharp.IRestResponse -> the parent qualified name starts with the RestSharp identifier.
        for (var node = name.Parent; node is QualifiedNameSyntax qualified; node = node.Parent) {
            if (qualified.Left.ToString().Split('.')[0] == RestSharpRoot) return true;
        }

        return false;
    }

    static bool HasRestSharpUsing(SyntaxTree tree) {
        if (tree.GetRoot() is not CompilationUnitSyntax root) return false;

        foreach (var directive in root.Usings) {
            var ns = directive.Name?.ToString();
            if (ns == RestSharpRoot || (ns != null && ns.StartsWith(RestSharpRoot + "."))) return true;
        }

        return false;
    }
}
