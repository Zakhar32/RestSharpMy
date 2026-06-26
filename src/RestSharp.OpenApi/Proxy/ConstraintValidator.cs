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

using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// Validates a value against the constraints derived from its schema, before the request is sent.
/// This is fail-fast client-side validation: it turns a "the server will 400 this" into a clear
/// <see cref="OpenApiConstraintViolationException"/> at the call site.
/// </summary>
static class ConstraintValidator {
    public static void Validate(string parameterName, object? value, SchemaConstraints constraints, bool required) {
        if (value == null) {
            if (required) throw new OpenApiConstraintViolationException(parameterName, "a value is required but null was supplied.");
            return;
        }

        if (constraints.IsEmpty) return;

        switch (value) {
            case string s:
                ValidateString(parameterName, s, constraints);
                ValidateAllowedValues(parameterName, s, constraints);
                break;
            case IEnumerable enumerable when value is not string:
                ValidateCollection(parameterName, enumerable, constraints);
                break;
            default:
                if (TryToDouble(value, out var number)) ValidateNumber(parameterName, number, constraints);
                ValidateAllowedValues(parameterName, ValueFormatter.Format(value), constraints);
                break;
        }
    }

    static void ValidateString(string parameterName, string value, SchemaConstraints c) {
        if (c.MinLength is { } min && value.Length < min)
            throw new OpenApiConstraintViolationException(parameterName, $"length {value.Length} is shorter than the minimum of {min}.");

        if (c.MaxLength is { } max && value.Length > max)
            throw new OpenApiConstraintViolationException(parameterName, $"length {value.Length} exceeds the maximum of {max}.");

        if (c.Pattern != null && !Regex.IsMatch(value, c.Pattern))
            throw new OpenApiConstraintViolationException(parameterName, $"value '{value}' does not match the required pattern '{c.Pattern}'.");
    }

    static void ValidateNumber(string parameterName, double value, SchemaConstraints c) {
        if (c.Minimum is { } min) {
            if (c.ExclusiveMinimum && value <= min)
                throw new OpenApiConstraintViolationException(parameterName, $"value {Fmt(value)} must be greater than {Fmt(min)}.");
            if (!c.ExclusiveMinimum && value < min)
                throw new OpenApiConstraintViolationException(parameterName, $"value {Fmt(value)} is less than the minimum of {Fmt(min)}.");
        }

        if (c.Maximum is { } max) {
            if (c.ExclusiveMaximum && value >= max)
                throw new OpenApiConstraintViolationException(parameterName, $"value {Fmt(value)} must be less than {Fmt(max)}.");
            if (!c.ExclusiveMaximum && value > max)
                throw new OpenApiConstraintViolationException(parameterName, $"value {Fmt(value)} exceeds the maximum of {Fmt(max)}.");
        }

        if (c.MultipleOf is { } multiple && multiple != 0) {
            var ratio = value / multiple;
            if (Math.Abs(ratio - Math.Round(ratio)) > 1e-9)
                throw new OpenApiConstraintViolationException(parameterName, $"value {Fmt(value)} is not a multiple of {Fmt(multiple)}.");
        }
    }

    static void ValidateCollection(string parameterName, IEnumerable enumerable, SchemaConstraints c) {
        var items = enumerable.Cast<object?>().ToList();

        if (c.MinItems is { } min && items.Count < min)
            throw new OpenApiConstraintViolationException(parameterName, $"item count {items.Count} is below the minimum of {min}.");

        if (c.MaxItems is { } max && items.Count > max)
            throw new OpenApiConstraintViolationException(parameterName, $"item count {items.Count} exceeds the maximum of {max}.");

        if (c.UniqueItems && items.Count != items.Distinct().Count())
            throw new OpenApiConstraintViolationException(parameterName, "items must be unique.");
    }

    static void ValidateAllowedValues(string parameterName, string value, SchemaConstraints c) {
        if (c.AllowedValues == null) return;
        if (c.AllowedValues.Contains(value)) return;

        throw new OpenApiConstraintViolationException(
            parameterName,
            $"value '{value}' is not one of the allowed values: {string.Join(", ", c.AllowedValues)}."
        );
    }

    static bool TryToDouble(object value, out double result) {
        try {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) {
            result = 0;
            return false;
        }
    }

    static string Fmt(double value) => value.ToString("0.################", CultureInfo.InvariantCulture);
}
