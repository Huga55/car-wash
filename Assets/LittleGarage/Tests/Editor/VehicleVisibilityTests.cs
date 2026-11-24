using NUnit.Framework;
using UnityEngine;

namespace LittleGarage.Tests
{
    /// <summary>
    /// STEP 5 rendering diagnostics. These run in real Unity EditMode and check the
    /// geometry/material facts that make the car visible and lightable at runtime.
    /// </summary>
    public class VehicleVisibilityTests
    {
        [Test]
        public void VehicleMeshes_StoreNormalsMatchingTheirVisibleWinding()
        {
            GameObject host = new GameObject("VehicleNormalTest");
            try
            {
                VehicleInstance car = VehicleFactory.Create(GameConfig.CreateDefault().Vehicle("sedan"), host.transform);
                Assert.IsNotNull(car, "Sedan could not be created.");

                MeshFilter[] filters = car.GetComponentsInChildren<MeshFilter>(true);
                int checkedTriangles = 0;
                for (int f = 0; f < filters.Length; f++)
                {
                    Mesh mesh = filters[f] != null ? filters[f].sharedMesh : null;
                    if (mesh == null || mesh.vertices == null || mesh.normals == null || mesh.triangles == null) continue;
                    for (int t = 0; t + 2 < mesh.triangles.Length; t += 3)
                    {
                        int a = mesh.triangles[t];
                        int b = mesh.triangles[t + 1];
                        int c = mesh.triangles[t + 2];
                        if (a >= mesh.vertices.Length || b >= mesh.vertices.Length || c >= mesh.vertices.Length) continue;

                        Vector3 geometric = Vector3.Cross(mesh.vertices[b] - mesh.vertices[a], mesh.vertices[c] - mesh.vertices[a]);
                        if (geometric.sqrMagnitude < 1e-10f) continue;
                        Vector3 stored = (mesh.normals[a] + mesh.normals[b] + mesh.normals[c]).normalized;
                        Assert.Greater(Vector3.Dot(geometric.normalized, stored), 0.45f,
                            "Triangle " + (t / 3) + " in " + filters[f].gameObject.name +
                            " is lit from the opposite side of its visible face.");
                        checkedTriangles++;
                    }
                }
                Assert.Greater(checkedTriangles, 100, "Expected a procedural vehicle with many checked triangles.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WheelOuterSurfaces_FaceAwayFromTheAxle()
        {
            GameObject host = new GameObject("WheelWindingTest");
            try
            {
                VehicleInstance car = VehicleFactory.Create(GameConfig.CreateDefault().Vehicle("sedan"), host.transform);
                MeshFilter[] filters = car.GetComponentsInChildren<MeshFilter>(true);
                int checkedFaces = 0;
                for (int f = 0; f < filters.Length; f++)
                {
                    GameObject go = filters[f] != null ? filters[f].gameObject : null;
                    if (go == null || (go.name != "Tire" && go.name != "Rim")) continue;
                    Mesh mesh = filters[f].sharedMesh;
                    if (mesh == null || mesh.vertices == null || mesh.normals == null || mesh.triangles == null) continue;

                    float maxRadius = 0.01f;
                    for (int v = 0; v < mesh.vertices.Length; v++)
                        maxRadius = Mathf.Max(maxRadius, new Vector2(mesh.vertices[v].x, mesh.vertices[v].y).magnitude);

                    for (int t = 0; t + 2 < mesh.triangles.Length; t += 3)
                    {
                        int a = mesh.triangles[t];
                        int b = mesh.triangles[t + 1];
                        int c = mesh.triangles[t + 2];
                        if (a >= mesh.vertices.Length || b >= mesh.vertices.Length || c >= mesh.vertices.Length) continue;

                        Vector3 centroid = (mesh.vertices[a] + mesh.vertices[b] + mesh.vertices[c]) / 3f;
                        Vector2 radial2 = new Vector2(centroid.x, centroid.y);
                        if (radial2.magnitude < maxRadius * 0.9f) continue; // skip cap rings and inner bore

                        Vector3 normal = (mesh.normals[a] + mesh.normals[b] + mesh.normals[c]).normalized;
                        if (Mathf.Abs(normal.z) > 0.7f) continue; // cap ring, not the tread
                        Vector3 radial = new Vector3(radial2.x, radial2.y, 0f).normalized;
                        Assert.Greater(Vector3.Dot(normal, radial), 0.5f,
                            "Outer wheel face on " + go.name + " is wound inside-out and would be culled.");
                        checkedFaces++;
                    }
                }
                Assert.Greater(checkedFaces, 20, "Expected procedural tire/rim outer faces to be checked.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Progress_DoesNotRevealFutureWashStages()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            SurfaceStateSystem surface = new SurfaceStateSystem();
            surface.Reset(cfg.Vehicle(cfg.jobs[0].vehicleId));

            GameObject host = new GameObject("ProgressHonestyTest");
            try
            {
                WashingSystem wash = host.AddComponent<WashingSystem>();
                // Build the full six-tool sequence while only actually washing stage 1.
                WashTool[] allTools =
                {
                    WashTool.Pressure, WashTool.Foam, WashTool.Sponge,
                    WashTool.Rinse, WashTool.Dryer, WashTool.Polisher
                };
                wash.Begin(null, allTools, surface);
                Assert.AreEqual(WashTool.Pressure, wash.CurrentTool);

                int passes = 0;
                while (wash.CurrentTool == WashTool.Pressure && passes < 20)
                {
                    PaintAll(surface, WashTool.Pressure, 0.18f);
                    wash.Advance(0.25f);
                    passes++;
                }

                Assert.AreEqual(WashTool.Foam, wash.CurrentTool, "Pressure stage should hand over to foam.");
                Assert.Less(wash.Progress, 0.30f,
                    "HUD progress must not count sponge/rinse/polish before those stages start.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void PaintAll(SurfaceStateSystem surface, WashTool tool, float radius)
        {
            const int steps = 20;
            for (int y = 0; y <= steps; y++)
                for (int x = 0; x <= steps; x++)
                    surface.Paint(new Vector2(x / (float)steps, y / (float)steps), radius, tool);
        }
    }
}
