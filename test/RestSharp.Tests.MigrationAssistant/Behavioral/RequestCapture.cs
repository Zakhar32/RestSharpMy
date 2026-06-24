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

/// <summary>The outgoing request as observed by the test server.</summary>
public class CapturedRequest {
    public string Url         { get; set; }
    public string Body        { get; set; }
    public string ContentType { get; set; }
    public string Accept      { get; set; }
}

/// <summary>
/// Sends a request built with the modern RestSharp API against a live WireMock server and captures what was sent.
/// Used by the behavioural-equivalence tests to compare a legacy form and its migrated form.
/// </summary>
public static class RequestCapture {
    public static async Task<CapturedRequest> CaptureAsync(Action<RestRequest> configure, Method method = Method.Post) {
        using var server   = WireMockServer.Start();
        var       captured = new CapturedRequest();

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
        var       request = new RestRequest("/resource", method);
        configure(request);
        await client.ExecuteAsync(request);

        return captured;
    }

    /// <summary>
    /// Replaces the random multipart boundary with a fixed token so two multipart bodies built independently can be
    /// compared part-for-part. Non-multipart bodies are returned unchanged.
    /// </summary>
    public static string NormalizeMultipartBoundary(string body) {
        if (string.IsNullOrEmpty(body)) return body;

        var firstLine = body.Split('\n')[0].TrimEnd('\r');
        if (!firstLine.StartsWith("--")) return body;

        var boundary = firstLine.Substring(2);
        return boundary.Length == 0 ? body : body.Replace(boundary, "BOUNDARY");
    }
}
