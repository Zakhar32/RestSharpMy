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

using System.Text;
using RestSharp;
using static RestSharp.Tests.MigrationAssistant.Behavioral.RequestCapture;

namespace RestSharp.Tests.MigrationAssistant.Behavioral;

/// <summary>
/// Behavioural-equivalence coverage for multipart/form-data requests. Multipart bodies carry a random boundary, so
/// bodies are compared after normalising the boundary; this proves the migration rules leave the multipart parts
/// (form fields and files) intact.
/// </summary>
public class MultipartEquivalenceTests {
    static void BuildMultipart(RestRequest request) {
        request.AlwaysMultipartFormData = true;
        request.AddParameter("field", "value");
        request.AddFile("file", Encoding.UTF8.GetBytes("file contents"), "data.txt", ContentType.Plain);
    }

    // RSM006 on a multipart request: removing a redundant Content-Type header leaves the multipart parts unchanged and
    // still produces a well-formed multipart/form-data content type (with a boundary, which the manual header lacked).
    [Fact]
    public async Task RSM006_multipart_removing_content_type_header_preserves_the_parts() {
        var legacy   = await CaptureAsync(r => { BuildMultipart(r); r.AddHeader("Content-Type", "multipart/form-data"); });
        var migrated = await CaptureAsync(BuildMultipart);

        NormalizeMultipartBoundary(migrated.Body).Should().Be(NormalizeMultipartBoundary(legacy.Body));
        migrated.ContentType.Should().StartWith("multipart/form-data");
        migrated.Body.Should().Contain("name=\"field\"").And.Contain("filename=\"data.txt\"");
    }

    // RSM007 on a multipart request: removing a redundant Accept header does not touch the multipart body at all.
    [Fact]
    public async Task RSM007_multipart_removing_accept_header_preserves_the_parts() {
        var legacy   = await CaptureAsync(r => { BuildMultipart(r); r.AddHeader("Accept", "application/json"); });
        var migrated = await CaptureAsync(BuildMultipart);

        NormalizeMultipartBoundary(migrated.Body).Should().Be(NormalizeMultipartBoundary(legacy.Body));
        migrated.ContentType.Should().StartWith("multipart/form-data");
    }
}
