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
/// The JSON Schema validation keywords that the proxy enforces on parameter and body values
/// before a request is sent. Everything is nullable so that "not specified" is distinct from a
/// real bound. Constraints are intentionally a flat bag rather than per-type subclasses because
/// a single OpenAPI schema node can legally mix string, numeric and array keywords.
/// </summary>
public sealed class SchemaConstraints {
    /// <summary>Inclusive lower bound for numeric values (<c>minimum</c>).</summary>
    public double? Minimum { get; set; }

    /// <summary>Inclusive upper bound for numeric values (<c>maximum</c>).</summary>
    public double? Maximum { get; set; }

    /// <summary>When true, <see cref="Minimum"/> is an exclusive bound (<c>exclusiveMinimum</c>).</summary>
    public bool ExclusiveMinimum { get; set; }

    /// <summary>When true, <see cref="Maximum"/> is an exclusive bound (<c>exclusiveMaximum</c>).</summary>
    public bool ExclusiveMaximum { get; set; }

    /// <summary>Numeric value must be a multiple of this (<c>multipleOf</c>).</summary>
    public double? MultipleOf { get; set; }

    /// <summary>Minimum string length (<c>minLength</c>).</summary>
    public int? MinLength { get; set; }

    /// <summary>Maximum string length (<c>maxLength</c>).</summary>
    public int? MaxLength { get; set; }

    /// <summary>Regular expression the string must match (<c>pattern</c>).</summary>
    public string? Pattern { get; set; }

    /// <summary>Minimum number of array items (<c>minItems</c>).</summary>
    public int? MinItems { get; set; }

    /// <summary>Maximum number of array items (<c>maxItems</c>).</summary>
    public int? MaxItems { get; set; }

    /// <summary>When true, array items must be unique (<c>uniqueItems</c>).</summary>
    public bool UniqueItems { get; set; }

    /// <summary>The allowed set of values (<c>enum</c>), as raw strings, or null when unconstrained.</summary>
    public IReadOnlyList<string>? AllowedValues { get; set; }

    /// <summary>True when none of the constraints are set, so validation can be skipped cheaply.</summary>
    public bool IsEmpty =>
        Minimum    == null && Maximum   == null && MultipleOf == null &&
        MinLength  == null && MaxLength == null && Pattern    == null &&
        MinItems   == null && MaxItems  == null && !UniqueItems       &&
        AllowedValues == null;
}
