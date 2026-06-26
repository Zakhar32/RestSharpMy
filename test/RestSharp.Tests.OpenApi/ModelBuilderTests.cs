using System.Linq;
using RestSharp.OpenApi.Model;
using RestSharp.Tests.OpenApi.Fixtures;

namespace RestSharp.Tests.OpenApi;

public class ModelBuilderTests {
    static OpenApiDocumentModel Model => OpenApiDocument.Load(SampleDocuments.PetStore).Model;

    [Fact]
    public void Reads_info_and_server_base_path() {
        var model = Model;

        model.Title.Should().Be("Pet Store");
        model.ApiVersion.Should().Be("1.2.3");
        model.BasePath.Should().Be("v1");
    }

    [Fact]
    public void Parses_all_operations() {
        var ids = Model.Operations.Select(o => o.OperationId).ToList();

        ids.Should().BeEquivalentTo("getPetById", "deletePet", "listPets", "createPet");
    }

    [Fact]
    public void Merges_path_level_parameters_into_operation() {
        var operation = Model.FindOperation("getPetById")!;

        var petId = operation.Parameters.Single(p => p.Name == "petId");
        petId.Location.Should().Be(ApiParameterLocation.Path);
        petId.Required.Should().BeTrue();
        petId.Schema.Primitive.Should().Be(PrimitiveType.Integer);
        petId.Schema.Format.Should().Be("int64");
        petId.Schema.Constraints.Minimum.Should().Be(1);

        operation.Parameters.Should().Contain(p => p.Name == "X-Trace-Id" && p.Location == ApiParameterLocation.Header);
    }

    [Fact]
    public void Parses_query_parameter_constraints_and_enum() {
        var operation = Model.FindOperation("listPets")!;

        var limit = operation.Parameters.Single(p => p.Name == "limit");
        limit.Location.Should().Be(ApiParameterLocation.Query);
        limit.Required.Should().BeFalse();
        limit.Schema.Constraints.Minimum.Should().Be(1);
        limit.Schema.Constraints.Maximum.Should().Be(100);

        var status = operation.Parameters.Single(p => p.Name == "status");
        status.Schema.Constraints.AllowedValues.Should().BeEquivalentTo("available", "pending", "sold");

        var tags = operation.Parameters.Single(p => p.Name == "tags");
        tags.Schema.Kind.Should().Be(SchemaKind.Array);
    }

    [Fact]
    public void Parses_request_body_with_content_type() {
        var operation = Model.FindOperation("createPet")!;

        operation.RequestBody.Should().NotBeNull();
        operation.RequestBody!.Required.Should().BeTrue();
        operation.RequestBody.Content.Should().ContainSingle(c => c.MediaType == "application/json");
        operation.RequestBody.Content[0].Schema!.Name.Should().Be("Pet");
    }

    [Fact]
    public void Resolves_schema_references() {
        var operation = Model.FindOperation("getPetById")!;
        var response  = operation.PrimarySuccessResponse!;

        response.StatusCode.Should().Be("200");
        response.Content.Should().HaveCount(2); // json + xml
        response.Content.First(c => c.MediaType == "application/json").Schema!.Name.Should().Be("Pet");
    }

    [Fact]
    public void Parses_object_schema_with_required_and_constraints() {
        var pet = Model.Schemas["Pet"];

        pet.Kind.Should().Be(SchemaKind.Object);
        pet.Properties.Select(p => p.Name).Should().BeEquivalentTo("id", "name", "petType", "tags");
        pet.Properties.Single(p => p.Name == "name").Required.Should().BeTrue();
        pet.Properties.Single(p => p.Name == "tags").Required.Should().BeFalse();

        var name = pet.Properties.Single(p => p.Name == "name").Schema;
        name.Constraints.MinLength.Should().Be(1);
        name.Constraints.MaxLength.Should().Be(50);
    }

    [Fact]
    public void Parses_discriminator_and_inheritance() {
        var pet = Model.Schemas["Pet"];
        var dog = Model.Schemas["Dog"];
        var cat = Model.Schemas["Cat"];

        pet.Discriminator.Should().NotBeNull();
        pet.Discriminator!.PropertyName.Should().Be("petType");
        pet.Discriminator.Mapping["dog"].Should().Be("Dog");

        dog.BaseSchema.Should().BeSameAs(pet);
        dog.Properties.Select(p => p.Name).Should().Contain(new[] { "bark", "breed" });

        cat.BaseSchema.Should().BeSameAs(pet);
        cat.Properties.Select(p => p.Name).Should().Contain("huntingSkill");
    }

    [Fact]
    public void Sets_source_hash() {
        OpenApiDocument.Load(SampleDocuments.PetStore).SourceHash.Should().NotBeNullOrEmpty();
    }
}
