---
title: Migration Assistant (Roslyn analyzers)
---

## What it is

`RestSharp.MigrationAssistant` is a set of [Roslyn](https://github.com/dotnet/roslyn) diagnostic analyzers and code
fixes that find legacy RestSharp usages (v106 and earlier) in your code and rewrite them to the modern v107+ API. It
encodes the rules from the [migration guide](/migration) so the upgrade can be automated and reviewed in your IDE or on
the command line.

It is a development-only package: it ships analyzers, not runtime code, and is versioned independently from RestSharp
itself (it starts at `1.0.0`).

## Installation

Add the package to the project you want to migrate:

```shell
dotnet add package RestSharp.MigrationAssistant
```

Because it is an analyzer, you typically reference it with `PrivateAssets="all"` so it never flows to your own
package's consumers:

```xml
<PackageReference Include="RestSharp.MigrationAssistant" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID | Legacy API | Modern replacement | Auto-fix |
|----|------------|--------------------|----------|
| `RSM001` | `IRestResponse` / `IRestResponse<T>` | `RestResponse` / `RestResponse<T>` | ✅ |
| `RSM002` | `IRestRequest` | `RestRequest` | ✅ |
| `RSM003` | `IHttp` | no direct replacement (use `RestClient`/`RestRequest`) | — |
| `RSM004` | `AddParameter(name, value, ParameterType.RequestBody)` | `AddBody(value, name)` | ✅ (content-type literals) |
| `RSM005` | `AddJsonBody("<string>")` | `AddStringBody(str, DataFormat.Json)` | ✅ |
| `RSM006` | redundant `AddHeader("Content-Type", …)` | remove the call | ✅ |
| `RSM007` | redundant `AddHeader("Accept", …)` | remove the call | ✅ |
| `RSM008` | `new NtlmAuthenticator(...)` | `RestClientOptions.UseDefaultCredentials` / `Credentials` | — |
| `RSM009` | synchronous `Execute` / `Execute<T>` | `await ExecuteAsync` / `await ExecuteAsync<T>` | ✅ (inside `async`) |

### Examples

```csharp
// RSM001 / RSM002
IRestResponse Send(IRestRequest request) => _client.Execute(request);
// becomes
RestResponse Send(RestRequest request) => _client.Execute(request);

// RSM004 — the content type is preserved as the AddBody content type
request.AddParameter("application/json", json, ParameterType.RequestBody);
// becomes
request.AddBody(json, "application/json");

// RSM005 — AddJsonBody is for objects, not pre-serialized strings
request.AddJsonBody(jsonString);
// becomes
request.AddStringBody(jsonString, DataFormat.Json);

// RSM006 / RSM007 — RestSharp sets these automatically
request.AddHeader("Content-Type", "application/json");   // removed
request.AddHeader("Accept", "application/json");          // removed

// RSM009 — the synchronous Execute blocks on the async API; inside an async method:
var response = client.Execute(request);
// becomes
var response = await client.ExecuteAsync(request);
```

## Applying the fixes

The analyzers light up in the IDE (Visual Studio, Rider, VS Code with C# Dev Kit) with lightbulb fixes, including
**Fix all occurrences in Document / Project / Solution**.

From the command line, run the formatter to apply the code fixes in bulk:

```shell
dotnet format analyzers --diagnostics RSM001 RSM002 RSM004 RSM005 RSM006 RSM007
```

## Behavioural-equivalence guarantee

The body and header rewrites are backed by a behavioural-equivalence test suite: each rule's legacy form and migrated
form are executed against a live test server, and the outgoing HTTP request is compared. Coverage spans **JSON, XML and
multipart/form-data** payloads — the body rules are checked for both JSON and XML content types, and the header rules
are additionally checked against a multipart request (comparing the parts after normalising the random boundary). This
is what each rule guarantees:

- **RSM004** (`AddParameter(contentType, value, ParameterType.RequestBody)` → `AddBody(value, contentType)`) and
  **RSM005** (`AddJsonBody(string)` → `AddStringBody(str, DataFormat.Json)`) are **byte-for-byte identical** — same
  method, URL, body and content type. RSM004 is auto-fixed **only** when the parameter name is a string-literal content
  type (it contains `/`, e.g. `"application/json"`); other shapes are flagged for manual review because no single
  rewrite is provably equivalent.
- **RSM006** keeps the body and resource identical and still sends a JSON content type. Note that RestSharp normalises
  the header to `application/json; charset=utf-8`, so removing a hand-written `application/json` header is the *intended*
  behaviour — the manual header was redundant and, by dropping the charset, slightly wrong.
- **RSM007** leaves the method, URL, body and content type unchanged. The explicit `Accept` value is replaced by
  RestSharp's serializer-derived `Accept`; both still negotiate JSON. If you depend on a non-default `Accept` value,
  review this change before applying it.

- **RSM009** (synchronous `Execute` → `await ExecuteAsync`) is proven equivalent: the synchronous overloads are blocking
  wrappers over `ExecuteAsync` (`AsyncHelpers.RunSync`), so both return the same response. Note that synchronous
  `Execute` was *not* removed (it returns `RestResponse` instead of the old `IRestResponse`); this rule is a
  recommendation to adopt the async API the migration guide promotes, not a fix for a compile break. The auto-fix is
  offered **only inside an `async` method, local function or lambda**, where `await` compiles. Elsewhere the diagnostic
  is reported without a fix, because making the enclosing member (and its callers) async is a refactor you should drive.

`RSM003` and `RSM008` have no automatic fix because the replacement requires moving configuration (to `RestClient`
/`RestRequest` and to `RestClientOptions` respectively), which should be done by hand.

## Roadmap

The rule set is intentionally focused and extensible. Planned follow-ups:

- A standalone CLI migrator (built on `MSBuildWorkspace`) for solution-wide, non-interactive migration. Until then,
  `dotnet format analyzers` and the IDE "Fix all" command cover bulk application.
- Additional rules, e.g. legacy `RestSharp` XML serializers → `RestSharp.Serializers.Xml`, and the synchronous
  `Get`/`Post`/`Delete` wrappers (RSM009 currently covers the `Execute` family).
