using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LittleGarage.Tests
{
    /// <summary>
    /// Deterministic Play Mode captures published through the existing workflow's
    /// build/verification artifact path. Unlike ScreenCapture.CaptureScreenshot,
    /// rendering a camera directly into a RenderTexture works in headless CI.
    /// The canvas is temporarily switched to ScreenSpaceCamera so the HUD is included.
    /// These files are camera renders, not a claim about Unity's Game View window.
    /// </summary>
    public class GameplayVisualCaptureTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [UnityTest]
        public IEnumerator CapturesGameplayAtRequiredAspectRatios()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Required scene missing: " + ScenePath);
            while (!load.isDone) yield return null;
            yield return null;

            GameManager game = Object.FindObjectOfType<GameManager>(true);
            Assert.IsNotNull(game, "GameManager missing.");
            GameConfig cfg = GameConfig.CreateDefault();
            game.StartJob(cfg.jobs[0].id);
            for (int i = 0; i < 5; i++) yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing.");
            Assert.IsFalse(cam.orthographic, "Perspective camera required for gameplay capture.");

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            RenderMode[] oldModes = new RenderMode[canvases.Length];
            Camera[] oldCameras = new Camera[canvases.Length];
            float[] oldPlanes = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                oldModes[i] = canvases[i].renderMode;
                oldCameras[i] = canvases[i].worldCamera;
                oldPlanes[i] = canvases[i].planeDistance;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 1f;
            }
            yield return null; // let the canvas rebuild for ScreenSpaceCamera

            try
            {
                Capture(cam, 1280, 720, "gameplay-16x9.png");
                Capture(cam, 1280, 800, "gameplay-16x10.png");
                Capture(cam, 1024, 768, "gameplay-4x3.png");
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null) continue;
                    canvases[i].renderMode = oldModes[i];
                    canvases[i].worldCamera = oldCameras[i];
                    canvases[i].planeDistance = oldPlanes[i];
                }
            }
        }

        [UnityTest]
        public IEnumerator CapturesActiveStreamAndWashedSurface()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Required scene missing: " + ScenePath);
            while (!load.isDone) yield return null;
            yield return null;

            GameManager game = Object.FindObjectOfType<GameManager>(true);
            Assert.IsNotNull(game, "GameManager missing.");
            GameConfig cfg = GameConfig.CreateDefault();
            game.StartJob(cfg.jobs[0].id);
            for (int i = 0; i < 5; i++) yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing.");
            MeshCollider[] colliders = game.CurrentVehicle.PaintColliders;
            Assert.Greater(colliders.Length, 0, "No paint colliders to aim at.");

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            RenderMode[] oldModes = new RenderMode[canvases.Length];
            Camera[] oldCameras = new Camera[canvases.Length];
            float[] oldPlanes = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                oldModes[i] = canvases[i].renderMode;
                oldCameras[i] = canvases[i].worldCamera;
                oldPlanes[i] = canvases[i].planeDistance;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 1f;
            }
            yield return null;

            try
            {
                Vector2 screen = game.ScreenPointForWorldForTests(colliders[0].bounds.center);
                game.SimulatePointerForTests(screen, true);
                for (int i = 0; i < 4; i++)
                {
                    game.RefreshHeldPointerForTests(0.05f);
                    yield return null;
                }

                Assert.IsTrue(game.Stream.IsActive, "Stream must be active for the stream capture.");
                Assert.IsTrue(game.Stream.StreamRenderer.enabled, "Stream renderer must be enabled for capture.");
                Capture(cam, 1280, 720, "gameplay-stream.png");

                game.SimulatePointerForTests(screen, false);
                yield return null;
                Capture(cam, 1280, 720, "gameplay-washed.png");
            }
            finally
            {
                game.SimulatePointerForTests(Vector2.zero, false);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null) continue;
                    canvases[i].renderMode = oldModes[i];
                    canvases[i].worldCamera = oldCameras[i];
                    canvases[i].planeDistance = oldPlanes[i];
                }
            }
        }

        private static void Capture(Camera cam, int width, int height, string fileName)
        {
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = rt;
                RenderTexture.active = rt;
                cam.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                byte[] png = texture.EncodeToPNG();
                Assert.Greater(png.Length, 1000, fileName + " did not encode a rendered frame.");

                string dir = Path.Combine("build", "verification");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName).Replace("\\", "/");
                File.WriteAllBytes(path, png);

                Assert.IsTrue(File.Exists(path), "Rendered gameplay capture missing: " + path);
                Assert.Greater(new FileInfo(path).Length, 1000L, "Rendered gameplay capture is empty: " + path);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(texture);
                Object.Destroy(rt);
            }
        }
    }
}
