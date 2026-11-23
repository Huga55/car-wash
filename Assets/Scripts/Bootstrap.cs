using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Entry point. Creates the camera, input system and game controller.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class Bootstrap : MonoBehaviour
{
    [SerializeField] private int weakDeviceFps = 30;
    [SerializeField] private int fastDeviceFps = 60;

    private void Awake()
    {
        // Older/cheaper tablets stay smooth at 30 fps, faster devices get 60.
        bool weak = SystemInfo.processorCount <= 4 || SystemInfo.systemMemorySize < 2048;
        Application.targetFrameRate = weak ? weakDeviceFps : fastDeviceFps;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif

        if (Camera.main == null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.22f, 0.32f);
        }

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        var audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        AudioManager.Init(audio);

        if (FindObjectOfType<GameController>() == null)
            new GameObject("GameController").AddComponent<GameController>();
    }
}
