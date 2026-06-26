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

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// Formats parameter values for transport on the URL/headers using invariant culture, so a client's
/// thread culture never leaks into the wire format. Mirrors the conventions RestSharp itself uses for
/// scalar parameters.
/// </summary>
static class ValueFormatter {
    public static string Format(object value)
        => value switch {
            string s          => s,
            bool b            => b ? "true" : "false",
            DateTime dt       => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dt => dt.ToString("o", CultureInfo.InvariantCulture),
            IFormattable f    => f.ToString(null, CultureInfo.InvariantCulture),
            _                 => value.ToString() ?? ""
        };

    /// <summary>True when the value is a collection that should expand into multiple query values.</summary>
    public static bool IsMultiValue(object value) => value is IEnumerable && value is not string;

    public static IEnumerable<string> FormatMany(object value) {
        foreach (var item in (IEnumerable)value) {
            if (item != null) yield return Format(item);
        }
    }
}
