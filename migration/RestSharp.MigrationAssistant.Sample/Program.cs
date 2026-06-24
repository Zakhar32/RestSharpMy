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

using System;
using LegacyConsumer;

var users = new LegacyUserClient();
Console.WriteLine(users.GetUser(1).IsSuccessful);
Console.WriteLine(users.CreateUser("{}").IsSuccessful);
Console.WriteLine(users.UpdateUser(1, "{}").IsSuccessful);
Console.WriteLine(users.DeleteUser(1).IsSuccessful);
Console.WriteLine(users.SearchUsers("foo").IsSuccessful);
Console.WriteLine(users.PatchUser(1, "{}", "{}").IsSuccessful);

var docs = new LegacyDocumentClient();
Console.WriteLine(docs.Upload("{}").IsSuccessful);
Console.WriteLine(docs.Annotate("{}").IsSuccessful);
Console.WriteLine(docs.Replace("<x/>").IsSuccessful);
Console.WriteLine(docs.Fetch(1).IsSuccessful);

var orders = new LegacyOrderClient();
Console.WriteLine(orders.PlaceOrder("{}").IsSuccessful);
Console.WriteLine(orders.AmendOrder(1, "{}").IsSuccessful);
Console.WriteLine(orders.GetOrderOwner(1).IsSuccessful);
Console.WriteLine(orders.Cancel(1, "{}").IsSuccessful);
Console.WriteLine(orders.Track("{}").IsSuccessful);

_ = LegacyRequestFactory.BuildPost("res", "{}");
_ = LegacyRequestFactory.BuildJson("res", "{}");
_ = LegacyRequestFactory.BuildGet("res");

_ = LegacyAuthSetup.CreateDefault();
_ = LegacyAuthSetup.CreateForUser();
_ = LegacyAuthSetup.CreateForService();
