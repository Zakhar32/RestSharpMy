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

public class SyncExecuteTests {
    const string AsyncMethod = "using RestSharp; using System.Threading.Tasks; class C { async Task M(RestClient c, RestRequest r) { @body } }";
    const string SyncMethod  = "using RestSharp; class C { void M(RestClient c, RestRequest r) { @body } }";

    static string Async(string body) => AsyncMethod.Replace("@body", body);
    static string Sync(string body)  => SyncMethod.Replace("@body", body);

    [Fact]
    public async Task RSM009_flags_synchronous_Execute() {
        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(Sync("var resp = c.Execute(r);"), new SyncExecuteAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM009");
    }

    [Fact]
    public async Task RSM009_rewrites_to_await_ExecuteAsync_in_async_context() {
        var migrated = await RoslynTestHarness.ApplyFixAsync(Async("var resp = c.Execute(r);"), new SyncExecuteAnalyzer(), new SyncExecuteCodeFix());
        migrated.Should().Contain("var resp = await c.ExecuteAsync(r);").And.NotContain("c.Execute(r)");
    }

    [Fact]
    public async Task RSM009_preserves_generic_argument() {
        var migrated = await RoslynTestHarness.ApplyFixAsync(Async("var resp = c.Execute<int>(r);"), new SyncExecuteAnalyzer(), new SyncExecuteCodeFix());
        migrated.Should().Contain("await c.ExecuteAsync<int>(r)");
    }

    [Fact]
    public async Task RSM009_parenthesises_when_result_is_used() {
        var migrated = await RoslynTestHarness.ApplyFixAsync(Async("var s = c.Execute(r).Content;"), new SyncExecuteAnalyzer(), new SyncExecuteCodeFix());
        migrated.Should().Contain("(await c.ExecuteAsync(r)).Content");
    }

    [Fact]
    public async Task RSM009_is_reported_but_not_auto_fixed_outside_an_async_context() {
        var source = Sync("var resp = c.Execute(r);");

        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(source, new SyncExecuteAnalyzer());
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RSM009");

        // No async context, so converting to await would not compile: the fix is intentionally withheld.
        var migrated = await RoslynTestHarness.ApplyFixAsync(source, new SyncExecuteAnalyzer(), new SyncExecuteCodeFix());
        migrated.Should().Be(source);
    }

    [Fact]
    public async Task ExecuteAsync_is_not_flagged() {
        var diagnostics = await RoslynTestHarness.GetDiagnosticsAsync(Async("var resp = await c.ExecuteAsync(r);"), new SyncExecuteAnalyzer());
        diagnostics.Should().BeEmpty();
    }
}
