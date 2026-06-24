//  Copyright (c) .NET Foundation and Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using RestSharp.MigrationAssistant.Analyzers;
using RestSharp.MigrationAssistant.CodeFixes;
using RestSharp.Tests.MigrationAssistant.Infrastructure;

namespace RestSharp.Tests.MigrationAssistant.Analyzers;

public class SerializationAttributeTests {
    [Fact]
    public async Task RSM010_collapses_matching_serialize_and_deserialize_into_one_json_attribute() {
        const string source = """
            using RestSharp.Serializers;
            class Product {
                [SerializeAs(Name = "product_id")]
                [DeserializeAs(Name = "product_id")]
                public int Id { get; set; }
            }
            """;

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new SerializationAttributeAnalyzer());
        diagnostics.Select(d => d.Id).Should().Equal("RSM010", "RSM010");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SerializationAttributeAnalyzer(), new SerializationAttributeCodeFix());
        migrated.Should()
            .Contain("System.Text.Json.Serialization.JsonPropertyName(\"product_id\")")
            .And.NotContain("SerializeAs")
            .And.NotContain("DeserializeAs");
    }

    [Fact]
    public async Task RSM010_maps_a_single_serialize_attribute() {
        const string source = """
            using RestSharp.Serializers;
            class Product {
                [SerializeAs(Name = "product_name")]
                public string Name { get; set; }
            }
            """;

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SerializationAttributeAnalyzer(), new SerializationAttributeCodeFix());
        migrated.Should().Contain("JsonPropertyName(\"product_name\")").And.NotContain("SerializeAs");
    }

    [Fact]
    public async Task RSM010_is_not_auto_fixed_when_an_xml_only_option_is_present() {
        const string source = """
            using RestSharp.Serializers;
            class Product {
                [SerializeAs(Name = "sku", Attribute = true)]
                public string Sku { get; set; }
            }
            """;

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new SerializationAttributeAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM010");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SerializationAttributeAnalyzer(), new SerializationAttributeCodeFix());
        migrated.Should().Be(source);   // XML-only option has no JSON equivalent
    }

    [Fact]
    public async Task RSM010_is_not_auto_fixed_when_serialize_and_deserialize_names_disagree() {
        const string source = """
            using RestSharp.Serializers;
            class Product {
                [SerializeAs(Name = "price_out")]
                [DeserializeAs(Name = "price_in")]
                public decimal Price { get; set; }
            }
            """;

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SerializationAttributeAnalyzer(), new SerializationAttributeCodeFix());
        migrated.Should().Be(source);
    }

    [Fact]
    public async Task RSM011_flags_a_legacy_and_modern_attribute_conflict() {
        const string source = """
            using RestSharp.Serializers;
            using System.Text.Json.Serialization;
            class Product {
                [SerializeAs(Name = "legacy_color")]
                [JsonPropertyName("color")]
                public string Color { get; set; }
            }
            """;

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new SerializationAttributeAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM011");

        // The conflict has no automatic fix.
        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SerializationAttributeAnalyzer(), new SerializationAttributeCodeFix());
        migrated.Should().Be(source);
    }

    [Fact]
    public async Task Modern_json_attribute_alone_is_not_flagged() {
        const string source = """
            using System.Text.Json.Serialization;
            class Product {
                [JsonPropertyName("name")]
                public string Name { get; set; }
            }
            """;

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new SerializationAttributeAnalyzer());
        diagnostics.Should().BeEmpty();
    }
}
