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

using Microsoft.Build.Locator;
using RestSharp.MigrationAssistant.Tool;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h")) {
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var options = CliOptions.Parse(args);
if (options.Error != null) {
    Console.Error.WriteLine(options.Error);
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

// Must run before any MSBuildWorkspace type is touched, so it loads MSBuild from the installed SDK.
if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

return await MigrationRunner.RunAsync(options);

static void PrintUsage()
    => Console.WriteLine(
        """
        dotnet restore-migration — apply the RestSharp.MigrationAssistant rules across a solution or project.

        Usage:
          dotnet restore-migration <path> [options]

          <path>                 Path to a .sln, .slnx, .csproj, or a directory containing one.

        Options:
          -r, --report <file>    Report output path (default: migration-report.md).
              --dry-run          Analyze and report without modifying any files.
          -h, --help             Show this help.
        """
    );
