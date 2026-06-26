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

using System.Text;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Emit;

/// <summary>
/// Maps primitive schemas onto BCL types and turns wire names into valid CLR identifiers. Kept
/// separate from the emitter so the mapping rules are testable and reusable.
/// </summary>
/// <remarks>
/// Documented design choice: OpenAPI enums are surfaced as their underlying primitive type
/// (<c>string</c>/<c>int</c>) carrying allowed-value validation, rather than as generated CLR enums.
/// This guarantees lossless JSON serialization (the original casing/values survive) and avoids the
/// fragile enum-converter emission that a generated enum would require.
/// </remarks>
static class ClrTypeMapper {
    /// <summary>Maps a primitive (or enum) schema to a BCL type, applying nullability for value types.</summary>
    public static Type MapPrimitive(ApiSchema schema, bool nullableContext) {
        var baseType = schema.Primitive switch {
            PrimitiveType.Integer => schema.Format == "int64" ? typeof(long) : typeof(int),
            PrimitiveType.Number  => schema.Format == "float" ? typeof(float) : typeof(double),
            PrimitiveType.Boolean => typeof(bool),
            _                     => MapStringFormat(schema.Format)
        };

        if (!baseType.IsValueType) return baseType; // string, byte[] - already nullable references

        return nullableContext || schema.Nullable ? typeof(Nullable<>).MakeGenericType(baseType) : baseType;
    }

    static Type MapStringFormat(string? format)
        => format switch {
            "date-time" => typeof(DateTimeOffset),
            "date"      => typeof(DateTimeOffset),
            "uuid"      => typeof(Guid),
            "byte"      => typeof(byte[]),
            "binary"    => typeof(byte[]),
            _           => typeof(string)
        };

    /// <summary>
    /// Converts a wire name (which may contain dots, dashes, spaces, etc.) into a PascalCase CLR
    /// identifier. Always returns a non-empty identifier that starts with a letter or underscore.
    /// </summary>
    public static string ToPascalCase(string wireName) {
        var builder        = new StringBuilder(wireName.Length);
        var capitalizeNext = true;

        foreach (var ch in wireName) {
            if (char.IsLetterOrDigit(ch)) {
                builder.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                capitalizeNext = false;
            }
            else {
                capitalizeNext = true; // word boundary
            }
        }

        if (builder.Length == 0) return "Value";

        // An identifier may not start with a digit.
        if (char.IsDigit(builder[0])) builder.Insert(0, '_');

        return builder.ToString();
    }
}
