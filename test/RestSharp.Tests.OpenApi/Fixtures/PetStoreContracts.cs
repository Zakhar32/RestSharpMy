using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RestSharp.Tests.OpenApi.Fixtures;

// User-defined DTOs for the strongly-typed proxy tests. These are ordinary POCOs - the generator
// uses them directly and lets the client's serializer handle them.

public class Pet {
    public long         Id      { get; set; }
    public string       Name    { get; set; }
    [JsonPropertyName("petType")]
    public string       PetType { get; set; }
    public List<string> Tags    { get; set; }
}

// The user-defined interface that gets implemented at runtime.
public interface IPetStoreApi {
    Task<Pet>           GetPetByIdAsync(long petId, CancellationToken ct = default);
    Task<List<Pet>>     ListPetsAsync(int? limit = null, string status = null, CancellationToken ct = default);
    Task<Pet>           CreatePetAsync(Pet body, CancellationToken ct = default);
    Task<RestResponse>  DeletePetAsync(long petId, CancellationToken ct = default);
}

// Interface used for the no-operationId document, relying on explicit attributes.
public interface IThingsApi {
    [RestMethod(Method.Get, "things/{id}")]
    Task<object> GetThingAsync([PathParam] string id, CancellationToken ct = default);
}
