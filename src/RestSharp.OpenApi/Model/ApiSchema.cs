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
/// A normalised, resolved OpenAPI schema node. References (<c>$ref</c>) are resolved into the
/// actual <see cref="ApiSchema"/> instances they point at, so a graph of <see cref="ApiSchema"/>
/// can contain cycles (e.g. a tree node that references itself). Consumers that walk the graph
/// must guard against cycles using <see cref="Name"/> identity.
/// </summary>
public sealed class ApiSchema {
    /// <summary>
    /// The component name (the key under <c>components/schemas</c>) when this schema is a named,
    /// top-level schema; <c>null</c> for inline/anonymous schemas. Named schemas are the ones the
    /// runtime type factory materialises into CLR types.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>The normalised shape of this schema.</summary>
    public SchemaKind Kind { get; set; } = SchemaKind.Unknown;

    /// <summary>The primitive type for <see cref="SchemaKind.Primitive"/> / <see cref="SchemaKind.Enum"/> schemas.</summary>
    public PrimitiveType Primitive { get; set; } = PrimitiveType.String;

    /// <summary>The OpenAPI <c>format</c> (e.g. <c>int64</c>, <c>date-time</c>, <c>uuid</c>), or null.</summary>
    public string? Format { get; set; }

    /// <summary>True when the value may be <c>null</c> (OpenAPI 3.0 <c>nullable: true</c> or 3.1 <c>type: [..., "null"]</c>).</summary>
    public bool Nullable { get; set; }

    /// <summary>Human-readable description, surfaced on generated types as XML doc / attribute where possible.</summary>
    public string? Description { get; set; }

    /// <summary>Validation constraints attached to this schema.</summary>
    public SchemaConstraints Constraints { get; set; } = new();

    /// <summary>The element schema for <see cref="SchemaKind.Array"/>.</summary>
    public ApiSchema? Items { get; set; }

    /// <summary>The value schema for <see cref="SchemaKind.Map"/> (from <c>additionalProperties</c>).</summary>
    public ApiSchema? AdditionalProperties { get; set; }

    /// <summary>The declared properties for <see cref="SchemaKind.Object"/>, in document order.</summary>
    public IList<ApiProperty> Properties { get; } = new List<ApiProperty>();

    /// <summary>The allowed enum values for <see cref="SchemaKind.Enum"/> (raw string form).</summary>
    public IList<string> EnumValues { get; } = new List<string>();

    /// <summary>
    /// For <see cref="SchemaKind.Composite"/>: the constituent sub-schemas (<c>oneOf</c>/<c>anyOf</c>).
    /// </summary>
    public IList<ApiSchema> Composition { get; } = new List<ApiSchema>();

    /// <summary>
    /// Discriminator information for polymorphic schemas, or null. Present on both the base schema
    /// (when it declares the discriminator) and propagated to <see cref="SchemaKind.Composite"/> nodes.
    /// </summary>
    public DiscriminatorInfo? Discriminator { get; set; }

    /// <summary>
    /// For object schemas produced by <c>allOf</c> composition: the single schema this one inherits
    /// from, when exactly one <c>allOf</c> entry is a named object schema. Used to emit CLR inheritance.
    /// </summary>
    public ApiSchema? BaseSchema { get; set; }

    public override string ToString() => Name ?? $"{Kind}";
}

/// <summary>A named property of an object schema.</summary>
public sealed class ApiProperty {
    public ApiProperty(string name, ApiSchema schema, bool required) {
        Name     = name;
        Schema   = schema;
        Required = required;
    }

    /// <summary>The property name as it appears on the wire (JSON property name).</summary>
    public string Name { get; }

    /// <summary>The property's schema.</summary>
    public ApiSchema Schema { get; }

    /// <summary>True when the property is listed in the schema's <c>required</c> array.</summary>
    public bool Required { get; set; }
}

/// <summary>
/// OpenAPI discriminator: the property whose value selects the concrete sub-type, and the mapping
/// from discriminator value to schema name.
/// </summary>
public sealed class DiscriminatorInfo {
    public DiscriminatorInfo(string propertyName, IReadOnlyDictionary<string, string> mapping) {
        PropertyName = propertyName;
        Mapping      = mapping;
    }

    /// <summary>The name of the property carrying the discriminator value.</summary>
    public string PropertyName { get; }

    /// <summary>Maps a discriminator value to the target schema name (component name).</summary>
    public IReadOnlyDictionary<string, string> Mapping { get; }
}
