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

using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RestSharp.OpenApi.Emit;
using RestSharp.OpenApi.Model;
using RestSharp.OpenApi.Parsing;
using RestSharp.OpenApi.Proxy;

namespace RestSharp.OpenApi;

/// <summary>
/// Process-wide, thread-safe caches keyed by the document's content hash. They exist so that parsing,
/// runtime type generation and per-interface binding - the expensive, one-time work - is not repeated
/// on every call or every client creation.
/// </summary>
/// <remarks>
/// <para>
/// Each cache uses <see cref="ConcurrentDictionary{TKey,TValue}"/> of <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> semantics, so even under heavy
/// concurrency a given document is parsed (and its types emitted) exactly once.
/// </para>
/// <para>
/// Documented tradeoff: a <see cref="DispatchProxy"/> type cannot be serialized, so the cache is
/// in-process only - it cannot survive a process restart. What it does guarantee is that within a
/// process, repeated <c>FromOpenApi</c> calls for the same document and interface are O(1) lookups,
/// which is what "avoid regeneration on every startup" means for a long-lived host. To persist work
/// across restarts, persist the textual document yourself (it hashes identically) and rely on these
/// caches at runtime. <see cref="Clear"/> empties all caches (useful in tests).
/// </para>
/// </remarks>
static class OpenApiCache {
    static readonly ConcurrentDictionary<string, Lazy<LoadedOpenApiDocument>> Documents = new();

    static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<MethodInfo, OperationBinding>>> Plans = new();

    public static LoadedOpenApiDocument Load(string content, OpenApiClientOptions options) {
        var hash = ComputeHash(content);

        if (options.DisableCache) return BuildDocument(content, hash, options);

        var key = $"{hash}|{options.ModelAffectingKey()}";
        return Documents.GetOrAdd(key, _ => new Lazy<LoadedOpenApiDocument>(() => BuildDocument(content, hash, options))).Value;
    }

    public static IReadOnlyDictionary<MethodInfo, OperationBinding> GetPlan(Type interfaceType, LoadedOpenApiDocument document, OpenApiClientOptions options) {
        if (options.DisableCache) return BindingPlanBuilder.Build(interfaceType, document.Model, options, document.TypeFactory);

        var key = $"{interfaceType.AssemblyQualifiedName}::{document.SourceHash}|{options.ModelAffectingKey()}|{options.ComputeBehaviourKey()}";
        return Plans.GetOrAdd(key, _ => new Lazy<IReadOnlyDictionary<MethodInfo, OperationBinding>>(
            () => BindingPlanBuilder.Build(interfaceType, document.Model, options, document.TypeFactory))).Value;
    }

    public static void Clear() {
        Documents.Clear();
        Plans.Clear();
    }

    static LoadedOpenApiDocument BuildDocument(string content, string hash, OpenApiClientOptions options) {
        var model   = BuildModel(content, hash, options);
        var factory = new RuntimeTypeFactory(model, options.RuntimeTypeNamespace);
        return new LoadedOpenApiDocument(model, factory, options);
    }

    static OpenApiDocumentModel BuildModel(string content, string hash, OpenApiClientOptions options) {
        using var document = options.DocumentReader.Read(content);

        // The model builder copies everything it needs out of the JSON, so the JsonDocument can be
        // disposed once Build returns - nothing downstream holds a JsonElement.
        var model = OpenApiModelBuilder.Build(document.RootElement, hash);

        options.DocumentTransformer?.Invoke(model);
        return model;
    }

    static string ComputeHash(string content) {
        using var sha   = SHA256.Create();
        var       bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        var       sb    = new StringBuilder(bytes.Length * 2);

        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
