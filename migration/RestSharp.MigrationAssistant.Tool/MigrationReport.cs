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

namespace RestSharp.MigrationAssistant.Tool;

public sealed record AppliedFix(string RuleId, string File, int Line, string Message, Confidence Confidence);

public sealed record ManualAction(string RuleId, string File, int Line, string Message, string Guidance);

/// <summary>Accumulates the outcome of a migration run and renders it as a Markdown report and a console summary.</summary>
public sealed class MigrationReport {
    public List<AppliedFix>    Applied { get; } = [];
    public List<ManualAction>  Manual  { get; } = [];

    int TotalFound => Applied.Count + Manual.Count;

    public string Render(string target, bool dryRun, string timestamp) {
        var sb = new StringBuilder();

        sb.AppendLine("# RestSharp migration report");
        sb.AppendLine();
        sb.AppendLine($"- **Target:** `{target}`");
        sb.AppendLine($"- **Generated:** {timestamp}");
        sb.AppendLine($"- **Mode:** {(dryRun ? "dry run (no files modified)" : "applied")}");
        sb.AppendLine($"- **Usages found:** {TotalFound} ({Applied.Count} auto-fixed, {Manual.Count} need manual action)");
        sb.AppendLine();

        AppendSummary(sb);
        AppendApplied(sb);
        AppendManual(sb);

        return sb.ToString();
    }

    void AppendSummary(StringBuilder sb) {
        sb.AppendLine("## Summary by rule");
        sb.AppendLine();
        sb.AppendLine("| Rule | Found | Auto-fixed | Manual | Confidence |");
        sb.AppendLine("|------|-------|------------|--------|------------|");

        var ruleIds = Applied.Select(a => a.RuleId).Concat(Manual.Select(m => m.RuleId)).Distinct().OrderBy(id => id);
        foreach (var id in ruleIds) {
            var fixedCount  = Applied.Count(a => a.RuleId == id);
            var manualCount = Manual.Count(m => m.RuleId == id);
            var confidence  = fixedCount > 0 ? RuleCatalog.For(id).Confidence.ToString() : "Manual";
            sb.AppendLine($"| {id} | {fixedCount + manualCount} | {fixedCount} | {manualCount} | {confidence} |");
        }

        sb.AppendLine();
    }

    void AppendApplied(StringBuilder sb) {
        sb.AppendLine("## Applied fixes");
        sb.AppendLine();

        if (Applied.Count == 0) {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        foreach (var fix in Applied.OrderBy(a => a.File).ThenBy(a => a.Line)) {
            sb.AppendLine($"- `{fix.RuleId}` **[{fix.Confidence}]** {fix.File}:{fix.Line} — {fix.Message}");
        }

        sb.AppendLine();
    }

    void AppendManual(StringBuilder sb) {
        sb.AppendLine("## Manual actions required");
        sb.AppendLine();

        if (Manual.Count == 0) {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        foreach (var action in Manual.OrderBy(m => m.File).ThenBy(m => m.Line)) {
            sb.AppendLine($"- `{action.RuleId}` {action.File}:{action.Line} — {action.Message}");
            sb.AppendLine($"  - _Action:_ {action.Guidance}");
        }

        sb.AppendLine();
    }

    public string ConsoleSummary() {
        var sb = new StringBuilder();
        sb.AppendLine($"Usages found : {TotalFound}");
        sb.AppendLine($"Auto-fixed   : {Applied.Count}");
        sb.AppendLine($"Manual action: {Manual.Count}");
        return sb.ToString().TrimEnd();
    }
}
