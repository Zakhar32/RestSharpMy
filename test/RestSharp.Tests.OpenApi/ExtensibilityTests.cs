using System;
using System.Threading;
using System.Threading.Tasks;
using RestSharp.OpenApi.Model;
using RestSharp.Tests.OpenApi.Fixtures;

namespace RestSharp.Tests.OpenApi;

public sealed class ExtensibilityTests : IDisposable {
    readonly WireMockServer _server;
    readonly RestClient     _client;

    public ExtensibilityTests() {
        _server = WireMockServer.Start();
        _client = new RestClient(_server.Url!);
    }

    public void Dispose() {
        _client.Dispose();
        _server.Dispose();
    }

    void StubJson(string path, string method, int status, string body)
        => _server
            .Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));

    [Fact]
    public void Document_transformer_can_correct_the_model() {
        var api = _client.FromOpenApi<IPetStoreApi>(
            SampleDocuments.PetStore,
            o => {
                o.DisableCache = true;
                o.DocumentTransformer = model => {
                    // Pretend the document under-specified the limit; tighten it at runtime.
                    var limit = model.FindOperation("listPets")!.Parameters.First(p => p.Name == "limit");
                    limit.Schema.Constraints.Maximum = 5;
                };
            }
        );

        Action act = () => { api.ListPetsAsync(limit: 10); };

        act.Should().Throw<OpenApiConstraintViolationException>().WithMessage("*maximum of 5*");
    }

    [Fact]
    public async Task Custom_operation_resolver_binds_unconventional_names() {
        StubJson("/v1/pets/8", "GET", 200, """{ "id": 8, "name": "Z", "petType": "cat" }""");

        var api = _client.FromOpenApi<ICustomNamedApi>(
            SampleDocuments.PetStore,
            o => {
                o.DisableCache      = true;
                o.OperationResolver = (method, model) => method.Name == "Fetch" ? model.FindOperation("getPetById") : null;
            }
        );

        var pet = await api.Fetch(8);

        pet.Id.Should().Be(8);
    }

    [Fact]
    public async Task Configure_request_hook_can_override_generated_behaviour() {
        StubJson("/v1/pets/1", "GET", 200, """{ "id": 1, "name": "A", "petType": "cat" }""");

        var api = _client.FromOpenApi<IPetStoreApi>(
            SampleDocuments.PetStore,
            o => {
                o.DisableCache     = true;
                o.ConfigureRequest = ctx => ctx.Request.AddOrUpdateHeader("X-Custom", "injected");
            }
        );

        await api.GetPetByIdAsync(1);

        _server.LogEntries.Last().RequestMessage.Headers["X-Custom"].ToString().Should().Contain("injected");
    }

    [Fact]
    public async Task Binds_header_parameter_via_attribute() {
        StubJson("/v1/pets/2", "GET", 200, """{ "id": 2, "name": "H", "petType": "dog" }""");

        var api = _client.FromOpenApi<IPetStoreHeaderApi>(SampleDocuments.PetStore);

        await api.GetAsync(2, "trace-123");

        _server.LogEntries.Last().RequestMessage.Headers["X-Trace-Id"].ToString().Should().Contain("trace-123");
    }

    [Fact]
    public async Task Binds_via_rest_method_attribute_when_operation_id_is_missing() {
        StubJson("/things/abc", "GET", 200, """{ "ok": true }""");

        var api = _client.FromOpenApi<IThingsApi>(SampleDocuments.NoOperationIds);

        var result = await api.GetThingAsync("abc");

        result.Should().NotBeNull();
        _server.LogEntries.Last().RequestMessage.Path.Should().Be("/things/abc");
    }

    [Fact]
    public async Task Object_return_is_deserialized_into_a_runtime_generated_type() {
        // petType first so System.Text.Json's polymorphic deserialization accepts it (see RuntimeTypeFactory remarks).
        StubJson("/v1/pets/3", "GET", 200, """{ "petType": "dog", "id": 3, "name": "Rex", "bark": true }""");

        var document = _client.LoadOpenApi(SampleDocuments.PetStore);
        var api      = _client.FromOpenApi<IPetStoreObjectApi>(SampleDocuments.PetStore);

        var result = await api.GetAsync(3);

        // The generated Pet is polymorphic, so a "dog" payload comes back as the generated Dog type.
        var dogType = document.GetGeneratedType("Dog")!;
        result.Should().BeOfType(dogType);
        dogType.GetProperty("Name")!.GetValue(result).Should().Be("Rex");
    }

    [Fact]
    public void Unbindable_method_throws_a_clear_error_at_creation_time() {
        Action act = () => _client.FromOpenApi<IUnbindableApi>(SampleDocuments.PetStore, o => o.DisableCache = true);

        act.Should().Throw<OpenApiBindingException>().WithMessage("*Frobnicate*");
    }
}

public interface ICustomNamedApi {
    Task<Pet> Fetch(long petId, CancellationToken ct = default);
}

public interface IPetStoreHeaderApi {
    [RestOperation("getPetById")]
    Task<Pet> GetAsync(long petId, [HeaderParam("X-Trace-Id")] string trace, CancellationToken ct = default);
}

public interface IPetStoreObjectApi {
    [RestOperation("getPetById")]
    Task<object> GetAsync(long petId, CancellationToken ct = default);
}

public interface IUnbindableApi {
    Task<Pet> Frobnicate(long petId, CancellationToken ct = default);
}
