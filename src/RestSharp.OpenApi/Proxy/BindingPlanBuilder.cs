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
using RestSharp.OpenApi.Emit;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// Builds the per-interface plan that maps each method to an <see cref="OperationBinding"/>. All the
/// reflection and matching work happens here, once, so that runtime invocation is cheap. Binding
/// errors are raised eagerly at build time (which is when the client is created), not on first call.
/// </summary>
static class BindingPlanBuilder {
    public static IReadOnlyDictionary<MethodInfo, OperationBinding> Build(
        Type                 interfaceType,
        OpenApiDocumentModel model,
        OpenApiClientOptions options,
        RuntimeTypeFactory   typeFactory
    ) {
        if (!interfaceType.IsInterface)
            throw new OpenApiBindingException($"'{interfaceType.FullName}' must be an interface to be generated from an OpenAPI document.");

        var plan = new Dictionary<MethodInfo, OperationBinding>();

        foreach (var method in GetBindableMethods(interfaceType)) {
            var operation = OperationResolver.Resolve(method, model, options);
            plan[method] = BuildBinding(method, operation, model, options, typeFactory);
        }

        return plan;
    }

    static IEnumerable<MethodInfo> GetBindableMethods(Type interfaceType) {
        var seen = new HashSet<MethodInfo>();

        foreach (var type in new[] { interfaceType }.Concat(interfaceType.GetInterfaces())) {
            foreach (var method in type.GetMethods()) {
                // Skip property/event accessors and IDisposable.Dispose (handled as a no-op at runtime).
                if (method.IsSpecialName) continue;
                if (IsDispose(method)) continue;
                if (seen.Add(method)) yield return method;
            }
        }
    }

    internal static bool IsDispose(MethodInfo method)
        => method is { Name: "Dispose", ReturnType: var rt } && rt == typeof(void) && method.GetParameters().Length == 0;

    static OperationBinding BuildBinding(
        MethodInfo           method,
        ApiOperation         operation,
        OpenApiDocumentModel model,
        OpenApiClientOptions options,
        RuntimeTypeFactory   typeFactory
    ) {
        var resource = BuildResource(model, operation, options);
        var binding  = new OperationBinding(method, operation, resource) {
            BodyContentType   = operation.RequestBody != null ? ChooseBodyContentType(operation, options) : null,
            AcceptContentType = options.SendAcceptHeader ? ChooseAcceptContentType(operation, options) : null
        };

        BindParameters(binding, method, operation);
        BuildReturnHandling(binding, method, operation, options, typeFactory);
        return binding;
    }

    // --- Parameter binding ---------------------------------------------------------------------

    static void BindParameters(OperationBinding binding, MethodInfo method, ApiOperation operation) {
        var parameters = method.GetParameters();
        var bodyBound  = false;

        for (var i = 0; i < parameters.Length; i++) {
            var parameter = parameters[i];

            if (parameter.ParameterType == typeof(CancellationToken)) {
                binding.CancellationTokenIndex = i;
                continue;
            }

            var attribute = parameter.GetCustomAttribute<ParameterBindingAttribute>();

            if (attribute is BodyAttribute || (attribute == null && !bodyBound && IsBodyCandidate(parameter, operation, out _))) {
                binding.Parameters.Add(new ParameterBinding(i, parameter.Name ?? "body", ParameterTarget.Body, operation.RequestBody?.Required ?? false, new SchemaConstraints()));
                binding.BodyContentType ??= "application/json";
                bodyBound = true;
                continue;
            }

            binding.Parameters.Add(BindNonBodyParameter(i, parameter, attribute, operation));
        }

        EnsureRequiredPathParametersBound(binding, operation, method);
    }

    static bool IsBodyCandidate(ParameterInfo parameter, ApiOperation operation, out ApiParameter? matched) {
        matched = FindOperationParameter(operation, parameter.Name);
        // A parameter with no name match becomes the body, but only when the operation declares one.
        return matched == null && operation.RequestBody != null;
    }

