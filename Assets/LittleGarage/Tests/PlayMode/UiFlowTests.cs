using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LittleGarage.Tests
{
    /// <summary>
    /// Runtime regression coverage for the STEP 4 screen-management contract.
    /// These tests assert UI state exposed by UIManager/GameManager; they do not
    /// claim that Game View layout or physical-device appearance was verified.
    /// </summary>
    public class UiFlowTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            new SaveSystem().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator ShowGarage_HidesMainMenu_AndLeavesExactlyOnePrimaryScreen()
        {
            yield return LoadGameScene();
            UIManager ui = FindUi();

            ui.ShowGarage(null);
            yield return null;

            Assert.AreEqual(ScreenId.Garage, ui.ActivePrimaryScreen);
            Assert.IsTrue(ui.IsPrimaryScreenActive(ScreenId.Garage));
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Home));
            Assert.AreEqual(UiOverlay.None, ui.ActiveOverlay);
        }

        [UnityTest]
        public IEnumerator StartJob_HidesTaskSelection_AndShowsOnlyGameplay()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();
            string jobId = FirstJobId();

            game.ShowGarage();
            Assert.IsTrue(ui.IsPrimaryScreenActive(ScreenId.Garage));
            Assert.Greater(ui.VisibleJobCardCount, 0, "Task-selection cards should be visible in Garage.");

            game.StartJob(jobId);
            yield return null;

            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen);
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Garage), "Task selection must not remain behind gameplay.");
            Assert.AreEqual(0, ui.VisibleJobCardCount, "No task-selection cards may remain active during gameplay.");
            Assert.IsTrue(ui.IsPrimaryScreenActive(ScreenId.Play));
        }

        [UnityTest]
        public IEnumerator Pause_DoesNotActivateAnotherPrimaryScreen_AndResumeRestoresGameplay()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();

            game.StartJob(FirstJobId());
            yield return null;

            game.SetPaused(true);
            yield return null;

            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen);
            Assert.IsTrue(ui.IsPrimaryScreenActive(ScreenId.Play));
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Home));
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Garage));
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Complete));
            Assert.IsTrue(ui.IsOverlayActive(UiOverlay.Pause));
            Assert.IsTrue(game.IsPaused);
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f), "Pause must stop scaled game time.");

            game.SetPaused(false);
            yield return null;

            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen);
            Assert.AreEqual(UiOverlay.None, ui.ActiveOverlay);
            Assert.IsFalse(game.IsPaused);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f), "Resume must restore scaled game time.");
        }

        [UnityTest]
        public IEnumerator Restart_KeepsOneCanvasOneEventSystem_AndOneToolButtonPerTool()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();
            string jobId = FirstJobId();

            game.StartJob(jobId);
            yield return null;

            int canvasCount = Object.FindObjectsOfType<Canvas>(true).Length;
            int eventSystemCount = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>(true).Length;
            int toolCount = ui.ActiveToolButtonCount;
            Assert.Greater(toolCount, 0, "First wash task should expose tool buttons.");

            // Restart action is StartJob(_currentJobId) in GameManager.
            game.StartJob(jobId);
            yield return null;
            game.StartJob(jobId);
            yield return null;

            Assert.AreEqual(canvasCount, Object.FindObjectsOfType<Canvas>(true).Length, "Restart duplicated the Canvas.");
            Assert.AreEqual(eventSystemCount,
                Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>(true).Length,
                "Restart duplicated the EventSystem.");
            Assert.AreEqual(toolCount, ui.ActiveToolButtonCount, "Restart duplicated or removed tool buttons.");
            Assert.AreEqual(0, CountObjectsNamed("Tool_", exactPrefix: true) - toolCount,
                "Deferred tool-button destruction left stale UI objects in the hierarchy.");
            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen);
        }

        [UnityTest]
        public IEnumerator ReturnHome_ClearsGameplayOverlays_AndRestoresMainMenu()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();

            game.StartJob(FirstJobId());
            yield return null;
            game.SetPaused(true);
            yield return null;
            Assert.IsTrue(ui.IsOverlayActive(UiOverlay.Pause));

            game.ShowHome();
            yield return null;

            Assert.AreEqual(ScreenId.Home, ui.ActivePrimaryScreen);
            Assert.AreEqual(UiOverlay.None, ui.ActiveOverlay);
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Play));
            Assert.AreEqual(0, ui.ActiveToolButtonCount);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator Finale_KeepsGameplayVisible_UntilResultIsShown()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();

            game.StartJob(FirstJobId());
            yield return null;
            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen);

            game.CompleteJobForTests();
            yield return null;

            Assert.AreEqual(ScreenId.Complete, game.Screen, "Job completion must enter the Complete logical state.");
            Assert.IsTrue(game.IsFinaleActive, "Finale coroutine must own the delay before the result screen.");
            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen,
                "Result screen must not appear while the finale is still running.");
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Complete));
            Assert.AreEqual(0, ui.ActiveToolButtonCount, "Gameplay controls must be hidden for the finale.");

            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.IsFinaleActive && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsFalse(game.IsFinaleActive, "Finale did not finish within the test timeout.");
            Assert.AreEqual(ScreenId.Complete, ui.ActivePrimaryScreen, "Result screen must appear after the finale.");
            Assert.IsTrue(ui.IsPrimaryScreenActive(ScreenId.Complete));
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Play));
        }

        [UnityTest]
        public IEnumerator AllTransitions_MaintainExactlyOnePrimaryScreen()
        {
            yield return LoadGameScene();
            GameManager game = FindGame();
            UIManager ui = FindUi();
            string jobId = FirstJobId();

            AssertPrimary(ui, ScreenId.Home);
            game.ShowGarage();
            yield return null;
            AssertPrimary(ui, ScreenId.Garage);
            game.StartJob(jobId);
            yield return null;
            AssertPrimary(ui, ScreenId.Play);
            game.SetPaused(true);
            yield return null;
            AssertPrimary(ui, ScreenId.Play);
            game.SetPaused(false);
            yield return null;
            AssertPrimary(ui, ScreenId.Play);
            game.ShowHome();
            yield return null;
            AssertPrimary(ui, ScreenId.Home);
        }

        private static void AssertPrimary(UIManager ui, ScreenId expected)
        {
            Assert.IsTrue(ui.ActivePrimaryScreen.HasValue,
                "Exactly one primary screen must be active; found " +
                (ui.ActivePrimaryScreen.HasValue ? ui.ActivePrimaryScreen.Value.ToString() : "none/multiple"));
            Assert.AreEqual(expected, ui.ActivePrimaryScreen.Value);
        }

        private static IEnumerator LoadGameScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Required scene missing: " + ScenePath);
            while (!load.isDone) yield return null;
            yield return null;
        }

        private static GameManager FindGame()
        {
            GameManager game = Object.FindObjectOfType<GameManager>(true);
            Assert.IsNotNull(game, "GameManager missing from Game scene.");
            return game;
        }

        private static UIManager FindUi()
        {
            UIManager ui = Object.FindObjectOfType<UIManager>(true);
            Assert.IsNotNull(ui, "UIManager missing from Game scene.");
            return ui;
        }

        private static string FirstJobId()
        {
            GameConfig config = GameConfig.CreateDefault();
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.jobs);
            Assert.Greater(config.jobs.Length, 0);
            return config.jobs[0].id;
        }

        private static int CountObjectsNamed(string prefix, bool exactPrefix)
        {
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null) continue;
                string name = transforms[i].name;
                if (exactPrefix ? name.StartsWith(prefix, System.StringComparison.Ordinal) : name == prefix) count++;
            }
            return count;
        }
    }
}
