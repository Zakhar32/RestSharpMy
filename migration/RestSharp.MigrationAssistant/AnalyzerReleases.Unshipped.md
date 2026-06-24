; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/docs/Analyzer%20Configuration.md#analyzer-release-tracking

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------
RSM001  | RestSharp.Migration | Warning | IRestResponse was removed; use RestResponse
RSM002  | RestSharp.Migration | Warning | IRestRequest was removed; use RestRequest
RSM003  | RestSharp.Migration | Warning | IHttp was removed; use RestClient/RestRequest
RSM004  | RestSharp.Migration | Warning | AddParameter(..., ParameterType.RequestBody) -> AddBody
RSM005  | RestSharp.Migration | Warning | AddJsonBody(string) -> AddStringBody(str, DataFormat.Json)
RSM006  | RestSharp.Migration | Warning | Redundant Content-Type header
RSM007  | RestSharp.Migration | Warning | Redundant Accept header
RSM008  | RestSharp.Migration | Info    | NtlmAuthenticator was removed; use RestClientOptions
RSM009  | RestSharp.Migration | Warning | Synchronous Execute; prefer await ExecuteAsync
