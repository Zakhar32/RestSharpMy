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
/// A single API operation (one HTTP method on one path) in the semantic model. This is the unit
/// that an interface method is bound to.
/// </summary>
public sealed class ApiOperation {
    public ApiOperation(string? operationId, Method method, string path) {
        OperationId = operationId;
        Method      = method;
        Path        = path;
    }

    /// <summary>The OpenAPI <c>operationId</c>, or null when the document omits it.</summary>
    public string? OperationId { get; set; }

    /// <summary>The HTTP method, mapped onto RestSharp's <see cref="RestSharp.Method"/>.</summary>
    public Method Method { get; set; }

    /// <summary>
    /// The path template relative to the server base path, with <c>{name}</c> placeholders intact
    /// (these become RestSharp URL segments). Never starts with a leading slash so it composes with
    /// the client's base URL.
    /// </summary>
    public string Path { get; set; }

    /// <summary>The operation's parameters (path, query, header, cookie).</summary>
    public IList<ApiParameter> Parameters { get; } = new List<ApiParameter>();

    /// <summary>The request body, or null when the operation takes no body.</summary>
    public ApiRequestBody? RequestBody { get; set; }

    /// <summary>The declared responses.</summary>
    public IList<ApiResponse> Responses { get; } = new List<ApiResponse>();

    /// <summary>Operation tags.</summary>
    public IList<string> Tags { get; } = new List<string>();

    public string? Summary { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Returns the first declared 2xx response, falling back to the <c>default</c> response, then the
    /// first response of any kind. Used to choose the response content type and success schema.
    /// </summary>
    public ApiResponse? PrimarySuccessResponse {
        get {
            ApiResponse? defaultResponse = null;
            ApiResponse? first           = null;

            foreach (var response in Responses) {
                first ??= response;
                if (response.IsSuccess) return response;
                if (response.StatusCode == "default") defaultResponse = response;
            }

            return defaultResponse ?? first;
        }
    }

    public override string ToString() => $"{Method.ToString().ToUpperInvariant()} /{Path} ({OperationId})";
}
