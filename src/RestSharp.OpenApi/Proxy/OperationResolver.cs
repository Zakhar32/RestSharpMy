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

using System.Reflection;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// Matches an interface method to an OpenAPI operation. Precedence, from highest to lowest:
/// <list type="number">
/// <item>a custom <see cref="OpenApiClientOptions.OperationResolver"/>;</item>
/// <item>an explicit <see cref="RestMethodAttribute"/> (HTTP method + path);</item>
/// <item>an explicit <see cref="RestOperationAttribute"/> (operationId);</item>
/// <item>the method name (optionally transformed by <see cref="OpenApiClientOptions.MethodNameToOperationId"/>,
/// otherwise stripped of a trailing <c>Async</c>) matched against <c>operationId</c>.</item>
/// </list>
/// </summary>
static class OperationResolver {
    public static ApiOperation Resolve(MethodInfo method, OpenApiDocumentModel model, OpenApiClientOptions options) {
        var resolved = options.OperationResolver?.Invoke(method, model);
        if (resolved != null) return resolved;

        var byMethod = method.GetCustomAttribute<RestMethodAttribute>();
        if (byMethod != null) {
            return model.FindOperation(byMethod.HttpMethod, byMethod.Path)
                ?? throw new OpenApiBindingException(
                    $"Method '{Describe(method)}' is annotated with [RestMethod({byMethod.HttpMethod}, \"{byMethod.Path}\")] " +
                    "but the document has no such operation.");
        }

        var byOperation = method.GetCustomAttribute<RestOperationAttribute>();
        if (byOperation != null) {
            return model.FindOperation(byOperation.OperationId)
                ?? throw new OpenApiBindingException(
                    $"Method '{Describe(method)}' is annotated with [RestOperation(\"{byOperation.OperationId}\")] " +
                    "but the document has no operation with that id.");
        }

        var candidate = options.MethodNameToOperationId?.Invoke(method.Name) ?? StripAsyncSuffix(method.Name);

        var operation = model.FindOperation(candidate) ?? model.FindOperation(method.Name);
        if (operation != null) return operation;

        throw new OpenApiBindingException(
            $"Could not bind method '{Describe(method)}' to an operation. Tried operationId '{candidate}'. " +
            "Annotate the method with [RestOperation(\"...\")] or [RestMethod(...)], or supply a custom OperationResolver.");
    }

    static string StripAsyncSuffix(string name)
        => name.EndsWith("Async", StringComparison.Ordinal) && name.Length > "Async".Length
            ? name.Substring(0, name.Length - "Async".Length)
            : name;

    static string Describe(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";
}
