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

            ApplyPlayerSettings(target, outputPath);

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

        /// <summary>Settings are applied inside the explicit build method, never
        /// from InitializeOnLoad, so batch builds never see a pending domain reload.</summary>
        private static void ApplyPlayerSettings(BuildTarget target, string outputPath)
        {
            // The game uses the legacy Input API and StandaloneInputModule.
            // CI-generated ProjectSettings may default to the new Input System.
            var inputHandler = typeof(PlayerSettings).GetProperty("activeInputHandler",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (inputHandler != null)
                inputHandler.SetValue(null, Enum.Parse(inputHandler.PropertyType, "InputManager"), null);

            PlayerSettings.companyName = "Little Car Wash";
            PlayerSettings.productName = "Little Car Wash";
            // Keep the installed app version identical to the version that CI
            // writes into update.json; otherwise the updater can never compare.
            PlayerSettings.bundleVersion = AppInfo.Version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.runInBackground = false;

            if (target != BuildTarget.Android) return;

            EditorUserBuildSettings.buildAppBundle =
                outputPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.littlecarwash.game");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.bundleVersionCode = AppInfo.VersionCode;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
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
