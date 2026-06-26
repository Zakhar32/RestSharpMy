using System;
using System.Linq;
using System.Threading.Tasks;
using RestSharp.Tests.OpenApi.Fixtures;
using WireMock.Matchers;

namespace RestSharp.Tests.OpenApi;

public sealed class ConcurrencyTests : IDisposable {
    readonly WireMockServer _server;
    readonly RestClient     _client;

    public ConcurrencyTests() {
        _server = WireMockServer.Start();
        _client = new RestClient(_server.Url!);
        _server
            .Given(Request.Create().WithPath(new RegexMatcher("/v1/pets/[0-9]+")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{ "id": 1, "name": "Concurrent", "petType": "dog" }"""));
    }

    public void Dispose() {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Concurrent_creation_and_invocation_is_thread_safe() {
        // Forces the one-time parse, type generation and binding-plan build to race across threads.
        OpenApiDocument.ClearCache();

        var tasks = Enumerable.Range(1, 64).Select(i => Task.Run(async () => {
            var api = _client.FromOpenApi<IPetStoreApi>(SampleDocuments.PetStore);
            return await api.GetPetByIdAsync(i);
        }));

        var pets = await Task.WhenAll(tasks);

        pets.Should().HaveCount(64);
        pets.Should().OnlyContain(p => p != null && p.Name == "Concurrent");
    }
}
