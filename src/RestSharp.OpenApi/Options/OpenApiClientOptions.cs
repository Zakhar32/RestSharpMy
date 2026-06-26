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

using System.Reflection;
using RestSharp.OpenApi.Model;
using RestSharp.OpenApi.Parsing;

namespace RestSharp.OpenApi;

/// <summary>
/// Configures how a strongly-typed client is generated from an OpenAPI document. Every member is an
/// extensibility seam: they exist so developers can override generated behaviour or paper over an
/// imperfect document without forking the library.
/// </summary>
public sealed class OpenApiClientOptions {
    /// <summary>
    /// Reads the raw document into JSON. Defaults to <see cref="JsonOpenApiDocumentReader"/>. Replace
    /// it to support YAML or any other format.
    /// </summary>
    public IOpenApiDocumentReader DocumentReader { get; set; } = new JsonOpenApiDocumentReader();

    /// <summary>
    /// Runs after the document is parsed into the semantic model and before any binding or type
    /// generation. The primary hook for correcting an imperfect document at runtime: add a missing
    /// <c>operationId</c>, fix a wrong parameter location, relax a constraint, and so on.
    /// </summary>
    /// <remarks>
    /// The transformer mutates the model that may be shared via the document cache. It must be
    /// deterministic. When caching is enabled (the default), the transformer's method identity is
    /// folded into the cache key, so two distinct transformer methods over the same document produce
    /// two cached models; two different lambdas that close over different captured state but share a
    /// method body will collide - in that case set a distinct <see cref="CacheKey"/> or disable
    /// caching.
    /// </remarks>
    public Action<OpenApiDocumentModel>? DocumentTransformer { get; set; }

    /// <summary>
    /// Overrides how interface methods are matched to operations. Return the operation to bind, or
    /// null to fall back to the built-in resolver (operationId / name convention / attributes).
    /// </summary>
    public Func<MethodInfo, OpenApiDocumentModel, ApiOperation?>? OperationResolver { get; set; }

    /// <summary>
    /// Maps an interface method name to a candidate <c>operationId</c> for the default resolver. The
    /// default strips a trailing <c>Async</c>. Override to apply a different naming policy.
    /// </summary>
    public Func<string, string>? MethodNameToOperationId { get; set; }

    /// <summary>
    /// When true (default), parameter and body values are validated against the schema constraints
    /// (min/max, length, pattern, enum, required, ...) before the request is sent.
    /// </summary>
    public bool ValidateConstraints { get; set; } = true;

    /// <summary>
    /// When true (default), an <c>Accept</c> header is set from the operation's response media types
    /// so the server performs proper content negotiation. RestSharp already sets <c>Accept</c> from
    /// its serializers; this narrows it to what the operation actually declares.
    /// </summary>
    public bool SendAcceptHeader { get; set; } = true;

    /// <summary>
    /// Preferred media type when an operation's request body declares more than one. When set and
    /// available, it wins; otherwise JSON is preferred, then the first declared type.
    /// </summary>
    public string? PreferredRequestContentType { get; set; }

    /// <summary>
    /// Preferred media type for the <c>Accept</c> header when an operation declares several response
    /// media types. Same precedence rules as <see cref="PreferredRequestContentType"/>.
    /// </summary>
    public string? PreferredResponseContentType { get; set; }

    /// <summary>
    /// Runs just before each generated request executes, after binding. Use it to override generated
    /// behaviour per call (add a header, tweak the resource, etc.).
    /// </summary>
    public Action<OpenApiRequestContext>? ConfigureRequest { get; set; }

    /// <summary>
    /// When a method's return type is <see cref="object"/> (or <c>Task&lt;object&gt;</c>), deserialize
    /// the response into a CLR type generated at runtime from the operation's response schema rather
    /// than into a raw dictionary. Defaults to true.
    /// </summary>
    public bool UseRuntimeTypesForObjectReturns { get; set; } = true;

    /// <summary>The namespace used for runtime-generated CLR types. Cosmetic; affects type full names only.</summary>
    public string RuntimeTypeNamespace { get; set; } = "RestSharp.OpenApi.Generated";

    /// <summary>
    /// When true (default), the server base path from the document's <c>servers</c> entry is prepended
    /// to each operation path, matching OpenAPI semantics (operation paths are relative to the server
    /// URL). Set to false when the <see cref="RestClient"/>'s base URL already includes that path,
    /// to avoid doubling it.
    /// </summary>
    public bool IncludeServerBasePath { get; set; } = true;

    /// <summary>
    /// Disables the in-process document and binding-plan caches for this call. Useful when supplying a
    /// transformer whose effect varies in a way the cache key can't capture, at the cost of re-parsing.
    /// </summary>
    public bool DisableCache { get; set; }

    /// <summary>
    /// An extra component folded into the cache key. Set this to a stable value when you reuse a
    /// transformer or resolver whose behaviour varies between callers.
    /// </summary>
    public string? CacheKey { get; set; }

    /// <summary>
    /// The part of the cache key that affects how the document is parsed and its types generated:
    /// the transformer, the reader and the generated-type namespace. Keying the document cache on this
    /// keeps two distinct transformers from sharing a cached (and mutated) model.
    /// </summary>
    internal string ModelAffectingKey()
        => CacheKey != null
            ? "ck:" + CacheKey
            : string.Join("|", DelegateToken(DocumentTransformer), DocumentReader.GetType().FullName, RuntimeTypeNamespace);

    /// <summary>
    /// The part of the cache key that affects how methods bind: the resolver, naming policy, content
    /// negotiation preferences and validation flags. Used to key the per-interface binding plan.
    /// </summary>
    internal string ComputeBehaviourKey() {
        if (CacheKey != null) return "ck:" + CacheKey;

        return string.Join(
            "|",
            DelegateToken(OperationResolver),
            DelegateToken(MethodNameToOperationId),
            ValidateConstraints ? "v" : "-",
            SendAcceptHeader ? "a" : "-",
            IncludeServerBasePath ? "b" : "-",
            PreferredRequestContentType ?? "",
            PreferredResponseContentType ?? "",
            UseRuntimeTypesForObjectReturns ? "r" : "-"
        );
    }

    static string DelegateToken(Delegate? d)
        => d == null ? "" : $"{d.Method.DeclaringType?.FullName}.{d.Method.Name}#{d.Method.MetadataToken}";
}
