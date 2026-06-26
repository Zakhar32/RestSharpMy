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
using System.Text;
using RestSharp.OpenApi.Proxy;

namespace RestSharp.OpenApi;

/// <summary>
/// Entry points that turn a <see cref="RestClient"/> into a strongly-typed API gateway driven by an
/// OpenAPI document. The generated proxy executes through the supplied client, so its serializers,
/// authenticators and interceptors are reused verbatim.
/// </summary>
[PublicAPI]
public static class RestClientOpenApiExtensions {
    /// <summary>
    /// Generates a strongly-typed implementation of <typeparamref name="T"/> from an OpenAPI document.
    /// </summary>
    /// <param name="client">The client whose pipeline the generated proxy will use.</param>
    /// <param name="documentPathOrContent">A file path to the document, or the document content itself (JSON, or any format the configured reader understands).</param>
    /// <param name="configure">Optional configuration of the generation behaviour and extensibility hooks.</param>
    /// <typeparam name="T">The interface to implement.</typeparam>
    public static T FromOpenApi<T>(this IRestClient client, string documentPathOrContent, Action<OpenApiClientOptions>? configure = null)
        where T : class {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (documentPathOrContent == null) throw new ArgumentNullException(nameof(documentPathOrContent));

        var options  = BuildOptions(configure);
        var document = OpenApiCache.Load(DocumentContent.Resolve(documentPathOrContent), options);
        return CreateProxy<T>(client, document, options);
    }

    /// <summary>Generates a strongly-typed implementation of <typeparamref name="T"/> from a document stream.</summary>
    public static T FromOpenApi<T>(this IRestClient client, Stream documentStream, Action<OpenApiClientOptions>? configure = null)
        where T : class {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (documentStream == null) throw new ArgumentNullException(nameof(documentStream));

        var options  = BuildOptions(configure);
        var document = OpenApiCache.Load(ReadStream(documentStream), options);
        return CreateProxy<T>(client, document, options);
    }

    /// <summary>
    /// Generates a strongly-typed implementation of <typeparamref name="T"/> from an already-loaded
    /// document. Prefer this overload when you create several clients from the same document, or when
    /// you also want to inspect the model / generated types.
    /// </summary>
    public static T FromOpenApi<T>(this IRestClient client, LoadedOpenApiDocument document, Action<OpenApiClientOptions>? configure = null)
        where T : class {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (document == null) throw new ArgumentNullException(nameof(document));

        var options = BuildOptions(configure);
        return CreateProxy<T>(client, document, options);
    }

    /// <summary>
    /// Parses and loads an OpenAPI document (and generates its runtime types) without creating a proxy.
    /// Useful for inspecting the semantic model or the generated types directly.
    /// </summary>
    public static LoadedOpenApiDocument LoadOpenApi(this IRestClient client, string documentPathOrContent, Action<OpenApiClientOptions>? configure = null)
        => OpenApiCache.Load(DocumentContent.Resolve(documentPathOrContent), BuildOptions(configure));

    static T CreateProxy<T>(IRestClient client, LoadedOpenApiDocument document, OpenApiClientOptions options) where T : class {
        var plan = OpenApiCache.GetPlan(typeof(T), document, options);
        return ApiDispatchProxy.Create<T>(client, plan, options);
    }

    static OpenApiClientOptions BuildOptions(Action<OpenApiClientOptions>? configure) {
        var options = new OpenApiClientOptions();
        configure?.Invoke(options);
        return options;
    }

    static string ReadStream(Stream stream) {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