    static ParameterBinding BindNonBodyParameter(int index, ParameterInfo parameter, ParameterBindingAttribute? attribute, ApiOperation operation) {
        if (attribute != null) {
            var target   = TargetFromAttribute(attribute);
            var wireName = attribute.Name ?? parameter.Name!;
            var matched  = FindOperationParameter(operation, wireName) ?? FindOperationParameter(operation, parameter.Name);
            return new ParameterBinding(
                index,
                wireName,
                target,
                target == ParameterTarget.Path || (matched?.Required ?? false),
                matched?.Schema.Constraints ?? new SchemaConstraints()
            );
        }

        var apiParameter = FindOperationParameter(operation, parameter.Name)
            ?? throw new OpenApiBindingException(
                $"Parameter '{parameter.Name}' of '{operation.OperationId ?? operation.Path}' does not match any operation parameter. " +
                "Annotate it with [Query]/[Path]/[Header]/[Cookie]/[Body] to bind it explicitly.");

        return new ParameterBinding(
            index,
            apiParameter.Name,
            TargetFromLocation(apiParameter.Location),
            apiParameter.Required,
            apiParameter.Schema.Constraints
        );
    }

    static void EnsureRequiredPathParametersBound(OperationBinding binding, ApiOperation operation, MethodInfo method) {
        foreach (var pathParameter in operation.Parameters.Where(p => p.Location == ApiParameterLocation.Path)) {
            var bound = binding.Parameters.Any(p =>
                p.Target == ParameterTarget.Path && string.Equals(p.WireName, pathParameter.Name, StringComparison.OrdinalIgnoreCase));

            if (!bound)
                throw new OpenApiBindingException(
                    $"Operation '{operation.OperationId ?? operation.Path}' has required path parameter '{pathParameter.Name}' " +
                    $"that is not bound by any parameter of method '{method.DeclaringType?.Name}.{method.Name}'.");
        }
    }

    static ApiParameter? FindOperationParameter(ApiOperation operation, string? name)
        => name == null ? null : operation.Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    static ParameterTarget TargetFromAttribute(ParameterBindingAttribute attribute)
        => attribute switch {
            PathParamAttribute   => ParameterTarget.Path,
            QueryParamAttribute  => ParameterTarget.Query,
            HeaderParamAttribute => ParameterTarget.Header,
            CookieParamAttribute => ParameterTarget.Cookie,
            _                    => ParameterTarget.Query
        };

    static ParameterTarget TargetFromLocation(ApiParameterLocation location)
        => location switch {
            ApiParameterLocation.Path   => ParameterTarget.Path,
            ApiParameterLocation.Header => ParameterTarget.Header,
            ApiParameterLocation.Cookie => ParameterTarget.Cookie,
            _                           => ParameterTarget.Query
        };

    // --- Return handling -----------------------------------------------------------------------

    static void BuildReturnHandling(
        OperationBinding     binding,
        MethodInfo           method,
        ApiOperation         operation,
        OpenApiClientOptions options,
        RuntimeTypeFactory   typeFactory
    ) {
        var returnType   = method.ReturnType;
        var successType  = ResolveSuccessType(operation, options, typeFactory);

        if (returnType == typeof(Task)) {
            binding.IsAsync      = true;
            binding.ReturnsValue = false;
            binding.Execute      = ProxyExecutor.RawInvoker();
            return;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) {
            binding.IsAsync      = true;
            binding.ReturnsValue = true;
            ConfigureForDataType(binding, returnType.GetGenericArguments()[0], successType, options);
            return;
        }

        if (returnType == typeof(void)) {
            binding.IsAsync      = false;
            binding.ReturnsValue = false;
            binding.Execute      = ProxyExecutor.RawInvoker();
            return;
        }

        // Synchronous value-returning method.
        binding.IsAsync      = false;
        binding.ReturnsValue = true;
        var taskResultType   = ConfigureForDataType(binding, returnType, successType, options);
        binding.ResultProperty = taskResultType.GetProperty(nameof(Task<object>.Result));
    }

