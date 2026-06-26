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
/// A single operation parameter (path, query, header or cookie) in the semantic model.
/// </summary>
public sealed class ApiParameter {
    public ApiParameter(string name, ApiParameterLocation location, ApiSchema schema) {
        Name     = name;
        Location = location;
        Schema   = schema;
    }

    /// <summary>The wire name of the parameter.</summary>
    public string Name { get; set; }

    /// <summary>Where the parameter is transported.</summary>
    public ApiParameterLocation Location { get; set; }

    /// <summary>The parameter's schema (carries the type and constraints).</summary>
    public ApiSchema Schema { get; set; }

    /// <summary>Whether the parameter must be supplied. Path parameters are always required.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// The OpenAPI serialization <c>style</c> (e.g. <c>form</c>, <c>simple</c>), or null for the default.
    /// Retained for fidelity; the proxy uses RestSharp's default array handling and documents the gap.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>The OpenAPI <c>explode</c> flag. Defaults vary by style; null means "use the style default".</summary>
    public bool? Explode { get; set; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }
}
