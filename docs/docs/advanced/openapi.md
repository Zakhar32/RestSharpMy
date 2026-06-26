---
title: OpenAPI client generator
description: Generate strongly-typed API clients from an OpenAPI (Swagger) document at runtime.
sidebar_position: 6
---

# Runtime OpenAPI client generator

The `RestSharp.OpenApi` package turns RestSharp from a manual HTTP client into a strongly-typed API
gateway. It parses an OpenAPI (Swagger) document **at runtime**, builds a semantic model of the API,
generates the CLR types and a proxy implementation, and exposes everything through your own interface:

```csharp
using RestSharp;
using RestSharp.OpenApi;

public interface IPetStore {
    Task<Pet>       GetPetByIdAsync(long petId, CancellationToken ct = default);
    Task<List<Pet>> ListPetsAsync(int? limit = null, string status = null, CancellationToken ct = default);
    Task<Pet>       CreatePetAsync(Pet body, CancellationToken ct = default);
    Task<RestResponse> DeletePetAsync(long petId, CancellationToken ct = default);
}

var client = new RestClient("https://petstore.example.com");
var api     = client.FromOpenApi<IPetStore>("petstore.swagger.json");

var pet  = await api.GetPetByIdAsync(42);
var pets = await api.ListPetsAsync(limit: 10, status: "available");
```

The generated proxy executes through the supplied `RestClient`, so the serializers, authenticators
and interceptors you configure on the client are reused unchanged. Nothing about your existing setup
needs to change.

## How it works

`FromOpenApi<T>` runs a small pipeline, each stage cached so the work happens once per process:

1. **Read** – the document text is parsed into JSON by an `IOpenApiDocumentReader` (JSON by default).
2. **Model** – an `OpenApiDocumentModel` is built: operations, parameters (with their constraints and
   location), request bodies, responses, content types and component schemas including polymorphism
   (`allOf`/`oneOf`/`anyOf` and discriminators). `$ref`s are resolved into shared nodes.
3. **Generate types** – named object schemas are materialised into real CLR types via
   `System.Reflection.Emit`. `allOf` composition becomes inheritance; a discriminator becomes a
   `System.Text.Json` polymorphic hierarchy.
4. **Bind** – each interface method is matched to an operation and a binding plan is precomputed:
   which argument goes to the path/query/header/cookie/body, how the body and `Accept` content types
   are negotiated, and how the response maps to the method's return type.
5. **Proxy** – a `DispatchProxy` implements your interface using the binding plan.

You can also work with the model and the generated types directly, without creating a proxy:

```csharp
var document = client.LoadOpenApi("petstore.swagger.json"); // or OpenApiDocument.Load(...)
OpenApiDocumentModel model = document.Model;
Type petType               = document.GetGeneratedType("Pet");
```

## Method and parameter binding

Methods are matched to operations in this order:

1. a custom `OperationResolver` (see below);
2. `[RestMethod(Method.Get, "pets/{id}")]` – explicit HTTP method + path;
3. `[RestOperation("getPetById")]` – explicit `operationId`;
4. the method name, with a trailing `Async` stripped, matched against `operationId` (case-insensitive).

Parameters are matched to the operation's parameters **by name**. A parameter that matches nothing and
an operation that declares a request body is treated as the body. You can always be explicit:

```csharp
Task<Pet> GetAsync(
    [PathParam("petId")] long id,
    [QueryParam] string status,
    [HeaderParam("X-Trace-Id")] string trace,
    [Body] Pet pet,
    CancellationToken ct = default);
```

### Return types

| Method returns          | Behaviour                                                      |
|-------------------------|---------------------------------------------------------------|
| `Task<T>` / `T`         | Deserialize the response body into `T`.                       |
| `Task<RestResponse<T>>` | Return the full typed response (status, headers, data).       |
| `Task<RestResponse>`    | Return the raw response without deserializing.                |
| `Task` / `void`         | Execute and ignore the body.                                  |
| `Task<object>`          | Deserialize into the runtime-generated type for the response. |

