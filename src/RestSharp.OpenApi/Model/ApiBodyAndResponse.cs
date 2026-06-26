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

namespace RestSharp.OpenApi.Model;

/// <summary>
/// A single <c>content</c> entry: a media type (e.g. <c>application/json</c>) and the schema of
/// the payload for that media type.
/// </summary>
public sealed class ApiMediaType {
    public ApiMediaType(string mediaType, ApiSchema? schema) {
        MediaType = mediaType;
        Schema    = schema;
    }

    /// <summary>The media type / content type string.</summary>
    public string MediaType { get; }

    /// <summary>The schema of the payload, or null when the body is opaque (e.g. binary).</summary>
    public ApiSchema? Schema { get; }
}

/// <summary>The request body of an operation, with one entry per declared media type.</summary>
public sealed class ApiRequestBody {
    public ApiRequestBody(IReadOnlyList<ApiMediaType> content, bool required, string? description) {
        Content     = content;
        Required    = required;
        Description = description;
    }

    /// <summary>The declared media types, used for request content negotiation.</summary>
    public IReadOnlyList<ApiMediaType> Content { get; }

    /// <summary>Whether the body is required.</summary>
    public bool Required { get; }

    public string? Description { get; }
}

/// <summary>A single response (keyed by status code or <c>default</c>) of an operation.</summary>
public sealed class ApiResponse {
    public ApiResponse(string statusCode, IReadOnlyList<ApiMediaType> content, string? description) {
        StatusCode  = statusCode;
        Content     = content;
        Description = description;
    }

    /// <summary>The HTTP status code as a string, or <c>"default"</c>.</summary>
    public string StatusCode { get; }

    /// <summary>The declared media types, used for response content negotiation (the <c>Accept</c> header).</summary>
    public IReadOnlyList<ApiMediaType> Content { get; }

    public string? Description { get; }

    /// <summary>True when this entry represents a 2xx success response.</summary>
    public bool IsSuccess => StatusCode.Length == 3 && StatusCode[0] == '2';
}
