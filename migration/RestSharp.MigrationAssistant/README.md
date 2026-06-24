# RestSharp.MigrationAssistant

Roslyn analyzers and code fixes that help migrate legacy RestSharp code (v106 and earlier) to the modern
RestSharp API introduced in v107.

Install it as a development-only dependency in the project you want to migrate:

```shell
dotnet add package RestSharp.MigrationAssistant
```

The analyzers light up in the IDE and on the command line. Build the project (or run `dotnet format analyzers`)
to see the diagnostics, and use the IDE's "Fix all occurrences" or `dotnet format` to apply the rewrites.

## Rules

| ID | Legacy API | Modern replacement | Auto-fix |
|----|------------|--------------------|----------|
| RSM001 | `IRestResponse` / `IRestResponse<T>` | `RestResponse` / `RestResponse<T>` | yes |
| RSM002 | `IRestRequest` | `RestRequest` | yes |
| RSM003 | `IHttp` | no direct replacement (use `RestClient`/`RestRequest`) | no |
| RSM004 | `AddParameter(name, value, ParameterType.RequestBody)` | `AddBody(value, name)` | yes (content-type literals) |
| RSM005 | `AddJsonBody("<string>")` | `AddStringBody(str, DataFormat.Json)` | yes |
| RSM006 | redundant `AddHeader("Content-Type", …)` | remove | yes |
| RSM007 | redundant `AddHeader("Accept", …)` | remove | yes |
| RSM008 | `new NtlmAuthenticator(...)` | `RestClientOptions.UseDefaultCredentials` / `Credentials` | no |
| RSM009 | synchronous `Execute` / `Execute<T>` | `await ExecuteAsync` / `await ExecuteAsync<T>` | yes (inside `async`) |

The body and header rewrites are validated by a behavioural-equivalence test suite that runs the legacy and the
migrated form against a live test server and asserts the outgoing HTTP request is byte-for-byte identical.

See the [full documentation](https://restsharp.dev/docs/migration-assistant) for details.
