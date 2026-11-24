using System;
using NUnit.Framework;
using UnityEngine;

namespace LittleGarage.Tests
{
    /// <summary>
    /// Behavioral EditMode coverage for the single-car wash vertical slice.
    /// These tests exercise real state transitions rather than only catalog shape.
    /// </summary>
    public class WashCycleTests
    {
        private static readonly WashTool[] ExpectedSequence =
        {
            WashTool.Pressure, WashTool.Foam, WashTool.Sponge,
            WashTool.Rinse, WashTool.Dryer, WashTool.Polisher
        };

        [Test]
        public void FirstJob_HasCompleteSixStageWashCycle()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            Assert.IsNotNull(cfg, "Default config missing.");
            Assert.Greater(cfg.jobs.Length, 0, "Config has no jobs.");

            JobDef first = cfg.jobs[0];
            Assert.IsNotNull(first.tasks, "First job has no tasks.");
            Assert.AreEqual(ExpectedSequence.Length, first.tasks.Length,
                "First job must contain all six wash stages (vertical-slice requirement).");

            for (int i = 0; i < ExpectedSequence.Length; i++)
            {
                JobTaskDef task = first.tasks[i];
                Assert.AreEqual(TaskKind.Wash, task.kind, "Stage " + (i + 1) + " must be a wash task.");
                Assert.IsNotNull(task.tools, "Stage " + (i + 1) + " has no tools.");
                Assert.AreEqual(1, task.tools.Length, "Stage " + (i + 1) + " should use exactly one tool.");
                Assert.AreEqual(ExpectedSequence[i], task.tools[0],
                    "Stage " + (i + 1) + " must use " + ExpectedSequence[i] + ".");
            }
        }

        [Test]
        public void VehicleFactory_CreatesSedanWithFullUvCoverage()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            VehicleDef sedan = cfg.Vehicle("sedan");
            Assert.IsNotNull(sedan, "Sedan definition missing.");

