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
// Legacy model classes annotated with RestSharp's XML serialization attributes, used to demonstrate RSM010/RSM011.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using RestSharp.Serializers;

namespace LegacyConsumer {
    public class Product {
        // RSM010 (auto-fix): matching serialize/deserialize names collapse to one [JsonPropertyName("product_id")].
        [SerializeAs(Name = "product_id")]
        [DeserializeAs(Name = "product_id")]
        public int Id { get; set; }

        // RSM010 (auto-fix): serialize only.
        [SerializeAs(Name = "product_name")]
        public string Name { get; set; }

        // RSM010 (manual): XML-only option (Attribute) has no JSON equivalent.
        [SerializeAs(Name = "sku", Attribute = true)]
        public string Sku { get; set; }

        // RSM010 (manual): serialize and deserialize names disagree — cannot be one JSON attribute.
        [SerializeAs(Name = "price_out")]
        [DeserializeAs(Name = "price_in")]
        public decimal Price { get; set; }

        // RSM011 (conflict): a legacy attribute combined with a modern JSON attribute.
        [SerializeAs(Name = "legacy_color")]
        [JsonPropertyName("color")]
        public string Color { get; set; }
    }

    // RSM010 (auto-fix): class-level legacy attribute.
    [SerializeAs(Name = "Catalog")]
    public class Catalog {
        [DeserializeAs(Name = "items")]
        public List<Product> Items { get; set; }
    }
}
