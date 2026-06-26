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

using System.IO;

namespace RestSharp.OpenApi;

/// <summary>
/// Loads and parses OpenAPI documents independently of any <see cref="RestClient"/>. Use this when
/// you want the semantic model or the generated runtime types without (yet) creating a typed client.
/// All loads share the same process-wide cache as <see cref="RestClientOpenApiExtensions.FromOpenApi{T}(IRestClient, string, Action{OpenApiClientOptions})"/>.
/// </summary>
[PublicAPI]
public static class OpenApiDocument {
    /// <summary>Loads a document from a file path or inline content.</summary>
    public static LoadedOpenApiDocument Load(string pathOrContent, Action<OpenApiClientOptions>? configure = null) {
        if (pathOrContent == null) throw new ArgumentNullException(nameof(pathOrContent));
        return OpenApiCache.Load(DocumentContent.Resolve(pathOrContent), BuildOptions(configure));
    }

    /// <summary>Loads a document from a stream.</summary>
    public static LoadedOpenApiDocument Load(Stream stream, Action<OpenApiClientOptions>? configure = null) {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        using var reader = new StreamReader(stream);
        return OpenApiCache.Load(reader.ReadToEnd(), BuildOptions(configure));
    }

    /// <summary>Empties the process-wide document, type and binding-plan caches.</summary>
    public static void ClearCache() => OpenApiCache.Clear();

    static OpenApiClientOptions BuildOptions(Action<OpenApiClientOptions>? configure) {
        var options = new OpenApiClientOptions();
        configure?.Invoke(options);
        return options;
    }
}