            GameObject host = new GameObject("VehicleFactoryTest");
            try
            {
                VehicleInstance car = VehicleFactory.Create(sedan, host.transform);
                Assert.IsNotNull(car, "VehicleFactory.Create returned null.");
                Assert.GreaterOrEqual(car.PaintColliders.Length, 8,
                    "Vehicle should expose at least 8 paintable colliders.");
                Assert.GreaterOrEqual(car.Parts.Count, 6, "Vehicle should register serviceable parts.");
                Assert.IsTrue(StateMapCovered(car),
                    "Paint-mesh UVs must cover the full 256x256 state map so every pixel is reachable.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FullWashCycle_TransitionsSurfaceAndCompletesOncePerStage()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            JobDef job = cfg.jobs[0];
            VehicleDef vehicle = cfg.Vehicle(job.vehicleId);
            Assert.IsNotNull(vehicle, "First job vehicle missing.");

            SurfaceStateSystem surface = new SurfaceStateSystem();
            surface.Reset(vehicle);
            Assert.Less(surface.Cleanliness, 0.999f, "Vehicle should start dirty.");

            GameObject host = new GameObject("WashCycleTest");
            try
            {
                WashingSystem wash = host.AddComponent<WashingSystem>();
                int completions = 0;
                int toolChanges = 0;
                wash.Completed += () => completions++;
                wash.ToolChanged += t => toolChanges++;

                for (int stage = 0; stage < job.tasks.Length; stage++)
                {
                    JobTaskDef task = job.tasks[stage];
                    int before = completions;
                    wash.Begin(null, task.tools, surface);

                    int passes = 0;
                    while (!wash.IsComplete && passes < 20)
                    {
                        PaintAll(surface, wash.CurrentTool, 0.18f);
                        wash.Advance(0.25f);
                        passes++;
                    }

                    Assert.IsTrue(wash.IsComplete,
                        "Stage " + (stage + 1) + " (" + task.titleRu + ") never completed.");
                    Assert.AreEqual(before + 1, completions,
                        "Stage " + (stage + 1) + " must fire Completed exactly once.");
                }

                Assert.AreEqual(job.tasks.Length, completions, "One completion per stage expected.");
                Assert.AreEqual(job.tasks.Length, toolChanges, "One tool selection per stage expected.");
                Assert.Greater(surface.Cleanliness, 0.999f, "Wash should leave body clean.");
                Assert.Greater(surface.Dryness, 0.999f, "Wash should leave body dry.");
                Assert.Greater(surface.Polish, 0.999f, "Wash should leave body polished.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WashingSystem_DoesNotAllowSkippingAhead()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            VehicleDef vehicle = cfg.Vehicle(cfg.jobs[0].vehicleId);
            SurfaceStateSystem surface = new SurfaceStateSystem();
            surface.Reset(vehicle);

            GameObject host = new GameObject("ToolLockTest");
            try
            {
                WashingSystem wash = host.AddComponent<WashingSystem>();
                WashTool[] tools = { WashTool.Pressure, WashTool.Foam, WashTool.Sponge };
                wash.Begin(null, tools, surface);
                Assert.AreEqual(WashTool.Pressure, wash.CurrentTool, "First tool should be active on Begin.");

                wash.SetTool(WashTool.Sponge);
                Assert.AreEqual(WashTool.Pressure, wash.CurrentTool, "Skipping ahead to a later tool must be blocked.");

                wash.SetTool(WashTool.Foam);
                Assert.AreEqual(WashTool.Foam, wash.CurrentTool, "Selecting the immediate next tool is allowed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AutoFinish_ForcesCleanDryPolishedSurface()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            VehicleDef vehicle = cfg.Vehicle(cfg.jobs[0].vehicleId);
            SurfaceStateSystem surface = new SurfaceStateSystem();
            surface.Reset(vehicle);

            surface.BeginAutoFinish();
            for (int i = 0; i < 40 && !surface.IsWashFinished(); i++)
                surface.AdvanceAutoFinish(0.1f);

            Assert.IsTrue(surface.IsWashFinished(), "Auto-finish should terminate.");
            Assert.Greater(surface.Cleanliness, 0.999f, "Auto-finish must remove all dirt.");
            Assert.Greater(surface.Dryness, 0.999f, "Auto-finish must remove all wetness.");
            Assert.Greater(surface.Polish, 0.999f, "Auto-finish must apply final polish.");
        }

        private static void PaintAll(SurfaceStateSystem surface, WashTool tool, float radius)
        {
            const int steps = 20;
            for (int y = 0; y <= steps; y++)
            {
                for (int x = 0; x <= steps; x++)
                    surface.Paint(new Vector2(x / (float)steps, y / (float)steps), radius, tool);
            }
        }

        private static bool StateMapCovered(VehicleInstance car)
        {
            const int grid = 16;
            bool[] cells = new bool[grid * grid];
            MeshCollider[] colliders = car.PaintColliders;
            for (int c = 0; c < colliders.Length; c++)
            {
                MeshCollider collider = colliders[c];
                Mesh mesh = collider != null ? collider.sharedMesh : null;
                Vector2[] uv = mesh != null ? mesh.uv : null;
                if (uv == null || uv.Length == 0) continue;

                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < uv.Length; i++)
                {
                    minX = Mathf.Min(minX, uv[i].x);
                    maxX = Mathf.Max(maxX, uv[i].x);
                    minY = Mathf.Min(minY, uv[i].y);
                    maxY = Mathf.Max(maxY, uv[i].y);
                }

                for (int y = 0; y < grid; y++)
                {
                    for (int x = 0; x < grid; x++)
                    {
                        float px = (x + 0.5f) / grid;
                        float py = (y + 0.5f) / grid;
                        if (px >= minX && px <= maxX && py >= minY && py <= maxY)
                            cells[y * grid + x] = true;
                    }
                }
            }

            int covered = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i]) covered++;
            return covered == cells.Length;
        }
    }
}
