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

using RestSharp.OpenApi.Emit;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi;

/// <summary>
/// A parsed OpenAPI document together with the runtime CLR types generated from its schemas. This is
/// the reusable, cacheable unit: parse and type-generation happen once, then any number of typed
/// clients can be created from it. Exposed publicly so callers can also work with the semantic model
/// and the generated types directly, not only through a proxy.
/// </summary>
public sealed class LoadedOpenApiDocument {
    readonly RuntimeTypeFactory _typeFactory;

    internal LoadedOpenApiDocument(OpenApiDocumentModel model, RuntimeTypeFactory typeFactory, OpenApiClientOptions options) {
        Model        = model;
        _typeFactory = typeFactory;
        Options      = options;
    }

    /// <summary>The resolved semantic model.</summary>
    public OpenApiDocumentModel Model { get; }

    /// <summary>The content hash used as the cache key.</summary>
    public string SourceHash => Model.SourceHash;

    /// <summary>The CLR types generated from the document's named object schemas, keyed by schema name.</summary>
    public IReadOnlyDictionary<string, Type> GeneratedTypes => _typeFactory.GeneratedTypes;

    /// <summary>Returns the runtime type generated for a named schema, or null when there isn't one.</summary>
    public Type? GetGeneratedType(string schemaName) => _typeFactory.TryGetType(schemaName);

    internal OpenApiClientOptions Options      { get; }
    internal RuntimeTypeFactory   TypeFactory  => _typeFactory;
}
