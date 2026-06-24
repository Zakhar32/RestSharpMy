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
// This assembly is a MINIMAL stand-in for the public surface of RestSharp v106 and earlier. It exists only so that the
// migration sample can compile as "legacy" code and have the RestSharp.MigrationAssistant analyzers run against it. It
// is intentionally not shipped and does not perform any real HTTP work.
// -----------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;

namespace RestSharp {
    public enum Method { Get, Post, Put, Delete, Head, Options, Patch, Merge }

    public enum DataFormat { Json, Xml, None }

    public enum ParameterType { GetOrPost, UrlSegment, HttpHeader, RequestBody, QueryString, Cookie }

    public interface IRestResponse {
        string         Content       { get; }
        HttpStatusCode StatusCode    { get; }
        bool           IsSuccessful  { get; }
        string         ErrorMessage  { get; }
    }

    public interface IRestResponse<out T> : IRestResponse {
        T Data { get; }
    }

    public class RestResponse : IRestResponse {
        public string         Content      { get; set; }
        public HttpStatusCode StatusCode   { get; set; }
        public bool           IsSuccessful { get; set; }
        public string         ErrorMessage { get; set; }
    }

    public class RestResponse<T> : RestResponse, IRestResponse<T> {
        public T Data { get; set; }
    }

    public interface IRestRequest {
        IRestRequest AddParameter(string name, object value, ParameterType type);
        IRestRequest AddParameter(string name, object value);
        IRestRequest AddJsonBody(object body);
        IRestRequest AddHeader(string name, string value);
        IRestRequest AddUrlSegment(string name, string value);
        IRestRequest AddQueryParameter(string name, string value);
    }

    public class RestRequest : IRestRequest {
        public RestRequest() { }
        public RestRequest(string resource, Method method = Method.Get) { }

        public IRestRequest AddParameter(string name, object value, ParameterType type) => this;
        public IRestRequest AddParameter(string name, object value)                     => this;
        public IRestRequest AddJsonBody(object body)                                    => this;
        public IRestRequest AddHeader(string name, string value)                        => this;
        public IRestRequest AddUrlSegment(string name, string value)                    => this;
        public IRestRequest AddQueryParameter(string name, string value)                => this;
    }

    public interface IRestClient {
        IRestResponse    Execute(IRestRequest request);
        IRestResponse<T> Execute<T>(IRestRequest request);
        Uri              BaseUrl { get; set; }
    }

    public class RestClient : IRestClient {
        public RestClient() { }
        public RestClient(string baseUrl) => BaseUrl = new Uri(baseUrl);

        public Uri BaseUrl { get; set; }

        public IRestResponse    Execute(IRestRequest request)    => new RestResponse();
        public IRestResponse<T> Execute<T>(IRestRequest request) => new RestResponse<T>();
    }

    // The low-level HTTP abstraction that was removed in v107.
    public interface IHttp {
        IList<KeyValuePair<string, string>> Headers { get; }
    }
}

namespace RestSharp.Authenticators {
    public class NtlmAuthenticator {
        public NtlmAuthenticator() { }
        public NtlmAuthenticator(string username, string password) { }
    }
}

namespace RestSharp.Serializers {
    public enum NameStyle { AsIs, CamelCase, PascalCase, LowerCase }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
    public sealed class SerializeAsAttribute : Attribute {
        public string                   Name      { get; set; }
        public bool                     Attribute { get; set; }
        public bool                     Content   { get; set; }
        public System.Globalization.CultureInfo Culture { get; set; }
        public NameStyle                NameStyle { get; set; }
        public int                      Index     { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
    public sealed class DeserializeAsAttribute : Attribute {
        public string Name      { get; set; }
        public bool   Attribute { get; set; }
        public bool   Content   { get; set; }
    }
}
