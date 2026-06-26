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

/// <summary>
/// Binds an interface method to an OpenAPI operation by its <c>operationId</c>, overriding the
/// default name-based matching. Use this when the document's operation ids don't match your method
/// names, or to disambiguate.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RestOperationAttribute : Attribute {
    public RestOperationAttribute(string operationId) => OperationId = operationId;

    /// <summary>The <c>operationId</c> to bind to.</summary>
    public string OperationId { get; }
}

/// <summary>
/// Binds an interface method to an operation by HTTP method and path template, bypassing operation
/// id matching entirely. Useful for documents that omit <c>operationId</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RestMethodAttribute : Attribute {
    public RestMethodAttribute(Method method, string path) {
        HttpMethod = method;
        Path       = path;
    }

    public Method HttpMethod { get; }
    public string Path       { get; }
}

/// <summary>
/// Base for parameter-binding attributes. The default binder matches method parameters to operation
/// parameters by name; these attributes let you override the wire name or force a transport.
/// </summary>
public abstract class ParameterBindingAttribute : Attribute {
    protected ParameterBindingAttribute(string? name) => Name = name;

    /// <summary>The wire name to use, or null to use the method parameter name.</summary>
    public string? Name { get; }
}

/// <summary>Forces the parameter to be bound as a path segment with the given (or inferred) name.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PathParamAttribute : ParameterBindingAttribute {
    public PathParamAttribute(string? name = null) : base(name) { }
}

/// <summary>Forces the parameter to be bound as a query string parameter.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class QueryParamAttribute : ParameterBindingAttribute {
    public QueryParamAttribute(string? name = null) : base(name) { }
}

/// <summary>Forces the parameter to be bound as a request header.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class HeaderParamAttribute : ParameterBindingAttribute {
    public HeaderParamAttribute(string? name = null) : base(name) { }
}

/// <summary>Forces the parameter to be sent as a cookie.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CookieParamAttribute : ParameterBindingAttribute {
    public CookieParamAttribute(string? name = null) : base(name) { }
}

/// <summary>Forces the parameter to be serialized as the request body.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BodyAttribute : ParameterBindingAttribute {
    public BodyAttribute() : base(null) { }
}
