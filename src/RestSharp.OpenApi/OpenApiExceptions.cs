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

namespace RestSharp.OpenApi;

/// <summary>Base type for all errors raised by <c>RestSharp.OpenApi</c>.</summary>
public class OpenApiException : Exception {
    public OpenApiException(string message) : base(message) { }
    public OpenApiException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when an OpenAPI document cannot be read or is structurally invalid.</summary>
public sealed class OpenApiParseException : OpenApiException {
    public OpenApiParseException(string message) : base(message) { }
    public OpenApiParseException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when an interface method cannot be bound to an operation, or the binding is ambiguous
/// or incomplete (e.g. a required path parameter has no matching method parameter).
/// </summary>
public sealed class OpenApiBindingException : OpenApiException {
    public OpenApiBindingException(string message) : base(message) { }
}

/// <summary>Thrown when an argument fails the constraint validation derived from the schema.</summary>
public sealed class OpenApiConstraintViolationException : OpenApiException {
    public OpenApiConstraintViolationException(string parameterName, string message)
        : base($"Constraint violation for '{parameterName}': {message}") => ParameterName = parameterName;

    /// <summary>The name of the parameter (or property) that failed validation.</summary>
    public string ParameterName { get; }
}

/// <summary>Thrown when no acceptable media type can be negotiated for a request body or response.</summary>
public sealed class OpenApiContentNegotiationException : OpenApiException {
    public OpenApiContentNegotiationException(string message) : base(message) { }
}

/// <summary>Thrown when runtime type generation cannot produce a CLR type for a schema.</summary>
public sealed class OpenApiTypeGenerationException : OpenApiException {
    public OpenApiTypeGenerationException(string message) : base(message) { }
    public OpenApiTypeGenerationException(string message, Exception inner) : base(message, inner) { }
}