Synchronous methods are supported but block on the async pipeline; prefer the `Task`-returning forms.

## Validation

Parameter values are validated against the schema constraints (`minimum`/`maximum`, `minLength`/
`maxLength`, `pattern`, `enum`, `minItems`/`maxItems`, `uniqueItems`, `required`) **before** the request
is sent, raising an `OpenApiConstraintViolationException` at the call site. Disable with
`options.ValidateConstraints = false`.

## Content negotiation

For a request body, the media type is chosen from the operation's declared types using this
precedence: your `PreferredRequestContentType`, then JSON, then XML, then `text/*`, then
`application/octet-stream`. The response `Accept` header is negotiated the same way (controlled by
`SendAcceptHeader` / `PreferredResponseContentType`). If an operation declares only media types the
package can't handle, an `OpenApiContentNegotiationException` is raised at creation time.

## Extensibility

Everything is configured through `OpenApiClientOptions`:

```csharp
var api = client.FromOpenApi<IPetStore>("petstore.swagger.json", options => {
    // Fix an imperfect document at runtime (runs on the parsed model).
    options.DocumentTransformer = model => {
        var limit = model.FindOperation("listPets").Parameters.First(p => p.Name == "limit");
        limit.Schema.Constraints.Maximum = 50;
    };

    // Custom method -> operation matching.
    options.OperationResolver = (method, model) => /* ... */ null;

    // Override the request just before it executes.
    options.ConfigureRequest = ctx => ctx.Request.AddOrUpdateHeader("X-Api-Version", "2");

    // Support YAML or another format.
    options.DocumentReader = new MyYamlReader();
});
```

Other hooks include `MethodNameToOperationId` (naming policy), `IncludeServerBasePath`,
`PreferredRequestContentType` / `PreferredResponseContentType`, `UseRuntimeTypesForObjectReturns`,
`RuntimeTypeNamespace`, and the caching controls `DisableCache` / `CacheKey`.

## Caching, performance and thread-safety

Parsing, type generation and per-interface binding are the expensive, one-time steps. They are cached
process-wide, keyed by a hash of the document content (plus the identity of any transformer/resolver),
using `ConcurrentDictionary` of `Lazy<T>` so each is built exactly once even under concurrent access.
Repeated `FromOpenApi` calls for the same document and interface are O(1) lookups, and a single
`DispatchProxy` instance is safe to share across threads. Call `OpenApiDocument.ClearCache()` to reset
the caches (useful in tests).

## Documented limitations and tradeoffs

These are deliberate scope decisions, called out so there are no surprises:

- **JSON only out of the box.** The default reader parses JSON. For YAML, supply an
  `IOpenApiDocumentReader` that converts YAML to a `JsonDocument`; this keeps the package free of a
  YAML dependency.
- **No cross-process cache.** A `DispatchProxy` type can't be serialized, so the cache is in-process.
  "Avoid regeneration on every startup" means O(1) reuse within a long-lived host. Persist the document
  text yourself (it hashes identically) to skip re-downloading.
- **Enums are surfaced as their primitive type** (string/int) with allowed-value validation rather than
  generated CLR enums, to guarantee lossless JSON round-tripping.
- **Inline (anonymous) and `oneOf`/`anyOf` schemas become `object`.** Only named component schemas
  become concrete classes. The semantic model still captures the full composition.
- **Polymorphic deserialization follows System.Text.Json rules.** The discriminator must be the first
  JSON property on .NET 8; on .NET 9+ you can opt into out-of-order discriminators via
  `JsonSerializerOptions.AllowOutOfOrderMetadataProperties`. If your server emits it out of order on
  .NET 8, deserialize into the concrete type or use a custom serializer.
- **Server base path.** By default the document's `servers` base path is prepended to operation paths
  (OpenAPI semantics). Set `IncludeServerBasePath = false` if your `RestClient` base URL already
  includes it.
- **`trace` operations are skipped** since RestSharp's `Method` enum has no equivalent.
- **Constraint violations throw synchronously** at the call site (they are argument errors), not as a
  faulted task.
