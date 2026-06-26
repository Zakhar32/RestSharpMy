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
/// The shape of an <see cref="ApiSchema"/> in the semantic model. This is a normalised view of
/// the many ways OpenAPI lets you describe a schema, collapsed into the handful of cases the
/// type factory and the proxy actually need to reason about.
/// </summary>
public enum SchemaKind {
    /// <summary>A scalar value: string, integer, number or boolean.</summary>
    Primitive,

    /// <summary>An enumeration of scalar values.</summary>
    Enum,

    /// <summary>An object with named properties (possibly with a discriminator for polymorphism).</summary>
    Object,

    /// <summary>An array of items.</summary>
    Array,

    /// <summary>A free-form map (object with <c>additionalProperties</c> and no fixed properties).</summary>
    Map,

    /// <summary>
    /// A composite schema (<c>oneOf</c>/<c>anyOf</c>) that is satisfied by one of several
    /// sub-schemas. Polymorphism is represented here together with the discriminator.
    /// </summary>
    Composite,

    /// <summary>A schema with no usable type information; treated as <see cref="object"/>.</summary>
    Unknown
}

/// <summary>
/// The primitive JSON type backing a <see cref="SchemaKind.Primitive"/> or
/// <see cref="SchemaKind.Enum"/> schema.
/// </summary>
public enum PrimitiveType {
    String,
    Integer,
    Number,
    Boolean
}

/// <summary>
/// Where a request parameter is transported. Mirrors the OpenAPI <c>in</c> field.
/// </summary>
public enum ApiParameterLocation {
    Path,
    Query,
    Header,
    Cookie
}
