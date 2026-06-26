using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using RestSharp.Tests.OpenApi.Fixtures;

namespace RestSharp.Tests.OpenApi;

public class RuntimeTypeFactoryTests {
    static LoadedOpenApiDocument Load() => OpenApiDocument.Load(SampleDocuments.PetStore);

    [Fact]
    public void Generates_a_type_per_named_object_schema() {
        var document = Load();

        document.GeneratedTypes.Keys.Should().BeEquivalentTo("Pet", "Dog", "Cat");
    }

    [Fact]
    public void Maps_properties_to_pascal_case_clr_properties() {
        var pet = Load().GetGeneratedType("Pet")!;

        pet.GetProperty("Id").Should().NotBeNull();
        pet.GetProperty("Name").Should().NotBeNull();
        pet.GetProperty("Tags").Should().NotBeNull();
        pet.GetProperty("Id")!.PropertyType.Should().Be(typeof(long));
        pet.GetProperty("Tags")!.PropertyType.Should().Be(typeof(System.Collections.Generic.List<string>));
    }

    [Fact]
    public void Derived_types_inherit_from_the_base() {
        var document = Load();
        var pet      = document.GetGeneratedType("Pet")!;
        var dog      = document.GetGeneratedType("Dog")!;

        pet.IsAssignableFrom(dog).Should().BeTrue();
        dog.GetProperty("Bark").Should().NotBeNull();
        dog.GetProperty("Breed").Should().NotBeNull();
    }

    [Fact]
    public void Polymorphic_base_round_trips_through_system_text_json() {
        var document = Load();
        var petType  = document.GetGeneratedType("Pet")!;
        var dogType  = document.GetGeneratedType("Dog")!;

        // Deserialize a dog payload into the polymorphic base - STJ should pick the derived type.
        // Documented constraint: System.Text.Json requires the discriminator property to be the first
        // property of the object (on net8). On net9+ you can opt into out-of-order discriminators via
        // JsonSerializerOptions.AllowOutOfOrderMetadataProperties. The payload below puts it first.
        const string json = """{ "petType": "dog", "id": 7, "name": "Rex", "bark": true, "breed": "Husky" }""";
        var deserialized  = JsonSerializer.Deserialize(json, petType);

        deserialized.Should().BeOfType(dogType);
        dogType.GetProperty("Bark")!.GetValue(deserialized).Should().Be(true);
        petType.GetProperty("Name")!.GetValue(deserialized).Should().Be("Rex");

        // Serializing through the base type writes the discriminator back out.
        var serialized = JsonSerializer.Serialize(deserialized, petType);
        serialized.Should().Contain("\"petType\":\"dog\"");
        serialized.Should().Contain("\"breed\":\"Husky\"");
    }

    [Fact]
    public void Uses_json_property_name_for_wire_mapping() {
        var petType  = Load().GetGeneratedType("Pet")!;
        var instance = Activator.CreateInstance(petType)!;
        petType.GetProperty("Id")!.SetValue(instance, 99L);
        petType.GetProperty("Name")!.SetValue(instance, "Fido");

        var json = JsonSerializer.Serialize(instance, petType);

        json.Should().Contain("\"id\":99");
        json.Should().Contain("\"name\":\"Fido\"");
    }

    [Fact]
    public void Type_generation_is_idempotent_per_document() {
        var document = Load();

        var first  = document.GetGeneratedType("Pet");
        var second = document.GetGeneratedType("Pet");

        first.Should().BeSameAs(second);
    }
}
