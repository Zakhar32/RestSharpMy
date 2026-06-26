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

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// The <see cref="DispatchProxy"/> that implements a user interface at runtime. It owns no transport
/// of its own: it builds a <see cref="RestRequest"/> from the precomputed binding and the call
/// arguments, then hands it to the supplied <see cref="IRestClient"/>, so serialization,
/// authentication and interceptors are exactly the ones configured on that client.
/// </summary>
/// <remarks>
/// A single <see cref="ApiDispatchProxy"/> instance is created per generated client. It holds only
/// immutable references (the client, the shared binding plan and the options), so it is safe to share
/// across threads - <see cref="DispatchProxy"/>'s own generated type and the plan are read-only after
/// construction.
/// </remarks>
public class ApiDispatchProxy : DispatchProxy {
    IRestClient                                       _client      = null!;
    IReadOnlyDictionary<MethodInfo, OperationBinding> _plan        = null!;
    OpenApiClientOptions                              _options     = null!;

    internal void Initialize(IRestClient client, IReadOnlyDictionary<MethodInfo, OperationBinding> plan, OpenApiClientOptions options) {
        _client  = client;
        _plan    = plan;
        _options = options;
    }

    internal static T Create<T>(IRestClient client, IReadOnlyDictionary<MethodInfo, OperationBinding> plan, OpenApiClientOptions options)
        where T : class {
        var proxy = DispatchProxy.Create<T, ApiDispatchProxy>();
        ((ApiDispatchProxy)(object)proxy).Initialize(client, plan, options);
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        if (targetMethod == null) throw new ArgumentNullException(nameof(targetMethod));

        if (!_plan.TryGetValue(targetMethod, out var binding)) {
            // IDisposable.Dispose and anything else we deliberately didn't bind is a no-op.
            if (BindingPlanBuilder.IsDispose(targetMethod)) return null;
            throw new OpenApiBindingException($"Method '{targetMethod.Name}' was not bound to an operation.");
        }

        var arguments = args ?? Array.Empty<object?>();
        var request   = BuildRequest(binding, arguments);

        var cancellationToken = binding.CancellationTokenIndex >= 0 && arguments[binding.CancellationTokenIndex] is CancellationToken token
            ? token
            : CancellationToken.None;

        _options.ConfigureRequest?.Invoke(new OpenApiRequestContext(request, binding.Operation, targetMethod, arguments));

        var taskObject = binding.Execute(_client, request, cancellationToken);

        if (binding.IsAsync) return taskObject;

        // Synchronous method: block on the produced task and surface its result (if any).
        var task = (Task)taskObject;
        task.GetAwaiter().GetResult();
        return binding.ReturnsValue ? binding.ResultProperty?.GetValue(task) : null;
    }

    RestRequest BuildRequest(OperationBinding binding, object?[] arguments) {
        var request = new RestRequest(binding.Resource, binding.Operation.Method);

        foreach (var parameter in binding.Parameters) {
            var value = parameter.ArgIndex < arguments.Length ? arguments[parameter.ArgIndex] : null;

            if (_options.ValidateConstraints) {
                var name = parameter.Target == ParameterTarget.Body ? "body" : parameter.WireName;
                ConstraintValidator.Validate(name, value, parameter.Constraints, parameter.Required);
            }

            if (value == null) continue; // optional, omitted (required-null already threw above when validating)

            ApplyParameter(request, parameter, value, binding);
        }

        if (_options.SendAcceptHeader && binding.AcceptContentType != null) request.AddOrUpdateHeader(KnownHeaders.Accept, binding.AcceptContentType);

        return request;
    }

    static void ApplyParameter(RestRequest request, ParameterBinding parameter, object value, OperationBinding binding) {
        switch (parameter.Target) {
            case ParameterTarget.Path:
                request.AddUrlSegment(parameter.WireName, ValueFormatter.Format(value));
                break;
            case ParameterTarget.Query:
                if (ValueFormatter.IsMultiValue(value)) {
                    foreach (var item in ValueFormatter.FormatMany(value)) request.AddQueryParameter(parameter.WireName, item);
                }
                else {
                    request.AddQueryParameter(parameter.WireName, ValueFormatter.Format(value));
                }

                break;
            case ParameterTarget.Header:
                request.AddHeader(parameter.WireName, ValueFormatter.Format(value));
                break;
            case ParameterTarget.Cookie:
                request.AddCookie(parameter.WireName, ValueFormatter.Format(value));
                break;
            case ParameterTarget.Body:
                ApplyBody(request, value, binding.BodyContentType ?? "application/json");
                break;
        }
    }

    static void ApplyBody(RestRequest request, object value, string contentType) {
        ContentType ct = contentType;

        if (value is byte[] bytes) {
            request.AddBody(bytes, ct);
            return;
        }

        if (contentType.Contains("json")) {
            request.AddJsonBody(value, ct);
            return;
        }

        if (contentType.Contains("xml")) {
            request.AddXmlBody(value, ct);
            return;
        }

        request.AddStringBody(value as string ?? ValueFormatter.Format(value), ct);
    }
}
