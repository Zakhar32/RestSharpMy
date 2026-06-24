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

using RestSharp;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace RestSharp.Tests.MigrationAssistant.Behavioral;

/// <summary>
/// Proves that the body and header rewrites do not change the HTTP request that RestSharp actually sends. Each test
/// builds the legacy form and the migrated form with the modern RestSharp API, sends both against a live WireMock
/// server, and compares the captured outgoing request.
/// </summary>
public class EquivalenceTests {
    const string JsonString = """{"a":1}""";

    class Captured {
        public string Url         { get; set; }
        public string Body        { get; set; }
        public string ContentType { get; set; }
        public string Accept      { get; set; }
    }

    static async Task<Captured> CaptureAsync(Action<RestRequest> configure) {
        using var server   = WireMockServer.Start();
        var       captured = new Captured();

        server
            .Given(
                Request.Create()
                    .WithPath("/resource")
                    .WithUrl(url => { captured.Url = new Uri(url).PathAndQuery; return true; })
                    .WithBody(body => { captured.Body = body; return true; })
                    .WithHeader(headers => {
                        if (headers.TryGetValue("Content-Type", out var contentType)) captured.ContentType = contentType[0];
                        if (headers.TryGetValue("Accept", out var accept)) captured.Accept = accept[0];
                        return true;
                    })
                    .UsingAnyMethod()
            )
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));

        using var client  = new RestClient(server.Url!);
        var       request = new RestRequest("/resource", Method.Post);
        configure(request);
        await client.ExecuteAsync(request);

        return captured;
    }

    // RSM004: AddParameter(contentType, value, ParameterType.RequestBody) == AddBody(value, contentType)
    [Fact]
    public async Task RSM004_AddParameter_RequestBody_is_equivalent_to_AddBody() {
        var legacy   = await CaptureAsync(r => r.AddParameter("application/json", JsonString, ParameterType.RequestBody));
        var migrated = await CaptureAsync(r => r.AddBody(JsonString, "application/json"));

        migrated.Body.Should().Be(legacy.Body);
        migrated.ContentType.Should().Be(legacy.ContentType);
        migrated.Url.Should().Be(legacy.Url);
    }

    // RSM005: AddJsonBody(string) == AddStringBody(string, DataFormat.Json)
    [Fact]
    public async Task RSM005_AddJsonBody_string_is_equivalent_to_AddStringBody() {
        var legacy   = await CaptureAsync(r => r.AddJsonBody(JsonString));
        var migrated = await CaptureAsync(r => r.AddStringBody(JsonString, DataFormat.Json));

        migrated.Body.Should().Be(legacy.Body);
        migrated.ContentType.Should().Be(legacy.ContentType);
    }

    // RSM006: removing a manual Content-Type header keeps the body and resource identical, and the request still
    // declares a JSON body. RestSharp normalises the content type to "application/json; charset=utf-8" — which is
    // precisely why the hand-written "application/json" header is redundant (and, by dropping the charset, slightly wrong).
    [Fact]
    public async Task RSM006_removing_redundant_content_type_header_keeps_a_json_body() {
        var legacy   = await CaptureAsync(r => { r.AddJsonBody(new { a = 1 }); r.AddHeader("Content-Type", "application/json"); });
        var migrated = await CaptureAsync(r => r.AddJsonBody(new { a = 1 }));

        migrated.Body.Should().Be(legacy.Body);
        migrated.Url.Should().Be(legacy.Url);
        legacy.ContentType.Should().StartWith("application/json");
        migrated.ContentType.Should().StartWith("application/json");
    }

    // RSM007: removing a manual Accept header leaves the payload (method, URL, body, content type) unchanged. The
    // explicit Accept value is replaced by RestSharp's serializer-derived Accept; both still negotiate JSON.
    [Fact]
    public async Task RSM007_removing_redundant_accept_header_leaves_the_payload_unchanged() {
        var legacy   = await CaptureAsync(r => { r.AddJsonBody(new { a = 1 }); r.AddHeader("Accept", "application/json"); });
        var migrated = await CaptureAsync(r => r.AddJsonBody(new { a = 1 }));

        migrated.Body.Should().Be(legacy.Body);
        migrated.ContentType.Should().Be(legacy.ContentType);
        migrated.Url.Should().Be(legacy.Url);
        legacy.Accept.Should().Contain("json");
        migrated.Accept.Should().Contain("json");
    }
}
