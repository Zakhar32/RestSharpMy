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
/// Resolves a string argument that may be either a path to an OpenAPI document or the document
/// content itself. The heuristic is intentionally simple and order-independent: any string that
/// contains a <c>{</c> is treated as inline content (every JSON document has one and no file path
/// does); otherwise an existing file is read; otherwise the string is treated as content (which lets
/// a custom reader handle e.g. inline YAML, and produces a clear parse error if it really was a typo).
/// </summary>
static class DocumentContent {
    public static string Resolve(string pathOrContent) {
        if (pathOrContent.IndexOf('{') >= 0) return pathOrContent;

        try {
            if (File.Exists(pathOrContent)) return File.ReadAllText(pathOrContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new OpenApiParseException($"Failed to read the OpenAPI document at '{pathOrContent}'.", ex);
        }

        return pathOrContent;
    }
}
