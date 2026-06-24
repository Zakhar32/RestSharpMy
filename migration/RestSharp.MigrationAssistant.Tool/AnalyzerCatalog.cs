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

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using RestSharp.MigrationAssistant;

namespace RestSharp.MigrationAssistant.Tool;

/// <summary>
/// Discovers the analyzers and code-fix providers from the RestSharp.MigrationAssistant assembly via reflection, so the
/// tool automatically picks up new rules without changes here.
/// </summary>
public static class AnalyzerCatalog {
    static readonly Assembly AnalyzerAssembly = typeof(MigrationDiagnostics).Assembly;

    public static ImmutableArray<DiagnosticAnalyzer> Analyzers() => [..Instantiate<DiagnosticAnalyzer>()];

    /// <summary>Maps each fixable diagnostic id to the code-fix provider that handles it.</summary>
    public static IReadOnlyDictionary<string, CodeFixProvider> CodeFixesByDiagnosticId() {
        var map = new Dictionary<string, CodeFixProvider>();

        foreach (var fix in Instantiate<CodeFixProvider>()) {
            foreach (var id in fix.FixableDiagnosticIds) map[id] = fix;
        }

        return map;
    }

    static IEnumerable<T> Instantiate<T>() where T : class
        => AnalyzerAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(T).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (T)Activator.CreateInstance(t)!);
}
