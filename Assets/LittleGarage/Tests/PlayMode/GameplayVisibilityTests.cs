using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LittleGarage.Tests
{
    /// <summary>
    /// STEP 5 runtime checks for the active gameplay presentation: camera framing,
    /// vehicle renderers/materials, water stream lifecycle, raycast-to-surface feedback,
    /// and screen isolation. No screenshot or Game View claim is made here.
    /// </summary>
    public class GameplayVisibilityTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [UnityTest]
        public IEnumerator GameplayCamera_IsPerspective_AndFramesTheVehicle()
        {
            yield return StartJobAndSettle();

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing in gameplay.");
            Assert.IsFalse(cam.orthographic,
                "Game.unity starts orthographic; the garage rig must switch to perspective.");

            GameManager game = FindGame();
            Bounds bounds = VehicleBounds(game.CurrentVehicle);
            Assert.Greater(bounds.size.sqrMagnitude, 0.01f, "Vehicle has no renderable bounds.");

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, bounds),
                "Vehicle bounds must be inside the gameplay camera frustum.");
        }

        [UnityTest]
        public IEnumerator PaintRenderers_UseStatefulShader_AndBindTheSurfaceMap()
        {
            yield return StartJobAndSettle();

            GameManager game = FindGame();
            VehicleInstance car = game.CurrentVehicle;
            Assert.IsNotNull(car, "StartJob must create a vehicle.");
            Renderer[] renderers = car.PaintRenderers;
            Assert.Greater(renderers.Length, 0, "No registered paint renderers.");

            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] mats = renderers[i] != null ? renderers[i].sharedMaterials : null;
                Assert.IsNotNull(mats, "Paint renderer has no materials.");
                Assert.Greater(mats.Length, 0, "Paint renderer has an empty material list.");
                Assert.AreEqual("LittleGarage/CarSurface", mats[0].shader.name,
                    "Paint target must use the stateful car shader (also included in the player build).");
                Assert.AreEqual(car.Surface.Map, mats[0].GetTexture("_StateMap"),
                    "State map must be bound to the visible material.");
            }
        }

        [UnityTest]
        public IEnumerator Stream_StartsHidden_FollowsPaintTarget_AndStopsOnPointerUp()
        {
            yield return StartJobAndSettle();

            GameManager game = FindGame();
            WaterStreamFX stream = game.Stream;
            Assert.IsNotNull(stream, "WaterStreamFX was not created during initialization.");
            Assert.IsFalse(stream.IsActive, "Stream must be hidden before interaction.");
            Assert.IsFalse(stream.StreamRenderer.enabled, "Stream LineRenderer must be disabled at rest.");

            Camera cam = Camera.main;
            MeshCollider[] colliders = game.CurrentVehicle.PaintColliders;
            Assert.Greater(colliders.Length, 1, "Need at least two paint targets for a follow test.");

            Vector2 firstScreen = cam.WorldToScreenPoint(colliders[0].bounds.center);
            game.SimulatePointerForTests(firstScreen, true);
            yield return null;

            Assert.IsTrue(stream.IsActive, "Stream must activate on pointer down.");
            Assert.IsTrue(stream.StreamRenderer.enabled, "Stream LineRenderer must be enabled.");
            Assert.IsTrue(stream.NozzleRenderer.enabled, "Nozzle visual must be enabled.");
            Vector3 firstTarget = stream.Target;

            Vector2 secondScreen = cam.WorldToScreenPoint(colliders[colliders.Length / 2].bounds.center);
            game.SimulatePointerMoveForTests(secondScreen);
            yield return null;

            Assert.IsTrue(stream.IsActive, "Stream must remain active while the pointer is held.");
            Assert.Greater(Vector3.Distance(firstTarget, stream.Target), 0.01f,
                "Stream target must follow the raycast hit.");

            game.SimulatePointerForTests(secondScreen, false);
            Assert.IsFalse(stream.IsActive, "Stream must hide when interaction stops.");
            Assert.IsFalse(stream.StreamRenderer.enabled, "Stream LineRenderer must hide on pointer up.");
        }

        [UnityTest]
        public IEnumerator HeldPointer_ContinuesCleaning_AndHidesInstructionAfterFirstStroke()
        {
            yield return StartJobAndSettle();

            GameManager game = FindGame();
            UIManager ui = Object.FindObjectOfType<UIManager>(true);
            Assert.IsNotNull(ui, "UIManager missing.");
            Assert.IsTrue(ui.InstructionVisible, "Instruction should be visible before first interaction.");

            SurfaceStateSystem surface = game.CurrentVehicle.Surface;
            float before = surface.Cleanliness;
            Camera cam = Camera.main;
            Vector2 screen = cam.WorldToScreenPoint(game.CurrentVehicle.PaintColliders[0].bounds.center);

            game.SimulatePointerForTests(screen, true);
            yield return null;
            Assert.IsTrue(game.Stream.IsActive, "Held wash must keep the stream active.");

            for (int i = 0; i < 6; i++)
            {
                game.RefreshHeldPointerForTests(0.05f);
                yield return null;
            }

            // Cleanliness is 1 - average dirt, so successful washing raises it.
            Assert.Greater(surface.Cleanliness, before,
                "A stationary held pointer must keep cleaning, not just the initial move event.");
            Assert.IsFalse(ui.InstructionVisible, "Instruction must hide after the first successful interaction.");
            Assert.AreEqual(0, ui.VisibleJobCardCount, "Task selection must stay hidden during gameplay.");

            game.SimulatePointerForTests(screen, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScreen_KeepsExactlyOnePrimaryScreen_AndNoJobCards()
        {
            yield return StartJobAndSettle();

            UIManager ui = Object.FindObjectOfType<UIManager>(true);
            Assert.IsTrue(ui.ActivePrimaryScreen.HasValue, "Exactly one primary screen must be active.");
            Assert.AreEqual(ScreenId.Play, ui.ActivePrimaryScreen.Value);
            Assert.IsFalse(ui.IsPrimaryScreenActive(ScreenId.Garage));
            Assert.AreEqual(0, ui.VisibleJobCardCount, "Task-selection cards leaked into gameplay.");
            Assert.AreEqual(1, Object.FindObjectsOfType<Canvas>(true).Length, "Duplicate Canvas in gameplay.");
        }

        private static IEnumerator StartJobAndSettle()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Required scene missing: " + ScenePath);
            while (!load.isDone) yield return null;
            yield return null;

            GameManager game = FindGame();
            GameConfig cfg = GameConfig.CreateDefault();
            game.StartJob(cfg.jobs[0].id);

            // GarageCamera positions the rig in LateUpdate; allow it to settle.
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static GameManager FindGame()
        {
            GameManager game = Object.FindObjectOfType<GameManager>(true);
            Assert.IsNotNull(game, "GameManager missing from Game scene.");
            return game;
        }

        private static Bounds VehicleBounds(VehicleInstance car)
        {
            Assert.IsNotNull(car, "Vehicle missing after StartJob.");
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, "Vehicle has no renderers.");

            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude < 0.000001f) continue;
                if (!hasBounds) { bounds = b; hasBounds = true; }
                else bounds.Encapsulate(b);
            }
            Assert.IsTrue(hasBounds, "No enabled vehicle renderer produced bounds.");
            return bounds;
        }
    }
}
