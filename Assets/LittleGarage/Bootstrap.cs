using UnityEngine;

namespace LittleGarage
{
    /// <summary>Single scene entry point. Creates the whole game at runtime so the
    /// project has no fragile asset references and can be built from one scene.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = SystemInfo.processorCount <= 4 ? 30 : 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            GameConfig config = Resources.Load<GameConfig>("LittleGarage/GameConfig");
            if (config == null)
                config = GameConfig.CreateDefault();

            GameObject root = new GameObject("Little Garage");
            root.transform.SetParent(null, false);
            GameManager manager = root.AddComponent<GameManager>();
            manager.Initialize(config);
        }
    }
}
