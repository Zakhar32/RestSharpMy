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
/// The root of the semantic model: the normalised, resolved representation of an OpenAPI document.
/// This is the contract between the parser and everything downstream (proxy binding, type
/// generation). It is deliberately decoupled from any specific OpenAPI parsing library so that the
/// document reader can be swapped out (e.g. for YAML) without touching the rest of the pipeline.
/// </summary>
public sealed class OpenApiDocumentModel {
    /// <summary>The API title from the <c>info</c> object.</summary>
    public string Title { get; set; } = "";

    /// <summary>The API version from the <c>info</c> object.</summary>
    public string ApiVersion { get; set; } = "";

    /// <summary>
    /// The server base path (the path portion of the first <c>servers</c> entry), or empty. This is
    /// prepended to operation paths. The host/scheme part is intentionally ignored - the
    /// <see cref="RestClient"/>'s base URL owns that.
    /// </summary>
    public string BasePath { get; set; } = "";

    /// <summary>All operations across all paths.</summary>
    public IList<ApiOperation> Operations { get; } = new List<ApiOperation>();

    /// <summary>The named schemas from <c>components/schemas</c>, keyed by component name.</summary>
    public IDictionary<string, ApiSchema> Schemas { get; } = new Dictionary<string, ApiSchema>(StringComparer.Ordinal);

    /// <summary>
    /// A stable content hash of the source document used as the cache key. Set by the parser.
    /// </summary>
    public string SourceHash { get; set; } = "";

    /// <summary>Finds an operation by its <c>operationId</c> (case-insensitive), or null.</summary>
    public ApiOperation? FindOperation(string operationId) {
        foreach (var operation in Operations) {
            if (string.Equals(operation.OperationId, operationId, StringComparison.OrdinalIgnoreCase)) return operation;
        }

        return null;
    }

    /// <summary>Finds an operation by HTTP method and path template (case-insensitive on the path), or null.</summary>
    public ApiOperation? FindOperation(Method method, string path) {
        var normalized = path.TrimStart('/');

        foreach (var operation in Operations) {
            if (operation.Method == method && string.Equals(operation.Path, normalized, StringComparison.OrdinalIgnoreCase)) return operation;
        }

        return null;
    }
}
