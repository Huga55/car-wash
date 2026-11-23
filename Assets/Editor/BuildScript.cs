#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LittleCarWashBuild
{
    /// <summary>
    /// Explicit CI build entry point. Passing the scene path directly avoids
    /// relying on EditorBuildSettings and prevents "untitled scene" failures.
    /// </summary>
    public static class Build
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        public static void Run()
        {
            Dictionary<string, string> args = ParseArgs();

            string outputPath;
            if (!args.TryGetValue("customBuildPath", out outputPath) || string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("Missing -customBuildPath argument from unity-builder.");
                EditorApplication.Exit(110);
                return;
            }

            string targetName;
            BuildTarget target;
            if (!args.TryGetValue("buildTarget", out targetName) ||
                !Enum.TryParse(targetName, true, out target))
            {
                Debug.LogError("Missing or invalid -buildTarget argument from unity-builder.");
                EditorApplication.Exit(111);
                return;
            }

            if (target == BuildTarget.Android)
            {
                EditorUserBuildSettings.buildAppBundle =
                    outputPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);
            }

            Debug.Log("Building " + ScenePath + " -> " + outputPath + " (" + target + ")");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log("Build result: " + summary.result +
                      " | errors: " + summary.totalErrors +
                      " | warnings: " + summary.totalWarnings +
                      " | size: " + summary.totalSize);

            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static Dictionary<string, string> ParseArgs()
        {
            var args = new Dictionary<string, string>();
            string[] commandLine = Environment.GetCommandLineArgs();

            for (int i = 0; i < commandLine.Length - 1; i++)
            {
                if (!commandLine[i].StartsWith("-", StringComparison.Ordinal)) continue;
                if (commandLine[i + 1].StartsWith("-", StringComparison.Ordinal)) continue;
                args[commandLine[i].TrimStart('-')] = commandLine[i + 1];
            }

            return args;
        }
    }
}
#endif
