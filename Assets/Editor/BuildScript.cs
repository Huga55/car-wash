#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Robust local/CI build script for Little Garage.
/// Enforces the exact Unity version, validates the bootstrap scene in Build Settings,
/// provides Windows and Android build entry points, and keeps a detailed log per run.
/// </summary>
public static class BuildScript
{
    private const string RequiredUnityVersion = "2022.3.61f1";
    private const string MainScenePath = "Assets/Scenes/Game.unity";
    private const string WindowsOutputDir = "Builds/Windows";
    private const string WindowsExeName = "LittleCarWorkshop.exe";
    internal const string WindowsLogPath = "build/logs/windows-build.log";
    private const string AndroidLogPath = "build/logs/android-build.log";

    [MenuItem("LittleGarage/Build Windows")]
    public static void BuildWindowsMenu()
    {
        try
        {
            string exe = BuildWindows(WindowsOutputDir);
            Debug.Log("[BuildScript] Windows build succeeded: " + exe);
        }
        catch (Exception ex)
        {
            Debug.LogError("[BuildScript] Windows build failed: " + ex);
            WriteFailureLog("Windows menu", ex, WindowsLogPath);
            throw;
        }
    }

    /// <summary>
    /// Command line entry point: -executeMethod BuildScript.BuildWindows
    /// Optional args:
    ///   -customBuildPath &lt;path&gt; (file or directory)
    ///   -buildOutputDir &lt;directory&gt;
    /// </summary>
    public static void BuildWindows()
    {
        Dictionary<string, string> args = ParseArgs();
        string outputDir = WindowsOutputDir;
        string outputPath = null;

        if (args.TryGetValue("customBuildPath", out string custom) && !string.IsNullOrEmpty(custom))
        {
            outputPath = NormalizePath(custom);
        }
        else if (args.TryGetValue("buildOutputDir", out string dir) && !string.IsNullOrEmpty(dir))
        {
            outputDir = NormalizePath(dir);
        }

        try
        {
            string exe = !string.IsNullOrEmpty(outputPath)
                ? BuildWindowsToPath(outputPath)
                : BuildWindows(outputDir);
            Debug.Log("[BuildScript] Windows build output: " + exe);
            Exit(0);
        }
        catch (Exception ex)
        {
            FailAndExit("Windows", ex, WindowsLogPath);
        }
    }

    public static string BuildWindows(string outputDir)
    {
        if (string.IsNullOrEmpty(outputDir)) outputDir = WindowsOutputDir;
        string path = Path.Combine(NormalizePath(outputDir), WindowsExeName);
        return BuildWindowsToPath(path);
    }

    /// <summary>
    /// CI entry point that ignores -customBuildPath and always writes the
    /// canonical artifact expected by the workflow: Builds/Windows/LittleCarWorkshop.exe.
    /// </summary>
    public static void BuildWindowsCI()
    {
        try
        {
            string exe = BuildWindows(WindowsOutputDir);
            Debug.Log("[BuildScript] Windows CI build output: " + exe);
            Exit(0);
        }
        catch (Exception ex)
        {
            FailAndExit("Windows CI", ex, WindowsLogPath);
        }
    }

    public static void BuildAndroid()
    {
        Dictionary<string, string> args = ParseArgs();
        string outputPath = args.TryGetValue("customBuildPath", out string custom) && !string.IsNullOrEmpty(custom)
            ? NormalizePath(custom)
            : Path.Combine("Builds/Android", "LittleCarWorkshop.apk");

        try
        {
            BuildPlayer(BuildTarget.Android, BuildTargetGroup.Android, outputPath, AndroidLogPath);
            Exit(0);
        }
        catch (Exception ex)
        {
            FailAndExit("Android", ex, AndroidLogPath);
        }
    }