    /// <summary>
    /// Wires up the executor for the "payload" return shape and returns the runtime task type the
    /// executor produces (used to read <c>Result</c> on the synchronous path).
    /// </summary>
    static Type ConfigureForDataType(OperationBinding binding, Type declaredType, Type? successType, OpenApiClientOptions options) {
        if (declaredType == typeof(RestResponse)) {
            binding.Execute = ProxyExecutor.RawInvoker();
            return typeof(Task<RestResponse>);
        }

        if (declaredType.IsGenericType && declaredType.GetGenericTypeDefinition() == typeof(RestResponse<>)) {
            var dataType = declaredType.GetGenericArguments()[0];
            binding.Execute = ProxyExecutor.ResponseInvoker(dataType);
            return typeof(Task<>).MakeGenericType(declaredType);
        }

        if (declaredType == typeof(object) && options.UseRuntimeTypesForObjectReturns && successType != null && successType != typeof(object)) {
            binding.Execute = ProxyExecutor.ObjectInvoker(successType);
            return typeof(Task<object>);
        }

        binding.Execute = ProxyExecutor.DataInvoker(declaredType);
        return typeof(Task<>).MakeGenericType(declaredType);
    }

    static Type? ResolveSuccessType(ApiOperation operation, OpenApiClientOptions options, RuntimeTypeFactory typeFactory) {
        var response = operation.PrimarySuccessResponse;
        var schema   = response == null ? null : PickSchema(response.Content, options.PreferredResponseContentType);
        return schema == null ? null : typeFactory.ResolveType(schema);
    }

    // --- Content negotiation -------------------------------------------------------------------

    static string ChooseBodyContentType(ApiOperation operation, OpenApiClientOptions options) {
        var mediaTypes = operation.RequestBody!.Content.Select(c => c.MediaType).ToList();
        if (mediaTypes.Count == 0) return "application/json";

        var chosen = Negotiate(mediaTypes, options.PreferredRequestContentType);

        if (chosen == null)
            throw new OpenApiContentNegotiationException(
                $"Operation '{operation.OperationId ?? operation.Path}' declares only unsupported request media types " +
                $"({string.Join(", ", mediaTypes)}). Supported: JSON, XML, text/*, application/octet-stream. " +
                "Set OpenApiClientOptions.PreferredRequestContentType or correct the document.");

        return chosen;
    }

    static string? ChooseAcceptContentType(ApiOperation operation, OpenApiClientOptions options) {
        var response = operation.PrimarySuccessResponse;
        if (response == null || response.Content.Count == 0) return null;

        var mediaTypes = response.Content.Select(c => c.MediaType).ToList();
        return Negotiate(mediaTypes, options.PreferredResponseContentType) ?? mediaTypes[0];
    }

    /// <summary>Preference order: explicit preference, then JSON, then XML, then text, then binary.</summary>
    static string? Negotiate(List<string> mediaTypes, string? preferred) {
        if (preferred != null && mediaTypes.Any(m => SameMediaType(m, preferred))) return preferred;

        return mediaTypes.FirstOrDefault(m => m.Contains("json"))
            ?? mediaTypes.FirstOrDefault(m => m.Contains("xml"))
            ?? mediaTypes.FirstOrDefault(m => m.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            ?? mediaTypes.FirstOrDefault(m => m.Contains("octet-stream"));
    }

    static bool SameMediaType(string a, string b)
        => string.Equals(a.Split(';')[0].Trim(), b.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase);

    static ApiSchema? PickSchema(IReadOnlyList<ApiMediaType> content, string? preferred) {
        if (content.Count == 0) return null;

        var chosen = preferred != null ? content.FirstOrDefault(c => SameMediaType(c.MediaType, preferred)) : null;
        chosen ??= content.FirstOrDefault(c => c.MediaType.Contains("json"))
            ?? content.FirstOrDefault(c => c.MediaType.Contains("xml"))
            ?? content[0];

        return chosen.Schema;
    }

    static string BuildResource(OpenApiDocumentModel model, ApiOperation operation, OpenApiClientOptions options) {
        var path = operation.Path.Trim('/');
        if (!options.IncludeServerBasePath || string.IsNullOrEmpty(model.BasePath)) return path;

        var basePath = model.BasePath.Trim('/');
        return path.Length == 0 ? basePath : $"{basePath}/{path}";
    }
}
