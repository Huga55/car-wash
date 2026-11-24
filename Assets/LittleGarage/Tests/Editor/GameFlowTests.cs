using NUnit.Framework;
using UnityEngine;

namespace LittleGarage.Tests
{
    public class GameFlowTests
    {
        [Test]
        public void DefaultCatalog_HasTenJobsVehiclesAndTools()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            Assert.IsNotNull(cfg, "GameConfig.CreateDefault() returned null.");
            Assert.IsNotNull(cfg.jobs, "Jobs catalog is null.");
            Assert.IsNotNull(cfg.vehicles, "Vehicles catalog is null.");
            Assert.IsNotNull(cfg.tools, "Tools catalog is null.");
            Assert.AreEqual(10, cfg.jobs.Length, "Expected 10 jobs in default catalog.");
            Assert.AreEqual(10, cfg.vehicles.Length, "Expected 10 vehicles in default catalog.");
            Assert.GreaterOrEqual(cfg.tools.Length, 6, "Expected at least 6 wash tools in default catalog.");
        }

        [Test]
        public void DefaultCatalog_JobsReferenceValidVehiclesAndHaveTasks()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            for (int i = 0; i < cfg.jobs.Length; i++)
            {
                JobDef job = cfg.jobs[i];
                Assert.IsNotNull(job, "Job at index " + i + " is null.");
                Assert.IsFalse(string.IsNullOrEmpty(job.id), "Job id is empty at index " + i);
                Assert.IsFalse(string.IsNullOrEmpty(job.vehicleId), "Job vehicleId is empty: " + job.id);
                Assert.IsNotNull(job.tasks, "Job tasks null: " + job.id);
                Assert.Greater(job.tasks.Length, 0, "Job has no tasks: " + job.id);

                VehicleDef vehicle = cfg.Vehicle(job.vehicleId);
                Assert.IsNotNull(vehicle, "Job references missing vehicle: " + job.id + " -> " + job.vehicleId);

                for (int t = 0; t < job.tasks.Length; t++)
                {
                    JobTaskDef task = job.tasks[t];
                    Assert.IsNotNull(task, "Null task in job " + job.id);
                    Assert.IsFalse(string.IsNullOrEmpty(task.titleRu), "Task title missing in job " + job.id);
                    if (task.kind == TaskKind.Wash)
                    {
                        Assert.IsNotNull(task.tools, "Wash task missing tools in job " + job.id);
                        Assert.Greater(task.tools.Length, 0, "Wash task has no tools in job " + job.id);
                    }
                }
            }
        }

        [Test]
        public void ProgressManager_SequentialUnlockAndCompletion()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            SaveSystem save = new SaveSystem();
            save.Clear();

            ProgressManager progress = new ProgressManager(cfg, save);
            Assert.AreEqual(10, cfg.jobs.Length, "Test assumes 10 default jobs.");

            Assert.IsTrue(progress.IsUnlocked(cfg.jobs[0].id), "First job should be unlocked.");
            for (int i = 1; i < cfg.jobs.Length; i++)
            {
                Assert.IsFalse(progress.IsUnlocked(cfg.jobs[i].id), "Job should be locked before previous completion: " + cfg.jobs[i].id);
            }

            progress.Complete(cfg.jobs[0].id, cfg.jobs[0].vehicleId, 100, new VehicleAppearance { Body = Color.red, Rim = 1, Decal = 2 });
            Assert.IsTrue(progress.IsCompleted(cfg.jobs[0].id), "Completed job not marked completed.");
            Assert.IsTrue(progress.IsUnlocked(cfg.jobs[1].id), "Second job should unlock after first completion.");
            if (cfg.jobs.Length > 2)
                Assert.IsFalse(progress.IsUnlocked(cfg.jobs[2].id), "Third job should remain locked until second completion.");

            save.Clear();
        }

        [Test]
        public void SaveSystem_RoundTrip_PreservesAppearanceAndProgress()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            SaveSystem save = new SaveSystem();
            save.Clear();

            ProgressManager progress = new ProgressManager(cfg, save);
            string jobId = cfg.jobs[0].id;
            string vehicleId = cfg.jobs[0].vehicleId;
            VehicleAppearance look = new VehicleAppearance
            {
                Body = new Color(0.1f, 0.2f, 0.3f, 1f),
                Rim = 2,
                Decal = 1
            };

            progress.Complete(jobId, vehicleId, 150, look);

            ProgressManager loaded = new ProgressManager(cfg, save);
            Assert.IsTrue(loaded.IsCompleted(jobId), "Job completion did not round-trip.");
            Assert.GreaterOrEqual(loaded.Stars, 150, "Reward/stars did not round-trip.");

            VehicleAppearance saved = loaded.GetSaved(vehicleId);
            Assert.AreEqual(look.Rim, saved.Rim, "Rim did not round-trip.");
            Assert.AreEqual(look.Decal, saved.Decal, "Decal did not round-trip.");
            Assert.That(saved.Body.r, Is.EqualTo(look.Body.r).Within(0.001f), "Body.r did not round-trip.");
            Assert.That(saved.Body.g, Is.EqualTo(look.Body.g).Within(0.001f), "Body.g did not round-trip.");
            Assert.That(saved.Body.b, Is.EqualTo(look.Body.b).Within(0.001f), "Body.b did not round-trip.");

            save.Clear();
        }

        [Test]
        public void SaveSystem_CorruptData_FallsBackToDefaults()
        {
            SaveSystem save = new SaveSystem();
            save.Clear();
            PlayerPrefs.SetString("LittleGarage.Progress.v1", "{not valid json");
            PlayerPrefs.Save();

            ProgressData data = save.Load();
            Assert.IsNotNull(data, "Load should return fallback data for corrupt JSON.");
            Assert.GreaterOrEqual(data.version, 1, "Fallback data should have valid version.");
            Assert.IsNotNull(data.completedJobs, "Fallback completedJobs should not be null.");
            Assert.IsNotNull(data.appearances, "Fallback appearances should not be null.");

            save.Clear();
        }

        [Test]
        public void SurfaceStateSystem_WashTransitions()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            VehicleDef vehicle = cfg.vehicles != null && cfg.vehicles.Length > 0 ? cfg.vehicles[0] : null;
            Assert.IsNotNull(vehicle, "Default catalog has no vehicle for surface test.");

            SurfaceStateSystem surface = new SurfaceStateSystem();
            surface.Reset(vehicle);

            float initialClean = surface.Cleanliness;
            Assert.Less(initialClean, 1.001f, "Initial cleanliness should be valid.");
            Assert.GreaterOrEqual(initialClean, 0f, "Initial cleanliness should be valid.");

            bool painted = false;
            for (int y = 0; y <= 10 && !painted; y++)
            {
                for (int x = 0; x <= 10; x++)
                {
                    Vector2 uv = new Vector2(x / 10f, y / 10f);
                    if (surface.Paint(uv, 0.2f, WashTool.Pressure))
                    {
                        painted = true;
                    }
                }
            }

            Assert.IsTrue(painted, "SurfaceStateSystem.Paint should affect at least one point on a dirty vehicle.");
            Assert.Greater(surface.Cleanliness, initialClean - 0.0001f, "Cleanliness should not decrease after washing.");

            // Apply foam/rinse/polish tools over broad area to exercise transitions.
            PaintAll(surface, WashTool.Foam, 0.18f);
            Assert.GreaterOrEqual(surface.FoamCoverage, 0f, "FoamCoverage should be readable.");
            PaintAll(surface, WashTool.Sponge, 0.18f);
            PaintAll(surface, WashTool.Rinse, 0.2f);
            PaintAll(surface, WashTool.Dryer, 0.2f);
            PaintAll(surface, WashTool.Polisher, 0.2f);

            Assert.GreaterOrEqual(surface.Cleanliness, 0f);
            Assert.LessOrEqual(surface.Cleanliness, 1.0001f);
            Assert.GreaterOrEqual(surface.Dryness, 0f);
            Assert.LessOrEqual(surface.Dryness, 1.0001f);
            Assert.GreaterOrEqual(surface.Polish, 0f);
            Assert.LessOrEqual(surface.Polish, 1.0001f);
        }

        [Test]
        public void VehiclePart_StateMachineBasics()
        {
            GameObject go = new GameObject("TestPart");
            try
            {
                VehiclePart part = go.AddComponent<VehiclePart>();
                part.Kind = PartKind.WheelFL;

                part.SetBroken(true);
                Assert.IsTrue(part.Broken, "SetBroken(true) should mark part broken.");

                part.SetInstalled(false);
                Assert.IsFalse(part.Installed, "SetInstalled(false) should mark part uninstalled.");

                part.SetInstalled(true);
                Assert.IsTrue(part.Installed, "SetInstalled(true) should mark part installed.");

                part.SetBroken(false);
                Assert.IsFalse(part.Broken, "SetBroken(false) should mark part repaired.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void PaintAll(SurfaceStateSystem surface, WashTool tool, float radius)
        {
            for (int y = 0; y <= 12; y++)
            {
                for (int x = 0; x <= 12; x++)
                {
                    surface.Paint(new Vector2(x / 12f, y / 12f), radius, tool);
                }
            }
        }
    }
}
