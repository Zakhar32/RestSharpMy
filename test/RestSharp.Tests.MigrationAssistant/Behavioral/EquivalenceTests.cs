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
using static RestSharp.Tests.MigrationAssistant.Behavioral.RequestCapture;

namespace RestSharp.Tests.MigrationAssistant.Behavioral;

/// <summary>
/// Proves that the body and header rewrites do not change the HTTP request that RestSharp actually sends, across JSON
/// and XML payloads (multipart is covered by <see cref="MultipartEquivalenceTests"/>). Each test builds the legacy form
/// and the migrated form with the modern RestSharp API, sends both against a live WireMock server, and compares the
/// captured outgoing request.
/// </summary>
public class EquivalenceTests {
    const string JsonString = """{"a":1}""";
    const string XmlString  = "<a>1</a>";

    // RSM004: AddParameter(contentType, value, ParameterType.RequestBody) == AddBody(value, contentType), for any
    // string-literal content type. The synchronous AddBodyParameter the legacy call routes through is literally
    // AddBody(value, name) when the name is a content type, so the two are byte-for-byte identical.
    [Theory]
    [InlineData("application/json", JsonString)]
    [InlineData("application/xml", XmlString)]
    public async Task RSM004_AddParameter_RequestBody_is_equivalent_to_AddBody(string contentType, string body) {
        var legacy   = await CaptureAsync(r => r.AddParameter(contentType, body, ParameterType.RequestBody));
        var migrated = await CaptureAsync(r => r.AddBody(body, contentType));

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

    // RSM006 (XML): the same guarantee holds for an XML body — the parts are unchanged and the request still declares
    // an XML content type after the redundant header is removed.
    [Fact]
    public async Task RSM006_removing_redundant_content_type_header_keeps_an_xml_body() {
        var legacy   = await CaptureAsync(r => { r.AddStringBody(XmlString, DataFormat.Xml); r.AddHeader("Content-Type", "application/xml"); });
        var migrated = await CaptureAsync(r => r.AddStringBody(XmlString, DataFormat.Xml));

        migrated.Body.Should().Be(legacy.Body);
        migrated.Url.Should().Be(legacy.Url);
        legacy.ContentType.Should().Contain("xml");
        migrated.ContentType.Should().Contain("xml");
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

    // RSM009: the synchronous Execute is a blocking wrapper over ExecuteAsync, so both produce the same response.
    [Fact]
    public async Task RSM009_sync_Execute_and_async_ExecuteAsync_return_the_same_response() {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/resource"))
            .RespondWith(Response.Create().WithStatusCode(201).WithBody("hello"));

        using var client = new RestClient(server.Url!);

        var sync  = client.Execute(new RestRequest("/resource"));
        var async = await client.ExecuteAsync(new RestRequest("/resource"));

        async.StatusCode.Should().Be(sync.StatusCode);
        async.Content.Should().Be(sync.Content);
        async.IsSuccessful.Should().Be(sync.IsSuccessful);
    }
}
