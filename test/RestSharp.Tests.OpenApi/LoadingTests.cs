using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RestSharp.Tests.OpenApi.Fixtures;

namespace RestSharp.Tests.OpenApi;

public sealed class LoadingTests : IDisposable {
    readonly WireMockServer _server;
    readonly RestClient     _client;

    public LoadingTests() {
        _server = WireMockServer.Start();
        _client = new RestClient(_server.Url!);
        _server
            .Given(Request.Create().WithPath("/v1/pets/1").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{ "id": 1, "name": "FromFile", "petType": "dog" }"""));
    }

    public void Dispose() {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Loads_document_from_a_file_path() {
        var path = Path.Combine(Path.GetTempPath(), $"petstore-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, SampleDocuments.PetStore);

        try {
            var api = _client.FromOpenApi<IPetStoreApi>(path);
            var pet = await api.GetPetByIdAsync(1);

            pet.Name.Should().Be("FromFile");
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Loads_document_from_a_stream() {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleDocuments.PetStore));

        var api = _client.FromOpenApi<IPetStoreApi>(stream);
        var pet = await api.GetPetByIdAsync(1);

        pet.Name.Should().Be("FromFile");
    }

    [Fact]
    public async Task Loads_document_from_inline_content() {
        var api = _client.FromOpenApi<IPetStoreApi>(SampleDocuments.PetStore);
        var pet = await api.GetPetByIdAsync(1);

        pet.Name.Should().Be("FromFile");
    }

    [Fact]
    public void Invalid_json_raises_a_clear_parse_error() {
        Action act = () => _client.FromOpenApi<IPetStoreApi>("{ not valid json ", o => o.DisableCache = true);

        act.Should().Throw<OpenApiParseException>();
    }
}
