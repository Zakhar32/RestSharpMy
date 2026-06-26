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

namespace RestSharp.OpenApi;

/// <summary>
/// Passed to <see cref="OpenApiClientOptions.ConfigureRequest"/> just before a generated call is
/// executed. Lets a developer inspect or mutate the <see cref="RestRequest"/> that the binder built
/// - the primary hook for overriding generated request behaviour without abandoning the generator.
/// </summary>
public sealed class OpenApiRequestContext {
    internal OpenApiRequestContext(RestRequest request, ApiOperation operation, MethodInfo method, object?[] arguments) {
        Request   = request;
        Operation = operation;
        Method    = method;
        Arguments = arguments;
    }

    /// <summary>The request built from the operation and arguments. Mutate it to change what is sent.</summary>
    public RestRequest Request { get; }

    /// <summary>The operation this call is bound to.</summary>
    public ApiOperation Operation { get; }

    /// <summary>The interface method being invoked.</summary>
    public MethodInfo Method { get; }

    /// <summary>The arguments passed to the method (the cancellation token, if any, is included).</summary>
    public object?[] Arguments { get; }
}
