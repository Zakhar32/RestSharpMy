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

namespace RestSharp.MigrationAssistant.Tool;

/// <summary>Parsed command-line options for the migration tool.</summary>
public sealed class CliOptions {
    public string  Path       { get; init; } = "";
    public string  ReportPath { get; init; } = "migration-report.md";
    public bool    DryRun     { get; init; }
    public string? Error      { get; init; }

    public static CliOptions Parse(string[] args) {
        string? path = null, report = null;
        var     dryRun = false;

        for (var i = 0; i < args.Length; i++) {
            var arg = args[i];
            switch (arg) {
                case "--report" or "-r":
                    if (i + 1 >= args.Length) return new CliOptions { Error = "--report requires a file path." };
                    report = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return new CliOptions { Error = $"Unknown option '{arg}'." };
                    if (path != null) return new CliOptions { Error = "Specify a single solution or project path." };
                    path = arg;
                    break;
            }
        }

        return path == null
            ? new CliOptions { Error = "No solution or project path specified." }
            : new CliOptions { Path = path, ReportPath = report ?? "migration-report.md", DryRun = dryRun };
    }
}
