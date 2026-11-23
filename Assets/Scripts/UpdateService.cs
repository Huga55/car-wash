using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Data published by CI as update.json in the latest GitHub release.
/// Fields are public on purpose: JsonUtility only serialises public fields.</summary>
[Serializable]
public sealed class UpdateInfo
{
    public string version;
    public int versionCode;
    public string notes;
    public string androidUrl;
    public string windowsUrl;

    public string UrlForCurrentPlatform()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return androidUrl;
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return windowsUrl;
#else
        return null;
#endif
    }
}

/// <summary>Small, dependency-free updater. It only reads a JSON manifest from
/// the newest GitHub release and installs the asset for the current platform.
/// No background service, no native plugin, no auto-restart on Android.</summary>
public static class UpdateService
{
    private const int CheckTimeout = 15;
    private const int DownloadTimeout = 180;
    private const string DownloadName = "littlecarwash-update";

    public static bool IsNewer(UpdateInfo info)
    {
        return info != null && info.versionCode > AppInfo.VersionCode &&
               !string.IsNullOrEmpty(info.version);
    }

    /// <summary>Fetches update.json. Errors are returned as text so the UI can
    /// show a Russian retry message without leaking engine exceptions.</summary>
    public static IEnumerator Check(Action<UpdateInfo, string> done)
    {
        UpdateInfo result = null;
        string error = null;
        var request = UnityWebRequest.Get(AppInfo.UpdateManifestUrl);
        request.timeout = CheckTimeout;
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var parsed = JsonUtility.FromJson<UpdateInfo>(request.downloadHandler.text);
                if (parsed == null || parsed.versionCode <= 0 || string.IsNullOrEmpty(parsed.version))
                    error = "INVALID_MANIFEST";
                else
                    result = parsed;
            }
            catch (Exception)
            {
                error = "INVALID_MANIFEST";
            }
        }
        else
        {
            error = string.IsNullOrEmpty(request.error) ? "HTTP " + request.responseCode : request.error;
        }
        request.Dispose();
        if (done != null) done(result, error);
    }

    /// <summary>Streams a release asset into persistent storage and reports
    /// 0..1 progress while the request is in flight.</summary>
    public static IEnumerator Download(string url, string targetPath, Action<float> progress,
        Action<string> done)
    {
        string error = null;
        var request = UnityWebRequest.Get(url);
        request.timeout = DownloadTimeout;
        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            if (progress != null) progress(request.downloadProgress);
            yield return null;
        }
        if (request.result != UnityWebRequest.Result.Success)
        {
            error = string.IsNullOrEmpty(request.error) ? "HTTP " + request.responseCode : request.error;
        }
        else
        {
            try
            {
                File.WriteAllBytes(targetPath, request.downloadHandler.data);
                if (progress != null) progress(1f);
            }
            catch (Exception e)
            {
                error = e.Message;
            }
        }
        request.Dispose();
        if (done != null) done(error);
    }

    /// <summary>Installs the update. Android hands the APK to the browser
    /// installer (no custom FileProvider manifest is needed). Windows downloads
    /// a zip, unpacks it and runs a tiny batch that waits for the app to exit,
    /// copies files over and starts the game again.</summary>
    public static IEnumerator Install(UpdateInfo info, Action<float> progress, Action<bool, string> done)
    {
        if (info == null)
        {
            if (done != null) done(false, "NO_UPDATE");
            yield break;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(info.androidUrl))
        {
            if (done != null) done(false, "PLATFORM_UNAVAILABLE");
            yield break;
        }
        Application.OpenURL(info.androidUrl);
        if (progress != null) progress(1f);
        if (done != null) done(true, null);
        yield break;
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
        string url = info.windowsUrl;
        if (string.IsNullOrEmpty(url))
        {
            if (done != null) done(false, "PLATFORM_UNAVAILABLE");
            yield break;
        }
        string zip = Path.Combine(Application.persistentDataPath, DownloadName + ".zip");
        string downloadError = null;
        yield return Download(url, zip, progress, delegate(string e) { downloadError = e; });
        if (!string.IsNullOrEmpty(downloadError))
        {
            if (done != null) done(false, downloadError);
            yield break;
        }
        bool applied = ApplyWindowsUpdate(zip);
        if (progress != null) progress(1f);
        if (done != null) done(applied, applied ? null : "INSTALL_FAILED");
#else
        // Editor and non-shipping platforms only have a browser fallback.
        Application.OpenURL(AppInfo.ReleasesPageUrl);
        if (progress != null) progress(1f);
        if (done != null) done(true, null);
        yield break;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static bool ApplyWindowsUpdate(string zipPath)
    {
        try
        {
            string appDirectory = Directory.GetParent(Application.dataPath).FullName;
            string staging = Path.Combine(Application.persistentDataPath, "update_staging");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, staging);

            // A separate process is required: the running exe cannot overwrite
            // itself. The batch waits a moment, mirrors the new files in and
            // starts whatever exe is in the app folder, so renaming the build
            // does not break future updates.
            var batch = new StringBuilder();
            batch.AppendLine("@echo off");
            batch.AppendLine("setlocal EnableDelayedExpansion");
            batch.AppendLine("set \"APPDIR=" + appDirectory + "\"");
            batch.AppendLine("set \"STAGING=" + staging + "\"");
            batch.AppendLine("timeout /T 2 /NOBREAK >nul");
            batch.AppendLine("robocopy \"!STAGING!\" \"!APPDIR!\" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul");
            batch.AppendLine("set \"EXE=\"");
            batch.AppendLine("for %%F in (\"!APPDIR!\\*.exe\") do if not defined EXE set \"EXE=%%F\"");
            batch.AppendLine("if defined EXE start \"\" \"!EXE!\"");
            batch.AppendLine("del \"%~f0\"");
            string batchPath = Path.Combine(Application.persistentDataPath, "apply_update.cmd");
            File.WriteAllText(batchPath, batch.ToString());

            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/C \"" + batchPath + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            System.Diagnostics.Process.Start(start);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Update install failed: " + e.Message);
            return false;
        }
    }
#endif
}