    internal static string BuildWindowsFromArgs()
    {
        Dictionary<string, string> args = ParseArgs();
        if (args.TryGetValue("customBuildPath", out string custom) && !string.IsNullOrEmpty(custom))
            return BuildWindowsToPath(NormalizePath(custom));
        return BuildWindows(WindowsOutputDir);
    }

    private static string BuildWindowsToPath(string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException("Windows output path is empty.", nameof(outputPath));

        outputPath = NormalizePath(outputPath);
        if (Directory.Exists(outputPath) || !Path.HasExtension(outputPath))
            outputPath = Path.Combine(outputPath, WindowsExeName);

        return BuildPlayer(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, outputPath, WindowsLogPath);
    }

    private static string BuildPlayer(BuildTarget target, BuildTargetGroup group, string outputPath, string logPath)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string fullOutputPath = Path.GetFullPath(outputPath);
        string baseLogPath = Path.GetFullPath(logPath);
        string fullLogPath = CreateRunLogPath(baseLogPath);

        var log = new StringBuilder();
        log.AppendLine("=== Little Garage Build ===");
        log.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        log.AppendLine("Target: " + target);
        log.AppendLine("Group: " + group);
        log.AppendLine("Project: " + projectRoot);
        log.AppendLine("Output: " + fullOutputPath);
        log.AppendLine("Log: " + fullLogPath);

        BuildReport report = null;

        try
        {
            string requiredVersion = ReadRequiredUnityVersion(projectRoot);
            log.AppendLine("Required Unity: " + requiredVersion);
            VerifyUnityVersion(requiredVersion);
            log.AppendLine("Running Unity: " + Application.unityVersion);

            string[] scenes = GetEnabledScenes();
            log.AppendLine("Scenes: " + scenes.Length);
            for (int i = 0; i < scenes.Length; i++) log.AppendLine("  - " + scenes[i]);

            Debug.Log(log.ToString());

            ValidateMainScene(scenes, projectRoot);

            if (Directory.Exists(fullOutputPath))
                throw new InvalidOperationException("Output path is a directory, expected an executable file: " + fullOutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath));

            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException(BuildSupportMessage(target, requiredVersion));

            // Safe player settings for this project.
            PlayerSettings.companyName = "LittleGarage";
            PlayerSettings.productName = "Little Car Workshop";
            PlayerSettings.runInBackground = false;
            SetLegacyInputIfPossible();

