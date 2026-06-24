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

using Microsoft.CodeAnalysis.Diagnostics;
using RestSharp.MigrationAssistant.Analyzers;
using RestSharp.MigrationAssistant.CodeFixes;
using RestSharp.Tests.MigrationAssistant.Infrastructure;

namespace RestSharp.Tests.MigrationAssistant.Analyzers;

public class MigrationRuleTests {
    static DiagnosticAnalyzer[] AllAnalyzers => [
        new RemovedInterfaceAnalyzer(), new BodyParameterAnalyzer(), new RedundantHeaderAnalyzer(),
        new NtlmAuthenticatorAnalyzer(), new SyncExecuteAnalyzer(), new SerializationAttributeAnalyzer()
    ];

    // -------- RSM001/002/003: removed interfaces --------

    [Fact]
    public async Task RSM001_flags_IRestResponse_and_rewrites_to_RestResponse() {
        const string source = "using RestSharp; class C { IRestResponse M() => null; }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RemovedInterfaceAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM001");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RemovedInterfaceAnalyzer(), new RemovedInterfaceCodeFix());
        migrated.Should().Be("using RestSharp; class C { RestResponse M() => null; }");
    }

    [Fact]
    public async Task RSM001_preserves_generic_arguments() {
        const string source = "using RestSharp; using System.Collections.Generic; class C { IRestResponse<List<int>> M() => null; }";

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RemovedInterfaceAnalyzer(), new RemovedInterfaceCodeFix());
        migrated.Should().Contain("RestResponse<List<int>>").And.NotContain("IRestResponse");
    }

    [Fact]
    public async Task RSM002_flags_IRestRequest_and_rewrites_to_RestRequest() {
        const string source = "using RestSharp; class C { void M(IRestRequest r) {} }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RemovedInterfaceAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM002");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RemovedInterfaceAnalyzer(), new RemovedInterfaceCodeFix());
        migrated.Should().Be("using RestSharp; class C { void M(RestRequest r) {} }");
    }

    [Fact]
    public async Task RSM003_flags_IHttp_but_offers_no_fix() {
        const string source = "using RestSharp; class C { IHttp H; }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RemovedInterfaceAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM003");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RemovedInterfaceAnalyzer(), new RemovedInterfaceCodeFix());
        migrated.Should().Be(source);   // no automatic fix
    }

    [Fact]
    public async Task Removed_interface_rule_respects_RestSharp_context() {
        // No `using RestSharp;` and not RestSharp-qualified: the unrelated identifier must not be flagged.
        const string source = "class C { IRestResponse M() => null; }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RemovedInterfaceAnalyzer());
        diagnostics.Should().BeEmpty();
    }

    // -------- RSM004/005: body parameters --------

    [Fact]
    public async Task RSM004_rewrites_AddParameter_RequestBody_to_AddBody() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddParameter("application/json", "{}", ParameterType.RequestBody); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new BodyParameterAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM004");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new BodyParameterAnalyzer(), new BodyParameterCodeFix());
        migrated.Should().Contain("""r.AddBody("{}", "application/json")""").And.NotContain("AddParameter");
    }

    [Fact]
    public async Task RSM004_is_flagged_but_not_auto_fixed_for_non_content_type_name() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddParameter("foo", "{}", ParameterType.RequestBody); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new BodyParameterAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM004");

        // Not provably equivalent, so no rewrite is applied.
        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new BodyParameterAnalyzer(), new BodyParameterCodeFix());
        migrated.Should().Be(source);
    }

    [Fact]
    public async Task RSM005_rewrites_AddJsonBody_string_to_AddStringBody() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddJsonBody("{}"); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new BodyParameterAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM005");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new BodyParameterAnalyzer(), new BodyParameterCodeFix());
        migrated.Should().Contain("""r.AddStringBody("{}", DataFormat.Json)""").And.NotContain("AddJsonBody");
    }

    [Fact]
    public async Task AddJsonBody_with_object_is_not_flagged() {
        const string source = "using RestSharp; class C { void M(RestRequest r) { r.AddJsonBody(new object()); } }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new BodyParameterAnalyzer());
        diagnostics.Should().BeEmpty();
    }

    // -------- RSM006/007: redundant headers --------

    [Fact]
    public async Task RSM006_removes_standalone_content_type_header() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddHeader("Content-Type", "application/json"); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RedundantHeaderAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM006");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RedundantHeaderAnalyzer(), new RedundantHeaderCodeFix());
        migrated.Should().NotContain("AddHeader");
    }

    [Fact]
    public async Task RSM007_splices_accept_header_out_of_a_fluent_chain() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddHeader("Accept", "application/json").AddQueryParameter("a", "b"); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RedundantHeaderAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM007");

        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new RedundantHeaderAnalyzer(), new RedundantHeaderCodeFix());
        migrated.Should().Contain("""r.AddQueryParameter("a", "b")""").And.NotContain("AddHeader");
    }

    [Fact]
    public async Task Non_redundant_header_is_not_flagged() {
        const string source = """using RestSharp; class C { void M(RestRequest r) { r.AddHeader("X-Correlation-Id", "1"); } }""";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new RedundantHeaderAnalyzer());
        diagnostics.Should().BeEmpty();
    }

    // -------- RSM008: NTLM authenticator --------

    [Fact]
    public async Task RSM008_flags_NtlmAuthenticator() {
        const string source = "using RestSharp.Authenticators; class C { object A() => new NtlmAuthenticator(); }";

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new NtlmAuthenticatorAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM008");
    }

    // -------- General: migrated code is clean --------

    [Fact]
    public async Task Modern_code_produces_no_diagnostics() {
        const string source = """
            using RestSharp;
            class C {
                void M(RestRequest r) {
                    r.AddJsonBody(new object());
                    r.AddStringBody("{}", DataFormat.Json);
                    r.AddHeader("X-Custom", "v");
                }
                RestResponse Run(RestClient c, RestRequest r) => null;
            }
            """;

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, AllAnalyzers);
        diagnostics.Should().BeEmpty();
    }
}
