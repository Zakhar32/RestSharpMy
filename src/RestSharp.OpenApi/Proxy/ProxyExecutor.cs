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
using RestSharp.Serializers;

namespace RestSharp.OpenApi.Proxy;

/// <summary>
/// The bridge from a generated proxy method to the RestSharp execution pipeline. Every path goes
/// through <see cref="IRestClient.ExecuteAsync(RestRequest, CancellationToken)"/> (or its typed
/// extension), so client-level serializers, authenticators and interceptors all apply unchanged.
/// </summary>
/// <remarks>
/// The public entry points return <see cref="object"/> (not <c>Task&lt;T&gt;</c>) so they can be turned
/// into a single <c>Func&lt;IRestClient, RestRequest, CancellationToken, object&gt;</c> delegate via
/// <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/> with an exact signature match - the boxed
/// value is the actual <c>Task&lt;T&gt;</c> the caller awaits.
/// </remarks>
static class ProxyExecutor {
    static readonly MethodInfo DataMethod     = typeof(ProxyExecutor).GetMethod(nameof(ExecuteData), BindingFlags.Static | BindingFlags.Public)!;
    static readonly MethodInfo ResponseMethod = typeof(ProxyExecutor).GetMethod(nameof(ExecuteResponse), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo DeserializeContentMethod =
        typeof(RestSerializers).GetMethod(nameof(RestSerializers.DeserializeContent))!;

    // --- Generic entry points (closed over the data type via reflection) -----------------------

    public static object ExecuteData<T>(IRestClient client, RestRequest request, CancellationToken ct) => ExecuteDataCore<T>(client, request, ct);

    static async Task<T?> ExecuteDataCore<T>(IRestClient client, RestRequest request, CancellationToken ct) {
        var response = await client.ExecuteAsync<T>(request, ct).ConfigureAwait(false);
        return response.Data;
    }

    public static object ExecuteResponse<T>(IRestClient client, RestRequest request, CancellationToken ct) => ExecuteResponseCore<T>(client, request, ct);

    static async Task<RestResponse<T>> ExecuteResponseCore<T>(IRestClient client, RestRequest request, CancellationToken ct)
        => await client.ExecuteAsync<T>(request, ct).ConfigureAwait(false);

    public static object ExecuteRaw(IRestClient client, RestRequest request, CancellationToken ct) => client.ExecuteAsync(request, ct);

    static async Task<object?> ExecuteObjectCore(IRestClient client, RestRequest request, Type dataType, CancellationToken ct) {
        var response = await client.ExecuteAsync(request, ct).ConfigureAwait(false);
        if (response.Content == null) return null;

        // Deserialize into a runtime-generated type using the client's own serializers.
        var deserialize = DeserializeContentMethod.MakeGenericMethod(dataType);
        return deserialize.Invoke(client.Serializers, new object[] { response });
    }

    // --- Invoker builders ----------------------------------------------------------------------

    public static Func<IRestClient, RestRequest, CancellationToken, object> DataInvoker(Type dataType)
        => CreateInvoker(DataMethod.MakeGenericMethod(dataType));

    public static Func<IRestClient, RestRequest, CancellationToken, object> ResponseInvoker(Type dataType)
        => CreateInvoker(ResponseMethod.MakeGenericMethod(dataType));

    public static Func<IRestClient, RestRequest, CancellationToken, object> RawInvoker()
        => CreateInvoker(typeof(ProxyExecutor).GetMethod(nameof(ExecuteRaw), BindingFlags.Static | BindingFlags.Public)!);

    public static Func<IRestClient, RestRequest, CancellationToken, object> ObjectInvoker(Type dataType)
        => (client, request, ct) => ExecuteObjectCore(client, request, dataType, ct);

    static Func<IRestClient, RestRequest, CancellationToken, object> CreateInvoker(MethodInfo method)
        => (Func<IRestClient, RestRequest, CancellationToken, object>)
            Delegate.CreateDelegate(typeof(Func<IRestClient, RestRequest, CancellationToken, object>), method);
}