            if (target == BuildTarget.Android)
            {
                EditorUserBuildSettings.buildAppBundle = fullOutputPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.littlegarage.carworkshop");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOutputPath,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };

            report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            log.AppendLine("Result: " + summary.result);
            log.AppendLine("Errors: " + summary.totalErrors);
            log.AppendLine("Warnings: " + summary.totalWarnings);
            log.AppendLine("Time: " + summary.totalTime);
            log.AppendLine("Size: " + summary.totalSize + " bytes");
            log.AppendLine("Output: " + summary.outputPath);

            if (report.steps != null)
            {
                foreach (BuildStep step in report.steps)
                {
                    log.AppendLine("Step: " + step.name + " (" + step.duration + ")");
                    if (step.messages == null) continue;
                    foreach (BuildStepMessage m in step.messages)
                        log.AppendLine("  [" + m.type + "] " + m.content);
                }
            }

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Build failed: " + summary.result + ". " +
                                                     "Target=" + target + ", Unity=" + Application.unityVersion + ". " +
                                                     "If this is a missing module error, install the required Build Support module for " +
                                                     target + " in Unity " + Application.unityVersion + ".");
            }

            // A directory at the .exe path is NOT a successful build.
            if (!File.Exists(fullOutputPath) || Directory.Exists(fullOutputPath))
                throw new FileNotFoundException("Build reported success but no executable file was found at: " + fullOutputPath, fullOutputPath);

            WriteLog(fullLogPath, baseLogPath, log, "Build succeeded: " + fullOutputPath, report);
            Debug.Log("[BuildScript] Build succeeded: " + fullOutputPath);
            Debug.Log("[BuildScript] Log: " + fullLogPath);
            return fullOutputPath;
        }
        catch (Exception ex)
        {
            // Every failure, including preflight/validation failures, is logged.
            WriteLog(fullLogPath, baseLogPath, log, "Build failed: " + ex, report);
            Debug.LogError("[BuildScript] Build failed: " + ex);
            throw;
        }
    }

    private static string ReadRequiredUnityVersion(string projectRoot)
    {
        string path = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(path))
            throw new FileNotFoundException("ProjectVersion.txt is missing; cannot verify the required Unity version. Expected " +
                                            RequiredUnityVersion + ".", path);

        string[] lines = File.ReadAllLines(path);
        const string prefix = "m_EditorVersion:";
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;

            string version = line.Substring(prefix.Length).Trim();
            if (string.IsNullOrEmpty(version))
                throw new InvalidOperationException("ProjectVersion.txt contains an empty m_EditorVersion value: " + path);
            if (!string.Equals(version, RequiredUnityVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("ProjectVersion.txt requires Unity " + version +
                                                    ", but this build script only supports Unity " + RequiredUnityVersion + ".");
            return version;
        }

        throw new InvalidOperationException("ProjectVersion.txt has no m_EditorVersion entry: " + path);
    }

    private static void VerifyUnityVersion(string requiredVersion)
    {
        string actual = Application.unityVersion;
        if (!string.Equals(actual, requiredVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Unity version mismatch: project requires " + requiredVersion +
                                                " but this editor is " + actual + ". Use Unity " + RequiredUnityVersion + ".");
    }

    private static void ValidateMainScene(string[] scenes, string projectRoot)
    {
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings. " +
                                                MainScenePath + " must be the first enabled scene.");

        string firstScene = scenes[0].Replace('\\', '/');
        if (!string.Equals(firstScene, MainScenePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The first enabled build scene must be " + MainScenePath +
                                                "; found '" + firstScene + "'. Fix Build Settings.");

        string scenePath = Path.GetFullPath(Path.Combine(projectRoot, firstScene));
        if (!File.Exists(scenePath))
            throw new FileNotFoundException("Required build scene is missing: " + scenePath, scenePath);

        string metaPath = scenePath + ".meta";
        string metaGuid = ReadGuidFromMeta(metaPath);
        string settingsPath = Path.Combine(projectRoot, "ProjectSettings", "EditorBuildSettings.asset");
        string settingsGuid = ReadSceneGuidFromBuildSettings(settingsPath, firstScene);

        if (!string.Equals(metaGuid, settingsGuid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scene GUID mismatch: " + MainScenePath + " has guid " + metaGuid +
                                                " in " + metaPath + ", but EditorBuildSettings.asset lists " + settingsGuid +
                                                ". Re-add the scene to Build Settings so the GUIDs match.");

        // Unity's in-memory Build Settings must agree with the serialized file.
        EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes
            .Where(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path))
            .ToArray();
        if (enabled.Length > 0)
        {
            string runtimeGuid = enabled[0].guid.ToString();
            bool emptyGuid = string.IsNullOrEmpty(runtimeGuid) ||
                             runtimeGuid.Equals("00000000000000000000000000000000", StringComparison.Ordinal);
            if (!emptyGuid && !string.Equals(runtimeGuid, metaGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("EditorBuildSettings scene GUID for '" + firstScene + "' is " +
                                                    runtimeGuid + " in memory but " + metaGuid + " in the scene .meta file.");
        }
    }

    private static string ReadGuidFromMeta(string metaPath)
    {
        if (!File.Exists(metaPath))
            throw new FileNotFoundException("Scene .meta file is missing: " + metaPath +
                                            ". Let Unity import the scene before building.", metaPath);

        foreach (string raw in File.ReadAllLines(metaPath))
        {
            string line = raw.Trim();
            if (!line.StartsWith("guid:", StringComparison.Ordinal)) continue;
            string guid = line.Substring("guid:".Length).Trim();
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Scene .meta has an empty guid: " + metaPath);
            return guid;
        }

        throw new InvalidOperationException("Scene .meta has no guid entry: " + metaPath);
    }

    private static string ReadSceneGuidFromBuildSettings(string settingsPath, string scenePath)
    {
        if (!File.Exists(settingsPath))
            throw new FileNotFoundException("EditorBuildSettings.asset is missing: " + settingsPath, settingsPath);

        string[] lines = File.ReadAllLines(settingsPath);
        bool inScene = false;
        bool enabled = false;
        string entryPath = null;
        string entryGuid = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("- enabled:", StringComparison.Ordinal))
            {
                if (inScene && enabled && IsSameScene(entryPath, scenePath))
                    return SceneGuidOrThrow(settingsPath, entryGuid, scenePath);

                inScene = true;
                string value = line.Substring("- enabled:".Length).Trim();
                enabled = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                entryPath = null;
                entryGuid = null;
                continue;
            }

            if (!inScene) continue;
            if (line.StartsWith("m_configObjects:", StringComparison.Ordinal)) break;

            if (line.StartsWith("path:", StringComparison.Ordinal))
                entryPath = line.Substring("path:".Length).Trim().Replace('\\', '/');
            else if (line.StartsWith("guid:", StringComparison.Ordinal))
                entryGuid = line.Substring("guid:".Length).Trim();
        }

        if (inScene && enabled && IsSameScene(entryPath, scenePath))
            return SceneGuidOrThrow(settingsPath, entryGuid, scenePath);

        throw new InvalidOperationException("EditorBuildSettings.asset has no enabled entry for " + scenePath + ": " + settingsPath);
    }

    private static string SceneGuidOrThrow(string settingsPath, string guid, string scenePath)
    {
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException("EditorBuildSettings.asset has no guid for " + scenePath + ": " + settingsPath);
        return guid;
    }

    private static bool IsSameScene(string candidate, string scenePath)
    {
        return !string.IsNullOrEmpty(candidate) &&
               string.Equals(candidate.Replace('\\', '/'), scenePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSupportMessage(BuildTarget target, string unityVersion)
    {
        if (target == BuildTarget.StandaloneWindows64)
        {
            return "Windows build support is missing for Unity " + unityVersion + ". " +
                   "Install 'Windows Build Support (Mono)' or 'Windows Build Support (IL2CPP)' for this exact Unity version, " +
                   "then rerun the build. Required Unity version: " + RequiredUnityVersion + ".";
        }
        if (target == BuildTarget.Android)
        {
            return "Android build support is missing for Unity " + unityVersion + ". " +
                   "Install Android Build Support, SDK/NDK & OpenJDK modules for this Unity version.";
        }
        return "Build target not supported: " + target + " for Unity " + unityVersion + ". Install the corresponding build support module.";
    }

    private static void SetLegacyInputIfPossible()
    {
        try
        {
            var prop = typeof(PlayerSettings).GetProperty("activeInputHandler",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType.IsEnum)
            {
                object legacy = Enum.Parse(prop.PropertyType, "InputManager");
                prop.SetValue(null, legacy, null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BuildScript] Could not set legacy input handler: " + ex.Message);
        }
    }

    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path.Replace('\\', '/'))
            .Distinct()
            .ToArray();
    }

    /// <summary>Keeps one timestamped log per run; never overwrites an earlier run.</summary>
    private static string CreateRunLogPath(string requestedPath)
    {
        string fullPath = Path.GetFullPath(requestedPath);
        string dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(dir)) dir = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(dir);

        string name = Path.GetFileNameWithoutExtension(fullPath);
        string ext = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(name))
        {
            name = "build";
            ext = ".log";
        }

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string candidate = Path.Combine(dir, name + "-" + stamp + ext);
        for (int i = 1; File.Exists(candidate); i++)
            candidate = Path.Combine(dir, name + "-" + stamp + "-" + i + ext);
        return candidate;
    }

    private static void WriteLog(string runLogPath, string stableLogPath, StringBuilder log, string finalMessage, BuildReport report)
    {
        try
        {
            log.AppendLine();
            log.AppendLine("Final: " + finalMessage);
            if (report != null)
            {
                log.AppendLine("SummaryResult: " + report.summary.result);
                log.AppendLine("TotalErrors: " + report.summary.totalErrors);
                log.AppendLine("TotalWarnings: " + report.summary.totalWarnings);
            }

            string text = log.ToString();
            File.WriteAllText(runLogPath, text, Encoding.UTF8);
            if (!string.IsNullOrEmpty(stableLogPath) &&
                !string.Equals(Path.GetFullPath(runLogPath), Path.GetFullPath(stableLogPath), StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(stableLogPath));
                File.WriteAllText(stableLogPath, text, Encoding.UTF8);
            }
            Debug.Log("[BuildScript] Log: " + runLogPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BuildScript] Failed to write build log: " + ex);
        }
    }

    internal static void WriteFailureLog(string label, Exception ex, string logPath)
    {
        try
        {
            string stable = Path.GetFullPath(logPath);
            string run = CreateRunLogPath(stable);
            var log = new StringBuilder();
            log.AppendLine("=== Little Garage Build - " + label + " ===");
            log.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.AppendLine("Unity: " + Application.unityVersion);
            log.AppendLine("Failure: " + ex);
            WriteLog(run, stable, log, "FAILED before/during build: " + ex.Message, null);
        }
        catch (Exception logEx)
        {
            Debug.LogWarning("[BuildScript] Failed to write failure log: " + logEx);
        }
    }

    private static void FailAndExit(string label, Exception ex, string logPath)
    {
        Debug.LogError("[BuildScript] " + label + " build failed: " + ex);
        WriteFailureLog(label, ex, logPath);
        Exit(1);
    }

    private static Dictionary<string, string> ParseArgs()
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] cmd = Environment.GetCommandLineArgs();
        for (int i = 0; i < cmd.Length; i++)
        {
            string token = cmd[i];
            if (string.IsNullOrEmpty(token) || !token.StartsWith("-", StringComparison.Ordinal)) continue;
            string key = token.TrimStart('-');
            string value = null;
            if (i + 1 < cmd.Length && !cmd[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                value = cmd[i + 1];
                i++;
            }
            if (!string.IsNullOrEmpty(key) && value != null)
                args[key] = value;
        }
        return args;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static void Exit(int code)
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }
}

// Compatibility shim for existing CI/workflow expecting the old entry point.
namespace LittleCarWashBuild
{
    public static class Build
    {
        public static void Run()
        {
            try
            {
                Dictionary<string, string> args = ParseArgs();
                if (args.TryGetValue("buildTarget", out string targetName) &&
                    Enum.TryParse(targetName, true, out BuildTarget target) &&
                    target == BuildTarget.Android)
                {
                    BuildScript.BuildAndroid();
                    return;
                }

                string output = BuildScript.BuildWindowsFromArgs();
                Debug.Log("[BuildScript/Compat] Build output: " + output);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BuildScript/Compat] Build failed: " + ex);
                BuildScript.WriteFailureLog("Compat", ex, BuildScript.WindowsLogPath);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static Dictionary<string, string> ParseArgs()
        {
            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] cmd = Environment.GetCommandLineArgs();
            for (int i = 0; i < cmd.Length - 1; i++)
            {
                if (!cmd[i].StartsWith("-", StringComparison.Ordinal)) continue;
                if (cmd[i + 1].StartsWith("-", StringComparison.Ordinal)) continue;
                args[cmd[i].TrimStart('-')] = cmd[i + 1];
            }
            return args;
        }
    }
}
#endif
