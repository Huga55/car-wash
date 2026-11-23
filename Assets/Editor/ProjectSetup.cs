#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies player settings and registers the scene whenever the project opens.
/// This keeps the repository tiny - no large binary ProjectSettings files.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    static ProjectSetup()
    {
        EditorApplication.delayCall += Configure;
    }

    private static void Configure()
    {
        PlayerSettings.companyName = "Little Hands Studio";
        PlayerSettings.productName = "Little Car Wash";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.littlehands.littlecarwash");

        // Tablets are held in landscape; desktop starts at a comfortable window size.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.runInBackground = false;

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;

        // The game uses UnityEngine.Input and StandaloneInputModule, so make sure
        // the project is not switched to the newer Input System package only.
        ForceLegacyInputManager();

        const string scenePath = "Assets/Scenes/Game.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    found = true;
                }
            }
            if (!found) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void ForceLegacyInputManager()
    {
        // There is no public PlayerSettings property for Active Input Handling in
        // Unity 2022.3, so edit the serialized PlayerSettings object directly.
        // 0 = Input Manager (legacy), 1 = Input System Package, 2 = Both.
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return;

        var settings = new SerializedObject(assets[0]);
        var handler = settings.FindProperty("activeInputHandler");
        if (handler != null && handler.intValue != 0)
        {
            handler.intValue = 0;
            settings.ApplyModifiedProperties();
        }
    }
}
#endif
