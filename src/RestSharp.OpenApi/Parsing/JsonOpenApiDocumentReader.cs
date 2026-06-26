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

using System.Text.Json;

namespace RestSharp.OpenApi.Parsing;

/// <summary>
/// The default <see cref="IOpenApiDocumentReader"/>. Parses JSON OpenAPI documents using
/// <see cref="System.Text.Json"/>, the same serializer RestSharp uses by default, so no extra
/// dependency is pulled in. Comments and trailing commas are tolerated to be forgiving of
/// hand-edited documents.
/// </summary>
/// <remarks>
/// YAML is intentionally out of scope for the default reader to avoid a YAML dependency in the
/// core package. Supply a custom <see cref="IOpenApiDocumentReader"/> via
/// <see cref="RestSharp.OpenApi.OpenApiClientOptions.DocumentReader"/> to support YAML or other formats.
/// </remarks>
public sealed class JsonOpenApiDocumentReader : IOpenApiDocumentReader {
    static readonly JsonDocumentOptions Options = new() {
        AllowTrailingCommas = true,
        CommentHandling     = JsonCommentHandling.Skip
    };

    public JsonDocument Read(string content) {
        try {
            return JsonDocument.Parse(content, Options);
        }
        catch (JsonException ex) {
            throw new OpenApiParseException(
                "The OpenAPI document is not valid JSON. If the document is in YAML format, supply a YAML " +
                "document reader via OpenApiClientOptions.DocumentReader.",
                ex
            );
        }
    }
}
