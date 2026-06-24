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

namespace RestSharp.MigrationAssistant.Tool;

/// <summary>How much trust to place in an automatically applied transformation.</summary>
public enum Confidence {
    /// <summary>Proven byte-for-byte identical HTTP behaviour by the equivalence tests.</summary>
    High,

    /// <summary>Payload preserved, but a secondary aspect (a header value, threading) changes by design — review.</summary>
    Medium,

    /// <summary>No automatic fix; a human must perform the migration.</summary>
    Manual
}

/// <summary>Tool-level metadata for a migration rule: the confidence of its auto-fix and the guidance to print when a
/// usage cannot be fixed automatically.</summary>
public sealed record RuleInfo(string Id, Confidence Confidence, string ManualGuidance);

/// <summary>Confidence levels and manual-action guidance for each RSM rule, keyed by diagnostic id.</summary>
public static class RuleCatalog {
    static readonly Dictionary<string, RuleInfo> Rules = new RuleInfo[] {
        new("RSM001", Confidence.High,   "Rename the removed 'IRestResponse' interface to 'RestResponse'."),
        new("RSM002", Confidence.High,   "Rename the removed 'IRestRequest' interface to 'RestRequest'."),
        new("RSM003", Confidence.Manual, "'IHttp' was removed with no direct replacement. Rebuild the call using RestClient and RestRequest."),
        new("RSM004", Confidence.High,   "Replace AddParameter(..., ParameterType.RequestBody) with AddBody/AddStringBody. When the content type is not a string literal, confirm the intended body content type before applying."),
        new("RSM005", Confidence.High,   "Replace AddJsonBody(string) with AddStringBody(value, DataFormat.Json)."),
        new("RSM006", Confidence.Medium, "Remove the redundant Content-Type header; RestSharp sets it from the body. Confirm you did not intend a non-default content type."),
        new("RSM007", Confidence.Medium, "Remove the redundant Accept header; RestSharp sets it from the registered serializers. Confirm you did not intend a non-default Accept value."),
        new("RSM008", Confidence.Manual, "'NtlmAuthenticator' was removed. Configure NTLM via RestClientOptions.UseDefaultCredentials or RestClientOptions.Credentials."),
        new("RSM009", Confidence.Medium, "Convert the synchronous Execute call to 'await ExecuteAsync'. Make the enclosing method async (Task-returning) first, then re-run the tool to apply it.")
    }.ToDictionary(r => r.Id);

    public static RuleInfo For(string id)
        => Rules.TryGetValue(id, out var info) ? info : new RuleInfo(id, Confidence.Manual, "Review this usage manually.");

    public static IReadOnlyCollection<string> KnownIds => Rules.Keys;
}
