﻿using EnvDTE80;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TSLint
{
    internal class TsLint
    {
        private static DTE2 _dte2;

        private const string CmdSubpath =
            "\\node_modules\\.bin\\";

        private static readonly KeyValuePair<string, string> DefKeyValuePair =
            default(KeyValuePair<string, string>);

        // Key: project/solution path, value: tslint.cmd path.
        private static readonly Dictionary<string, string> Cache =
            new Dictionary<string, string>();

        public static void Init(DTE2 dte2)
        {
            TsLint._dte2 = dte2;
        }

        public static async Task<string> Run(string tsFilename)
        {
            if (TsLint._dte2 == null) // Fixes crashing for those that open project as folder.
                return null;

            var existingPath = TsLint.Cache.SingleOrDefault(
                p => tsFilename.Contains(p.Key)
            );

            if (existingPath.Equals(TsLint.DefKeyValuePair))
            {
                // First, check if the project for this file has local installation of tslint.
                var potentialPath = TsLint.TryGetProjectTsLint(tsFilename);

                if (potentialPath.Equals(TsLint.DefKeyValuePair))
                {
                    // Now, check if the solution has local installation of tslint.
                    potentialPath = TsLint.TryGetSolutionTsLint();
                }

                if (potentialPath.Equals(TsLint.DefKeyValuePair))
                {
                    // No project, no solution, check if we can find local installation of tslint "manually".
                    potentialPath = TsLint.TryGetTsLint(tsFilename);
                }

                if (!potentialPath.Equals(TsLint.DefKeyValuePair))
                {
                    existingPath = potentialPath;
                    TsLint.Cache.Add(existingPath.Key, existingPath.Value);
                }
            }

            if (existingPath.Equals(TsLint.DefKeyValuePair))
                return null;

            var tscCmdPath = $"{existingPath.Value}tsc.cmd";
            var tscConfigPath = $"{existingPath.Key}\\tsconfig.json";

            var useTsLintProjectFlag = false;

            if (File.Exists(tscCmdPath) && File.Exists(tscConfigPath))
                useTsLintProjectFlag = true;

            var procInfo = new ProcessStartInfo()
            {
                FileName = $"{existingPath.Value}tslint.cmd",
                WorkingDirectory = existingPath.Key,
                Arguments = $"-t JSON {(useTsLintProjectFlag ? $"--project \"{tscConfigPath}\"" : "")} \"{tsFilename}\"",
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var proc = Process.Start(procInfo);

            if (proc == null)
                return null;

            var standardOutput = proc.StandardOutput;
            var output = await standardOutput.ReadToEndAsync();

            await proc.WaitForExitAsync();

            return output;
        }

        private static KeyValuePair<string, string> TryGetProjectTsLint(string tsFilename)
        {
            var item = _dte2.Solution.FindProjectItem(tsFilename);
            var project = item.ContainingProject;

            // If there is no project (file is dragged or explicitly opened).
            if (string.IsNullOrEmpty(project.FullName))
                return TsLint.DefKeyValuePair;

            var projectPath = Path.GetDirectoryName(project.FullName);
            var tsLintCmdPath = $"{projectPath}{CmdSubpath}";

            return File.Exists(tsLintCmdPath)
                ? new KeyValuePair<string, string>(projectPath, tsLintCmdPath)
                : TsLint.DefKeyValuePair;
        }

        private static KeyValuePair<string, string> TryGetSolutionTsLint()
        {
            // If there is no solution (file is dragged or explicitly opened).
            if (string.IsNullOrEmpty(_dte2.Solution.FullName))
                return TsLint.DefKeyValuePair;

            var solutionPath = Path.GetDirectoryName(_dte2.Solution.FullName);
            var tsLintCmdPath = $"{solutionPath}{CmdSubpath}tslint.cmd";

            return File.Exists(tsLintCmdPath)
                ? new KeyValuePair<string, string>(solutionPath, $"{solutionPath}{CmdSubpath}")
                : TsLint.DefKeyValuePair;
        }

        private static KeyValuePair<string, string> TryGetTsLint(string tsFilename)
        {
            var dirPath = Path.GetDirectoryName(tsFilename);

            if (dirPath == null)
                return TsLint.DefKeyValuePair;

            var dirInfo = new DirectoryInfo(dirPath);
            var tsLintCmdPath = $"{dirInfo.FullName}{CmdSubpath}tslint.cmd";

            while (!File.Exists(tsLintCmdPath) && dirInfo.Parent != null)
            {
                dirInfo = dirInfo.Parent;
                tsLintCmdPath = $"{dirInfo.FullName}{CmdSubpath}tslint.cmd";
            }

            return File.Exists(tsLintCmdPath)
                ? new KeyValuePair<string, string>(dirInfo.FullName, $"{dirInfo.FullName}{CmdSubpath}")
                : TsLint.DefKeyValuePair;
        }
    }
}
