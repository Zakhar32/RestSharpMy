using RestSharp.Tests.OpenApi.Fixtures;

namespace RestSharp.Tests.OpenApi;

public class CachingTests {
    [Fact]
    public void Same_document_content_returns_the_cached_instance() {
        OpenApiDocument.ClearCache();

        var first  = OpenApiDocument.Load(SampleDocuments.PetStore);
        var second = OpenApiDocument.Load(SampleDocuments.PetStore);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Disabling_the_cache_builds_a_fresh_instance() {
        var cached = OpenApiDocument.Load(SampleDocuments.PetStore);
        var fresh  = OpenApiDocument.Load(SampleDocuments.PetStore, o => o.DisableCache = true);

        fresh.Should().NotBeSameAs(cached);
    }

    [Fact]
    public void Clearing_the_cache_forces_a_rebuild() {
        var first = OpenApiDocument.Load(SampleDocuments.PetStore);
        OpenApiDocument.ClearCache();
        var second = OpenApiDocument.Load(SampleDocuments.PetStore);

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Different_documents_get_different_cache_entries() {
        var pet   = OpenApiDocument.Load(SampleDocuments.PetStore);
        var thing = OpenApiDocument.Load(SampleDocuments.NoOperationIds);

        thing.Should().NotBeSameAs(pet);
        thing.Model.Title.Should().Be("Minimal");
    }

    [Fact]
    public void Generated_types_are_stable_across_loads_of_the_same_document() {
        OpenApiDocument.ClearCache();

        var typeA = OpenApiDocument.Load(SampleDocuments.PetStore).GetGeneratedType("Pet");
        var typeB = OpenApiDocument.Load(SampleDocuments.PetStore).GetGeneratedType("Pet");

        typeB.Should().BeSameAs(typeA);
    }
}
