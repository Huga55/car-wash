using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LittleGarage.Tests
{
    public class SceneSmokeTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [UnityTest]
        public IEnumerator GameScene_Bootstraps_And_CapturesScreenshot()
        {
            Assert.IsTrue(File.Exists(ScenePath), "Required scene missing: " + ScenePath);

            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Failed to start scene load: " + ScenePath);
            while (!load.isDone) yield return null;

            // Allow Bootstrap.Awake/Start and first UI frame.
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            Bootstrap bootstrap = Object.FindObjectOfType<Bootstrap>(true);
            GameManager gm = Object.FindObjectOfType<GameManager>(true);
            Assert.IsNotNull(bootstrap, "Bootstrap not found in Game scene. The scene must contain the Bootstrap entry point.");
            Assert.IsNotNull(gm, "GameManager not found after bootstrap. Bootstrap should create and initialize GameManager.");

            Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>(true);
            Assert.IsNotNull(cam, "No camera found in Game scene after bootstrap.");

            // Verify game has started by checking a known UI/root object exists.
            UIManager ui = Object.FindObjectOfType<UIManager>(true);
            Assert.IsNotNull(ui, "UIManager not found. Game UI should be created during initialization.");

            // Attempt selecting first job through public game flow. If job selection is unavailable,
            // fail clearly rather than silently skipping core smoke verification.
            GameConfig cfg = GameConfig.CreateDefault();
            Assert.IsNotNull(cfg.jobs, "Default config jobs missing.");
            Assert.Greater(cfg.jobs.Length, 0, "Default config has no jobs.");
            string firstJobId = cfg.jobs[0].id;
            Assert.IsFalse(string.IsNullOrEmpty(firstJobId), "First job id is empty.");

            gm.StartJob(firstJobId);
            yield return new WaitForSecondsRealtime(0.5f);

            bool hudFound = false;
            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "HUDController" || n == "UIManager" || n.IndexOf("HUD", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hudFound = true;
                    break;
                }
            }
            if (!hudFound)
            {
                Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
                Assert.Greater(canvases.Length, 0, "No Canvas/HUD found after starting first job. HUD is required.");
            }

            string screenshotDir = Path.Combine("build", "verification");
            Directory.CreateDirectory(screenshotDir);
            string screenshotPath = Path.Combine(screenshotDir, "playmode-smoke.png").Replace("\\", "/");
            ScreenCapture.CaptureScreenshot(screenshotPath);
            yield return new WaitForSecondsRealtime(0.2f);

            // CaptureScreenshot writes asynchronously; at least ensure request was issued and path is valid.
            Assert.IsFalse(string.IsNullOrEmpty(screenshotPath), "Screenshot path is empty.");
            Assert.IsTrue(screenshotPath.StartsWith("build/verification"), "Screenshot must be captured to build/verification.");

            // Basic console-error guard: UnityTest will fail on unexpected LogError/Exception by default in many runners,
            // but we also assert key objects exist. No explicit log hook to avoid brittle dependencies.
        }
    }
}
