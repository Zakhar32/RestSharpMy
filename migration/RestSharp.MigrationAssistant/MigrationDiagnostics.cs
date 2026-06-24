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
//

namespace RestSharp.MigrationAssistant;

/// <summary>
/// Diagnostic identifiers and descriptors shared by the RestSharp migration analyzers and code fixes.
/// The rules map directly to the breaking changes documented in the official migration guide
/// (https://restsharp.dev/docs/migration).
/// </summary>
public static class MigrationDiagnostics {
    public const string Category = "RestSharp.Migration";

    const string HelpUriBase = "https://restsharp.dev/docs/migration-assistant#";

    static DiagnosticDescriptor Rule(
        string             id,
        string             title,
        string             messageFormat,
        DiagnosticSeverity severity,
        string             description
    ) => new(
        id,
        title,
        messageFormat,
        Category,
        severity,
        isEnabledByDefault: true,
        description: description,
        helpLinkUri: HelpUriBase + id.ToLowerInvariant()
    );

    // RSM001 — IRestResponse / IRestResponse<T> were removed in v107.
    public static readonly DiagnosticDescriptor RestResponseInterface = Rule(
        "RSM001",
        "'IRestResponse' was removed",
        "'{0}' was removed in RestSharp v107; use '{1}' instead",
        DiagnosticSeverity.Warning,
        "The IRestResponse and IRestResponse<T> interfaces no longer exist. Execution methods return the concrete RestResponse / RestResponse<T> types."
    );

    // RSM002 — IRestRequest was removed in v107.
    public static readonly DiagnosticDescriptor RestRequestInterface = Rule(
        "RSM002",
        "'IRestRequest' was removed",
        "'IRestRequest' was removed in RestSharp v107; use 'RestRequest' instead",
        DiagnosticSeverity.Warning,
        "The IRestRequest interface no longer exists. Use the concrete RestRequest class."
    );

    // RSM003 — IHttp was removed in v107 with no direct replacement.
    public static readonly DiagnosticDescriptor HttpInterface = Rule(
        "RSM003",
        "'IHttp' was removed",
        "'IHttp' was removed in RestSharp v107 and has no direct replacement; use RestClient/RestRequest",
        DiagnosticSeverity.Warning,
        "The IHttp abstraction was removed. RestSharp now uses HttpClient internally; build requests with RestClient and RestRequest."
    );

    // RSM004 — AddParameter(..., ParameterType.RequestBody) should be a body method.
    public static readonly DiagnosticDescriptor RequestBodyParameter = Rule(
        "RSM004",
        "Use a body method instead of AddParameter with ParameterType.RequestBody",
        "Replace 'AddParameter(..., ParameterType.RequestBody)' with '{0}'",
        DiagnosticSeverity.Warning,
        "Adding a request body through AddParameter with ParameterType.RequestBody is fragile. Use AddJsonBody for serializable objects or AddBody/AddStringBody otherwise."
    );

    // RSM005 — AddJsonBody with a string argument should be AddStringBody.
    public static readonly DiagnosticDescriptor JsonBodyWithString = Rule(
        "RSM005",
        "AddJsonBody was called with a string",
        "'AddJsonBody' with a string serializes the string itself; use 'AddStringBody(..., ContentType.Json)' to send raw JSON",
        DiagnosticSeverity.Warning,
        "AddJsonBody is intended for serializable objects. Passing an already-serialized JSON string double-serializes it. Use AddStringBody with ContentType.Json instead."
    );

    // RSM006 — redundant Content-Type header.
    public static readonly DiagnosticDescriptor RedundantContentTypeHeader = Rule(
        "RSM006",
        "Redundant Content-Type header",
        "Remove the redundant '{0}(\"Content-Type\", ...)' call; RestSharp sets the content type from the body",
        DiagnosticSeverity.Warning,
        "Setting the Content-Type header manually is unnecessary and can be harmful. RestSharp sets it automatically based on the body type."
    );

    // RSM007 — redundant Accept header.
    public static readonly DiagnosticDescriptor RedundantAcceptHeader = Rule(
        "RSM007",
        "Redundant Accept header",
        "Remove the redundant '{0}(\"Accept\", ...)' call; RestSharp sets the Accept header from registered serializers",
        DiagnosticSeverity.Warning,
        "Setting the Accept header manually is unnecessary. RestSharp sets it automatically based on the registered serializers."
    );

    // RSM008 — NtlmAuthenticator was removed in v107.
    public static readonly DiagnosticDescriptor NtlmAuthenticator = Rule(
        "RSM008",
        "'NtlmAuthenticator' was removed",
        "'NtlmAuthenticator' was removed in RestSharp v107; set 'UseDefaultCredentials' or 'Credentials' on RestClientOptions instead",
        DiagnosticSeverity.Info,
        "NTLM authentication is configured through RestClientOptions.UseDefaultCredentials or RestClientOptions.Credentials, which feed the underlying HttpClientHandler."
    );

    // RSM009 — synchronous Execute family wraps the async API with a blocking call; prefer ExecuteAsync.
    public static readonly DiagnosticDescriptor SynchronousExecute = Rule(
        "RSM009",
        "Prefer the asynchronous Execute API",
        "'{0}' is a synchronous (sync-over-async) call; prefer 'await {1}'",
        DiagnosticSeverity.Warning,
        "RestSharp's synchronous Execute methods block on the async API via AsyncHelpers.RunSync, which can deadlock and scales poorly. The async ExecuteAsync family is the recommended way to make requests."
    );
}
