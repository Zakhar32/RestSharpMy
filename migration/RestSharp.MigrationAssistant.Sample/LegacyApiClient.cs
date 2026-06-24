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
// -----------------------------------------------------------------------------------------------------------------
// A deliberately legacy (pre-v107) RestSharp consumer used to demonstrate the RestSharp.MigrationAssistant analyzers.
// Building this project surfaces RSM001-RSM008 diagnostics; "Fix all" rewrites them to the modern API.
// -----------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using RestSharp;
using RestSharp.Authenticators;

namespace LegacyConsumer {
    public class User {
        public int    Id   { get; set; }
        public string Name { get; set; }
    }

    public class LegacyUserClient {
        readonly IRestClient _client = new RestClient("https://api.example.com");   // RSM (IRestClient is current, not flagged)

        // RSM001 (IRestResponse<T>), RSM002 (IRestRequest), RSM007 (Accept)
        public IRestResponse<User> GetUser(int id) {
            IRestRequest request = new RestRequest("users/{id}", Method.Get);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Accept", "application/json");
            return _client.Execute<User>(request);
        }

        // RSM001, RSM002, RSM006 (Content-Type), RSM007 (Accept), RSM004 (RequestBody)
        public IRestResponse CreateUser(string json) {
            IRestRequest request = new RestRequest("users", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");
            request.AddParameter("application/json", json, ParameterType.RequestBody);
            return _client.Execute(request);
        }

        // RSM001, RSM002, RSM005 (AddJsonBody string), RSM006 (Content-Type)
        public IRestResponse UpdateUser(int id, string json) {
            IRestRequest request = new RestRequest("users/{id}", Method.Put);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(json);
            return _client.Execute(request);
        }

        // RSM001, RSM002
        public IRestResponse DeleteUser(int id) {
            IRestRequest request = new RestRequest("users/{id}", Method.Delete);
            request.AddUrlSegment("id", id.ToString());
            return _client.Execute(request);
        }

        // RSM001 (IRestResponse<T>), RSM002, RSM007 (Accept)
        public IRestResponse<List<User>> SearchUsers(string term) {
            IRestRequest request = new RestRequest("users/search", Method.Get);
            request.AddQueryParameter("q", term);
            request.AddHeader("Accept", "application/json");
            return _client.Execute<List<User>>(request);
        }

        // RSM002, RSM004 (RequestBody), RSM005 (AddJsonBody string), RSM006 (Content-Type), RSM001
        public IRestResponse PatchUser(int id, string patchJson, string altJson) {
            IRestRequest request = new RestRequest("users/{id}", Method.Patch);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", patchJson, ParameterType.RequestBody);
            request.AddJsonBody(altJson);
            return _client.Execute(request);
        }
    }

    public class LegacyDocumentClient {
        readonly IRestClient _client = new RestClient("https://docs.example.com");

        // RSM002, RSM006 (Content-Type), RSM004 (RequestBody), RSM001
        public IRestResponse Upload(string payload) {
            IRestRequest request = new RestRequest("documents", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", payload, ParameterType.RequestBody);
            return _client.Execute(request);
        }

        // RSM002, RSM005 (AddJsonBody string), RSM007 (Accept), RSM001
        public IRestResponse Annotate(string json) {
            IRestRequest request = new RestRequest("documents/annotate", Method.Post);
            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(json);
            return _client.Execute(request);
        }

        // RSM002, RSM004 (RequestBody), RSM006 (Content-Type), RSM001
        public IRestResponse Replace(string xml) {
            IRestRequest request = new RestRequest("documents/replace", Method.Put);
            request.AddHeader("Content-Type", "application/xml");
            request.AddParameter("application/xml", xml, ParameterType.RequestBody);
            return _client.Execute(request);
        }

        // RSM001, RSM002, RSM007 (Accept)
        public IRestResponse<User> Fetch(int id) {
            IRestRequest request = new RestRequest("documents/{id}", Method.Get);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Accept", "application/json");
            return _client.Execute<User>(request);
        }
    }

    public class LegacyOrderClient {
        readonly IRestClient _client = new RestClient("https://orders.example.com");

        // RSM001, RSM002, RSM006, RSM007, RSM004
        public IRestResponse PlaceOrder(string json) {
            IRestRequest request = new RestRequest("orders", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");
            request.AddParameter("application/json", json, ParameterType.RequestBody);
            return _client.Execute(request);
        }

        // RSM001, RSM002, RSM005, RSM006
        public IRestResponse AmendOrder(int id, string json) {
            IRestRequest request = new RestRequest("orders/{id}", Method.Put);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(json);
            return _client.Execute(request);
        }

        // RSM001 (IRestResponse<T>), RSM002, RSM007
        public IRestResponse<User> GetOrderOwner(int id) {
            IRestRequest request = new RestRequest("orders/{id}/owner", Method.Get);
            request.AddUrlSegment("id", id.ToString());
            request.AddHeader("Accept", "application/json");
            return _client.Execute<User>(request);
        }

        // RSM001, RSM002, RSM004
        public IRestResponse Cancel(int id, string reasonJson) {
            IRestRequest request = new RestRequest("orders/{id}/cancel", Method.Post);
            request.AddUrlSegment("id", id.ToString());
            request.AddParameter("application/json", reasonJson, ParameterType.RequestBody);
            return _client.Execute(request);
        }

        // RSM001, RSM002, RSM005, RSM007
        public IRestResponse Track(string json) {
            IRestRequest request = new RestRequest("orders/track", Method.Post);
            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(json);
            return _client.Execute(request);
        }
    }

    public static class LegacyRequestFactory {
        // RSM002 x3 (parameter + local + return), RSM004 (RequestBody), RSM005 (AddJsonBody string)
        public static IRestRequest BuildPost(string resource, string json) {
            IRestRequest request = new RestRequest(resource, Method.Post);
            request.AddParameter("application/json", json, ParameterType.RequestBody);
            return request;
        }

        // RSM002, RSM005 (AddJsonBody string), RSM006 (Content-Type)
        public static IRestRequest BuildJson(string resource, string json) {
            IRestRequest request = new RestRequest(resource, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(json);
            return request;
        }

        // RSM002, RSM007 (Accept)
        public static IRestRequest BuildGet(string resource) {
            IRestRequest request = new RestRequest(resource, Method.Get);
            request.AddHeader("Accept", "application/json");
            return request;
        }
    }

    public static class LegacyAuthSetup {
        // RSM008 x3 (NtlmAuthenticator), RSM003 (IHttp)
        public static NtlmAuthenticator CreateDefault()    => new NtlmAuthenticator();
        public static NtlmAuthenticator CreateForUser()    => new NtlmAuthenticator("user", "secret");
        public static NtlmAuthenticator CreateForService() => new NtlmAuthenticator("svc", "token");

        public static IHttp LowLevelHttp;
    }
}
