/// <summary>Single source of truth for the shipped version and the public
/// update manifest. CI reads this file to tag the GitHub release and generate
/// update.json, so the app and the release can never disagree about versions.
/// </summary>
public static class AppInfo
{
    public const string Version = "1.1.0";
    public const int VersionCode = 1100;

    /// <summary>Stable GitHub URL: the newest release always wins, so no URL
    /// has to be changed when a new version is published.</summary>
    public const string UpdateManifestUrl =
        "https://github.com/Huga55/car-wash/releases/latest/download/update.json";

    public const string ReleasesPageUrl = "https://github.com/Huga55/car-wash/releases/latest";
}
