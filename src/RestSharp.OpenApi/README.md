# RestSharp.OpenApi

Runtime OpenAPI (Swagger) client generator for [RestSharp](https://restsharp.dev).

`RestSharp.OpenApi` parses an OpenAPI document at runtime, builds a semantic model of the API
and generates a strongly-typed proxy that implements an interface you define:

```csharp
using RestSharp;
using RestSharp.OpenApi;

public interface IPetStore {
    Task<Pet>       GetPetByIdAsync(long petId, CancellationToken ct = default);
    Task<Pet>       AddPetAsync(Pet body, CancellationToken ct = default);
    Task<List<Pet>> FindPetsByStatusAsync(string status, CancellationToken ct = default);
}

var client = new RestClient("https://petstore.example.com");
var api     = client.FromOpenApi<IPetStore>("petstore.swagger.json");

var pet = await api.GetPetByIdAsync(42);
```

The generated proxy runs through RestSharp's normal pipeline, so serialization, authentication
and interceptors configured on the `RestClient` all apply.

See the [documentation](https://restsharp.dev/docs/usage/openapi) for the full feature set,
extensibility hooks and documented limitations.
