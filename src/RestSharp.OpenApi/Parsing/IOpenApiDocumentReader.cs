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
/// Reads the raw text of an OpenAPI document into a <see cref="JsonDocument"/> that the model
/// builder understands. This is the seam for supporting formats other than JSON: a YAML reader,
/// for example, would convert YAML to a <see cref="JsonDocument"/> here. The model builder never
/// sees the original format.
/// </summary>
public interface IOpenApiDocumentReader {
    /// <summary>
    /// Parses the raw document text into a <see cref="JsonDocument"/>. The caller owns and disposes
    /// the returned document.
    /// </summary>
    /// <param name="content">The raw document text.</param>
    JsonDocument Read(string content);
}
