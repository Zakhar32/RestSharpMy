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

// The CLI tool targets net10.0 only (to align MSBuildLocator with the SDK), so its tests build on net10 only.
#if NET10_0_OR_GREATER
using System.Linq;
using System.Reflection;
using RestSharp.MigrationAssistant;
using RestSharp.MigrationAssistant.Tool;

namespace RestSharp.Tests.MigrationAssistant.Tool;

public class ToolTests {
    [Fact]
    public void Rule_catalog_covers_every_shipped_rule() {
        var shippedIds = typeof(MigrationDiagnostics)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => ((DiagnosticDescriptor)f.GetValue(null)!).Id);

        foreach (var id in shippedIds) {
            RuleCatalog.KnownIds.Should().Contain(id, "every RSM rule needs tool metadata (confidence + guidance)");
        }
    }

    [Fact]
    public void Report_renders_summary_applied_and_manual_sections() {
        var report = new MigrationReport();
        report.Applied.Add(new AppliedFix("RSM001", "Api.cs", 10, "IRestResponse removed", Confidence.High));
        report.Manual.Add(new ManualAction("RSM008", "Api.cs", 20, "NtlmAuthenticator removed", "Use RestClientOptions."));

        var markdown = report.Render("app.sln", dryRun: false, "2026-01-01 00:00:00");

        markdown.Should().Contain("| RSM001 | 1 | 1 | 0 | High |");
        markdown.Should().Contain("| RSM008 | 1 | 0 | 1 | Manual |");
        markdown.Should().Contain("**[High]** Api.cs:10");
        markdown.Should().Contain("## Manual actions required").And.Contain("Use RestClientOptions.");
        markdown.Should().Contain("**Usages found:** 2 (1 auto-fixed, 1 need manual action)");
    }

    [Theory]
    [InlineData(new[] { "app.sln" }, "app.sln", false)]
    [InlineData(new[] { "app.sln", "--dry-run" }, "app.sln", true)]
    public void Cli_parses_path_and_dry_run(string[] args, string expectedPath, bool expectedDryRun) {
        var options = CliOptions.Parse(args);

        options.Error.Should().BeNull();
        options.Path.Should().Be(expectedPath);
        options.DryRun.Should().Be(expectedDryRun);
    }

    [Fact]
    public void Cli_parses_report_path_and_reports_errors() {
        CliOptions.Parse(["app.sln", "--report", "out.md"]).ReportPath.Should().Be("out.md");
        CliOptions.Parse([]).Error.Should().NotBeNull();
        CliOptions.Parse(["--unknown"]).Error.Should().NotBeNull();
    }
}
#endif
