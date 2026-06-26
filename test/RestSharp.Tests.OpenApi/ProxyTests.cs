using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using RestSharp.Tests.OpenApi.Fixtures;
using WireMock.Matchers;

namespace RestSharp.Tests.OpenApi;

public sealed class ProxyTests : IDisposable {
    readonly WireMockServer _server;
    readonly RestClient     _client;
    readonly IPetStoreApi   _api;

    public ProxyTests() {
        _server = WireMockServer.Start();
        _client = new RestClient(_server.Url!);
        _api    = _client.FromOpenApi<IPetStoreApi>(SampleDocuments.PetStore);
    }

    public void Dispose() {
        _client.Dispose();
        _server.Dispose();
    }

    static string PetJson(long id, string name, string petType = "dog")
        => $$"""{ "id": {{id}}, "name": "{{name}}", "petType": "{{petType}}", "tags": ["x", "y"] }""";

    void StubJson(string path, string method, int status, string body)
        => _server
            .Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));

    [Fact]
    public async Task Binds_path_parameter_and_deserializes_response() {
        StubJson("/v1/pets/42", "GET", 200, PetJson(42, "Rex"));

        var pet = await _api.GetPetByIdAsync(42);

        pet.Should().NotBeNull();
        pet.Id.Should().Be(42);
        pet.Name.Should().Be("Rex");
        pet.PetType.Should().Be("dog");
        pet.Tags.Should().BeEquivalentTo("x", "y");
    }

    [Fact]
    public async Task Prepends_server_base_path() {
        StubJson("/v1/pets/7", "GET", 200, PetJson(7, "Spot"));

        await _api.GetPetByIdAsync(7);

        _server.LogEntries.Last().RequestMessage.Path.Should().Be("/v1/pets/7");
    }

    [Fact]
    public async Task Sends_accept_header_from_response_content_types() {
        StubJson("/v1/pets/1", "GET", 200, PetJson(1, "A"));

        await _api.GetPetByIdAsync(1);

        var headers = _server.LogEntries.Last().RequestMessage.Headers;
        headers.Should().ContainKey("Accept");
        headers["Accept"].ToString().Should().Contain("application/json");
    }

    [Fact]
    public async Task Binds_query_parameters() {
        StubJson("/v1/pets", "GET", 200, $"[{PetJson(1, "A")},{PetJson(2, "B")}]");

        var pets = await _api.ListPetsAsync(limit: 10, status: "available");

        pets.Should().HaveCount(2);
        var query = _server.LogEntries.Last().RequestMessage.Query;
        query["limit"].Should().Contain("10");
        query["status"].Should().Contain("available");
    }

    [Fact]
    public async Task Omits_optional_query_parameters_when_null() {
        StubJson("/v1/pets", "GET", 200, "[]");

        await _api.ListPetsAsync();

        var query = _server.LogEntries.Last().RequestMessage.Query;
        (query == null || !query.ContainsKey("limit")).Should().BeTrue();
    }

    [Fact]
    public async Task Serializes_request_body_as_json() {
        StubJson("/v1/pets", "POST", 201, PetJson(5, "Milo", "cat"));

        var created = await _api.CreatePetAsync(new Pet { Id = 5, Name = "Milo", PetType = "cat" });

        created.Name.Should().Be("Milo");
        var request = _server.LogEntries.Last().RequestMessage;
        request.Body.Should().Contain("Milo");
        request.Headers["Content-Type"].ToString().Should().Contain("application/json");
    }

    [Fact]
    public async Task Returns_raw_rest_response_for_no_content_operation() {
        _server
            .Given(Request.Create().WithPath("/v1/pets/9").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(204));

        var response = await _api.DeletePetAsync(9);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public void Validates_numeric_maximum_before_sending() {
        // Constraint violations are argument errors, surfaced synchronously at the call site.
        Action act = () => { _api.ListPetsAsync(limit: 200); };

        act.Should().Throw<OpenApiConstraintViolationException>()
            .WithMessage("*limit*maximum*");
    }

    [Fact]
    public void Validates_numeric_minimum_before_sending() {
        Action act = () => { _api.GetPetByIdAsync(0); };

        act.Should().Throw<OpenApiConstraintViolationException>().WithMessage("*petId*");
    }

    [Fact]
    public void Validates_enum_membership_before_sending() {
        Action act = () => { _api.ListPetsAsync(status: "teleported"); };

        act.Should().Throw<OpenApiConstraintViolationException>().WithMessage("*allowed values*");
    }

    [Fact]
    public async Task Expands_array_query_parameter_into_multiple_values() {
        StubJson("/v1/pets", "GET", 200, "[]");
        var api = _client.FromOpenApi<IPetStoreArrayApi>(SampleDocuments.PetStore);

        await api.ListPetsAsync(new[] { "red", "blue" });

        var query = _server.LogEntries.Last().RequestMessage.Query;
        query["tags"].Should().BeEquivalentTo("red", "blue");
    }
}

// Interface variant to exercise array query parameter expansion.
public interface IPetStoreArrayApi {
    [RestOperation("listPets")]
    Task<List<Pet>> ListPetsAsync([QueryParam("tags")] string[] tags, System.Threading.CancellationToken ct = default);
}
