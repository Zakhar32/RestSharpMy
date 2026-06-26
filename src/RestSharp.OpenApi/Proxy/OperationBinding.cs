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

/// <summary>Where an argument is transported in the generated request.</summary>
enum ParameterTarget {
    Path,
    Query,
    Header,
    Cookie,
    Body
}

/// <summary>Maps a single method argument onto a request element. Precomputed once per method.</summary>
sealed class ParameterBinding {
    public ParameterBinding(int argIndex, string wireName, ParameterTarget target, bool required, SchemaConstraints constraints) {
        ArgIndex    = argIndex;
        WireName    = wireName;
        Target      = target;
        Required    = required;
        Constraints = constraints;
    }

    public int               ArgIndex    { get; }
    public string            WireName    { get; }
    public ParameterTarget   Target      { get; }
    public bool              Required    { get; }
    public SchemaConstraints Constraints { get; }
}

/// <summary>
/// The fully resolved, immutable plan for invoking one interface method: how to build the request
/// and how to shape the response back into the method's declared return type. Built once and cached;
/// the proxy executes it on every call without touching reflection on the hot path (beyond the
/// optional sync <c>Result</c> read).
/// </summary>
sealed class OperationBinding {
    public OperationBinding(MethodInfo method, ApiOperation operation, string resource) {
        Method    = method;
        Operation = operation;
        Resource  = resource;
    }

    public MethodInfo   Method    { get; }
    public ApiOperation Operation { get; }

    /// <summary>The resource template (with <c>{placeholders}</c>) relative to the client base URL.</summary>
    public string Resource { get; }

    public List<ParameterBinding> Parameters { get; } = new();

    /// <summary>The negotiated request body media type, or null when the operation takes no body.</summary>
    public string? BodyContentType { get; set; }

    /// <summary>The negotiated <c>Accept</c> media type, or null.</summary>
    public string? AcceptContentType { get; set; }

    /// <summary>Index of the <see cref="CancellationToken"/> argument, or -1.</summary>
    public int CancellationTokenIndex { get; set; } = -1;

    /// <summary>Executes the request and returns the boxed <see cref="Task"/> to await.</summary>
    public Func<IRestClient, RestRequest, CancellationToken, object> Execute { get; set; } = null!;

    /// <summary>True when the interface method returns a <see cref="Task"/> / <c>Task&lt;T&gt;</c>.</summary>
    public bool IsAsync { get; set; }

    /// <summary>True when the method returns a value (so the proxy must surface the awaited result).</summary>
    public bool ReturnsValue { get; set; }

    /// <summary>The <c>Result</c> property of the produced task, used only on the synchronous path.</summary>
    public PropertyInfo? ResultProperty { get; set; }
}
