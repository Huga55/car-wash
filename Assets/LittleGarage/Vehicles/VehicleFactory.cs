using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    public static class VehicleFactory
    {
        private sealed class BuildCtx
        {
            public VehicleDef Def;
            public VehicleInstance Instance;
            public Transform Root;
            public Material BodyMat;
            public Material GlassMat;
            public Material DarkMat;
            public Material ChromeMat;
            public Material TireMat;
            public Material LightMat;
            public Material TailMat;
            public Material InteriorMat;
            public Material AccentMat;
            public float L;
            public float W;
            public float H;
            public float WheelR;
            public float WheelW;
        }

        private struct MeshData
        {
            public List<Vector3> V;
            public List<Vector3> N;
            public List<Vector2> Uv;
            public List<int> T;

            public static MeshData Create()
            {
                MeshData d;
                d.V = new List<Vector3>(256);
                d.N = new List<Vector3>(256);
                d.Uv = new List<Vector2>(256);
                d.T = new List<int>(512);
                return d;
            }

            public void Clear()
            {
                V.Clear(); N.Clear(); Uv.Clear(); T.Clear();
            }

            public Mesh ToMesh(string name)
            {
                Mesh m = new Mesh();
                m.name = name;
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, Uv);
                m.SetTriangles(T, 0);
                if (N.Count != V.Count) m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        public static VehicleInstance Create(VehicleDef def, Transform parent)
        {
            if (def == null) def = FallbackDef();

            GameObject rootGo = new GameObject("Vehicle_" + (string.IsNullOrEmpty(def.id) ? def.kind.ToString() : def.id));
            if (parent != null) rootGo.transform.SetParent(parent, false);

            VehicleInstance instance = rootGo.AddComponent<VehicleInstance>();
            Transform root = rootGo.transform;

            BuildCtx ctx = new BuildCtx();
            ctx.Def = def;
            ctx.Instance = instance;
            ctx.Root = root;
            ctx.L = Mathf.Max(2.5f, def.length);
            ctx.W = Mathf.Max(1.2f, def.width);
            ctx.H = Mathf.Max(1.0f, def.height);
            ctx.WheelR = Mathf.Clamp(def.wheelRadius, 0.22f, 0.8f);
            ctx.WheelW = Mathf.Clamp(ctx.WheelR * 0.62f, 0.12f, 0.5f);

            CreateMaterials(ctx);
            Transform hoodPivot = BuildBody(ctx);
            BuildBodyDetails(ctx);
            BuildInterior(ctx);
            BuildLights(ctx);
            BuildBumpersMirrors(ctx);
            BuildWheels(ctx);
            BuildServiceParts(ctx, hoodPivot);

            instance.Initialize(def, root, hoodPivot);
            instance.SetBodyColor(def.bodyColor);
            instance.SetRimStyle(0);
            instance.SetDecal(0);

            return instance;
        }

        private static VehicleDef FallbackDef()
        {
            VehicleDef d = ScriptableObject.CreateInstance<VehicleDef>();
            d.id = "fallback";
            d.displayNameRu = "Седан";
            d.kind = VehicleKind.Sedan;
            d.bodyColor = new Color(0.3f, 0.6f, 1f);
            d.accentColor = Color.white;
            d.length = 4.4f; d.width = 1.85f; d.height = 1.45f; d.wheelRadius = 0.34f;
            d.serviceableParts = new[] { PartKind.WheelFL, PartKind.Engine, PartKind.Bumper, PartKind.HeadlightL };
            d.cameraDistance = 7.2f;
            return d;
        }

        private static void CreateMaterials(BuildCtx ctx)
        {
            ctx.BodyMat = MakeMat("BodyMat", ctx.Def.bodyColor, 0.08f, 0.72f);
            ctx.GlassMat = MakeMat("GlassMat", new Color(0.65f, 0.82f, 0.92f, 0.35f), 0.0f, 0.95f, true);
            ctx.DarkMat = MakeMat("DarkMat", new Color(0.12f, 0.13f, 0.15f, 1f), 0.15f, 0.45f);
            ctx.ChromeMat = MakeMat("ChromeMat", new Color(0.82f, 0.84f, 0.88f, 1f), 0.9f, 0.85f);
            ctx.TireMat = MakeMat("TireMat", new Color(0.08f, 0.08f, 0.09f, 1f), 0.0f, 0.25f);
            ctx.LightMat = MakeMat("LightMat", new Color(1f, 0.98f, 0.85f, 1f), 0.1f, 0.9f);
            ctx.TailMat = MakeMat("TailMat", new Color(0.9f, 0.12f, 0.1f, 1f), 0.1f, 0.85f);
            ctx.InteriorMat = MakeMat("InteriorMat", new Color(0.2f, 0.2f, 0.22f, 1f), 0.0f, 0.25f);
            ctx.AccentMat = MakeMat("AccentMat", ctx.Def.accentColor, 0.05f, 0.65f);
        }

        private static Material MakeMat(string name, Color color, float metallic, float smoothness, bool transparent = false)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material m = new Material(shader);
            m.name = name;
            SetMatColor(m, color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (transparent)
            {
                if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            return m;
        }

        private static void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static Transform BuildBody(BuildCtx ctx)
        {
            VehicleKind kind = ctx.Def.kind;
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;

            float baseY = wr * 0.55f;
            float bodyH = h * 0.34f;
            float cabinH = h * 0.36f;
            float cabinL = l * 0.44f;
            float cabinW = w * 0.86f;
            float cabinX = -l * 0.04f;

            if (kind == VehicleKind.Sports) { bodyH = h * 0.28f; cabinH = h * 0.24f; cabinL = l * 0.36f; cabinX = -l * 0.08f; baseY = wr * 0.45f; }
            if (kind == VehicleKind.Pickup) { cabinL = l * 0.34f; cabinX = -l * 0.12f; }
            if (kind == VehicleKind.FireTruck || kind == VehicleKind.Ambulance || kind == VehicleKind.Truck || kind == VehicleKind.Bus || kind == VehicleKind.DumpTruck)
            { bodyH = h * 0.30f; cabinH = h * 0.40f; cabinL = l * 0.28f; cabinX = l * 0.26f; cabinW = w * 0.92f; baseY = wr * 0.60f; }
            if (kind == VehicleKind.Bus) { cabinL = l * 0.82f; cabinX = 0f; cabinH = h * 0.52f; bodyH = h * 0.22f; }
            if (kind == VehicleKind.SUV) { bodyH = h * 0.36f; cabinH = h * 0.34f; cabinL = l * 0.52f; }

            float bodyY = baseY + bodyH * 0.5f;
            float cabinY = baseY + bodyH + cabinH * 0.5f;

            // Lower body shell
            MeshData body = MeshData.Create();
            AddBeveledBox(body, Vector3.zero, new Vector3(l * 0.96f, bodyH, w * 0.96f), l * 0.03f, AtlasRect(0f, 0f, 0.5f, 0.5f));
            AddWheelArches(body, ctx, baseY, bodyH);
            CreatePaintMesh(ctx, "Body", body, ctx.BodyMat, new Vector3(0f, bodyY, 0f));

            // Cabin / roof shell
            MeshData cabin = MeshData.Create();
            Vector3 cabinSize = new Vector3(cabinL, cabinH, cabinW);
            AddBeveledBox(cabin, Vector3.zero, cabinSize, l * 0.02f, AtlasRect(0.5f, 0f, 0.5f, 0.5f));
            CreatePaintMesh(ctx, "Cabin", cabin, ctx.BodyMat, new Vector3(cabinX, cabinY, 0f));

            // Roof top detail
            MeshData roof = MeshData.Create();
            AddBeveledBox(roof, Vector3.zero, new Vector3(cabinL * 0.96f, cabinH * 0.18f, cabinW * 0.92f), l * 0.015f, AtlasRect(0.5f, 0.5f, 0.25f, 0.25f));
            CreatePaintMesh(ctx, "Roof", roof, ctx.BodyMat, new Vector3(cabinX, cabinY + cabinH * 0.45f, 0f));

            // Hood - separate pivoted part
            GameObject hoodPivotGo = new GameObject("HoodPivot");
            hoodPivotGo.transform.SetParent(ctx.Root, false);
            float hoodL = l * 0.28f;
            float hoodW = w * 0.88f;
            float hoodHh = bodyH * 0.22f;
            float hoodX = l * 0.30f;
            float hoodY = baseY + bodyH * 0.9f;
            hoodPivotGo.transform.localPosition = new Vector3(hoodX - hoodL * 0.5f, hoodY, 0f);
            MeshData hood = MeshData.Create();
            AddBeveledBox(hood, Vector3.zero, new Vector3(hoodL, hoodHh, hoodW), l * 0.012f, AtlasRect(0.75f, 0.5f, 0.25f, 0.25f));
            CreatePaintMesh(ctx, "Hood", hood, ctx.BodyMat, new Vector3(hoodL * 0.5f, 0f, 0f), hoodPivotGo.transform);

            // Trunk/rear deck
            MeshData rear = MeshData.Create();
            float rearL = l * 0.22f;
            AddBeveledBox(rear, Vector3.zero, new Vector3(rearL, bodyH * 0.22f, w * 0.86f), l * 0.01f, AtlasRect(0f, 0.5f, 0.25f, 0.25f));
            CreatePaintMesh(ctx, "RearDeck", rear, ctx.BodyMat, new Vector3(-l * 0.33f, baseY + bodyH * 0.88f, 0f));

            BuildKindSpecific(ctx, baseY, bodyH, cabinX, cabinY, cabinL, cabinH, cabinW);
            BuildGlass(ctx, baseY, bodyH, cabinX, cabinY, cabinL, cabinH, cabinW);

            return hoodPivotGo.transform;
        }

        /// <summary>
        /// Adds visible body panels that also complete the UV atlas. The paint meshes below
        /// partition the 256x256 state texture without gaps, so every surface pixel is reachable.
        /// </summary>
        private static void BuildBodyDetails(BuildCtx ctx)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;
            bool sports = ctx.Def.kind == VehicleKind.Sports;
            bool heavy = ctx.Def.kind == VehicleKind.FireTruck || ctx.Def.kind == VehicleKind.Ambulance ||
                         ctx.Def.kind == VehicleKind.Truck || ctx.Def.kind == VehicleKind.Bus ||
                         ctx.Def.kind == VehicleKind.DumpTruck;
            float baseY = wr * (sports ? 0.45f : heavy ? 0.60f : 0.55f);
            float bodyH = h * (sports ? 0.28f : heavy ? 0.30f : 0.34f);
            float sideZ = w * 0.492f;
            float panelT = Mathf.Max(0.012f, w * 0.014f);

            CreateSidePanel(ctx, "DoorL", new Vector3(l * 0.27f, bodyH * 0.58f, panelT),
                new Vector3(l * 0.01f, baseY + bodyH * 0.54f, sideZ), AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
            CreateSidePanel(ctx, "DoorR", new Vector3(l * 0.27f, bodyH * 0.58f, panelT),
                new Vector3(l * 0.01f, baseY + bodyH * 0.54f, -sideZ), AtlasRect(0f, 0.75f, 0.25f, 0.25f));
            CreateSidePanel(ctx, "FrontFenderL", new Vector3(l * 0.18f, bodyH * 0.48f, panelT),
                new Vector3(l * 0.31f, baseY + bodyH * 0.50f, sideZ), AtlasRect(0.25f, 0.75f, 0.25f, 0.25f));
            CreateSidePanel(ctx, "FrontFenderR", new Vector3(l * 0.18f, bodyH * 0.48f, panelT),
                new Vector3(l * 0.31f, baseY + bodyH * 0.50f, -sideZ), AtlasRect(0.5f, 0.75f, 0.25f, 0.25f));
            CreateSidePanel(ctx, "RockerPanel", new Vector3(l * 0.56f, bodyH * 0.16f, w * 0.99f),
                new Vector3(-l * 0.02f, baseY + bodyH * 0.10f, 0f), AtlasRect(0.75f, 0.75f, 0.25f, 0.25f));

            BuildWheelArchTrims(ctx, sideZ);
            BuildGrille(ctx, baseY, bodyH);
            BuildDoorHandles(ctx, baseY, bodyH, sideZ);
        }

        private static void CreateSidePanel(BuildCtx ctx, string name, Vector3 size, Vector3 pos, Rect uv)
        {
            MeshData data = MeshData.Create();
            AddBeveledBox(data, Vector3.zero, size, Mathf.Min(0.012f, size.y * 0.12f), uv);
            CreatePaintMesh(ctx, name, data, ctx.BodyMat, pos);
        }

        private static void BuildWheelArchTrims(BuildCtx ctx, float sideZ)
        {
            float l = ctx.L, wr = ctx.WheelR;
            float frontX = l * 0.32f, rearX = -l * 0.32f;
            float radius = wr * 1.12f;
            float thickness = Mathf.Max(0.025f, wr * 0.09f);
            const int segments = 10;

            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? sideZ + 0.006f : -sideZ - 0.006f;
                Vector3 normalHint = side == 0 ? Vector3.forward : Vector3.back;
                MeshData data = MeshData.Create();
                for (int wheel = 0; wheel < 2; wheel++)
                {
                    float cx = wheel == 0 ? frontX : rearX;
                    for (int i = 0; i < segments; i++)
                    {
                        float a0 = Mathf.Lerp(12f, 168f, i / (float)segments) * Mathf.Deg2Rad;
                        float a1 = Mathf.Lerp(12f, 168f, (i + 1f) / segments) * Mathf.Deg2Rad;
                        Vector3 inner0 = new Vector3(cx + Mathf.Cos(a0) * (radius - thickness), wr + Mathf.Sin(a0) * (radius - thickness), z);
                        Vector3 inner1 = new Vector3(cx + Mathf.Cos(a1) * (radius - thickness), wr + Mathf.Sin(a1) * (radius - thickness), z);
                        Vector3 outer1 = new Vector3(cx + Mathf.Cos(a1) * radius, wr + Mathf.Sin(a1) * radius, z);
                        Vector3 outer0 = new Vector3(cx + Mathf.Cos(a0) * radius, wr + Mathf.Sin(a0) * radius, z);
                        if (side == 0)
                            AddQuadN(data, inner0, inner1, outer1, outer0, normalHint, AtlasRect(0f, 0f, 0.1f, 0.1f));
                        else
                            AddQuadN(data, outer0, outer1, inner1, inner0, normalHint, AtlasRect(0f, 0f, 0.1f, 0.1f));
                    }
                }
                CreateMeshObject(side == 0 ? "ArchTrimL" : "ArchTrimR", ctx.Root, data, ctx.DarkMat);
            }
        }

        private static void BuildGrille(BuildCtx ctx, float baseY, float bodyH)
        {
            float x = ctx.L * 0.487f;
            float y = baseY + bodyH * 0.48f;
            CreateSimpleBox(ctx, "Grille", new Vector3(0.02f, bodyH * 0.34f, ctx.W * 0.52f),
                ctx.DarkMat, new Vector3(x, y, 0f), false);
            for (int i = 0; i < 3; i++)
            {
                float sy = y - bodyH * 0.09f + i * bodyH * 0.09f;
                CreateSimpleBox(ctx, "GrilleSlat" + i, new Vector3(0.024f, 0.012f, ctx.W * 0.48f),
                    ctx.ChromeMat, new Vector3(x + 0.008f, sy, 0f), false);
            }
        }

        private static void BuildDoorHandles(BuildCtx ctx, float baseY, float bodyH, float sideZ)
        {
            float y = baseY + bodyH * 0.70f;
            CreateSimpleBox(ctx, "DoorHandleL", new Vector3(0.14f, 0.025f, 0.035f), ctx.ChromeMat,
                new Vector3(ctx.L * 0.055f, y, sideZ + 0.02f), false);
            CreateSimpleBox(ctx, "DoorHandleR", new Vector3(0.14f, 0.025f, 0.035f), ctx.ChromeMat,
                new Vector3(ctx.L * 0.055f, y, -sideZ - 0.02f), false);
        }

        private static void BuildKindSpecific(BuildCtx ctx, float baseY, float bodyH, float cabinX, float cabinY, float cabinL, float cabinH, float cabinW)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H;
            VehicleKind kind = ctx.Def.kind;

            if (kind == VehicleKind.Pickup)
            {
                MeshData bed = MeshData.Create();
                AddBoxFrame(bed, new Vector3(l * 0.30f, bodyH * 0.55f, w * 0.90f), 0.04f, AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "CargoBed", bed, ctx.BodyMat, new Vector3(-l * 0.22f, baseY + bodyH + bodyH * 0.28f, 0f));
            }
            else if (kind == VehicleKind.Truck)
            {
                MeshData box = MeshData.Create();
                AddBeveledBox(box, Vector3.zero, new Vector3(l * 0.58f, h * 0.62f, w * 0.96f), 0.03f, AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "CargoBox", box, ctx.BodyMat, new Vector3(-l * 0.12f, baseY + h * 0.31f + bodyH * 0.5f, 0f));
            }
            else if (kind == VehicleKind.Bus)
            {
                MeshData side = MeshData.Create();
                AddBeveledBox(side, Vector3.zero, new Vector3(l * 0.88f, h * 0.18f, w * 0.98f), 0.02f, AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "BusBase", side, ctx.BodyMat, new Vector3(0f, baseY + h * 0.12f, 0f));
            }
            else if (kind == VehicleKind.DumpTruck)
            {
                MeshData hopper = MeshData.Create();
                AddDumpHopper(hopper, new Vector3(l * 0.45f, h * 0.42f, w * 0.9f), AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "DumpHopper", hopper, ctx.AccentMat, new Vector3(-l * 0.18f, baseY + bodyH + h * 0.22f, 0f));
            }
            else if (kind == VehicleKind.FireTruck)
            {
                MeshData tank = MeshData.Create();
                AddBeveledBox(tank, Vector3.zero, new Vector3(l * 0.45f, h * 0.45f, w * 0.9f), 0.03f, AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "Tank", tank, ctx.BodyMat, new Vector3(-l * 0.16f, baseY + bodyH + h * 0.22f, 0f));
                CreateLadder(ctx, new Vector3(-l * 0.16f, baseY + bodyH + h * 0.50f, 0f), l * 0.42f, w * 0.5f);
            }
            else if (kind == VehicleKind.Ambulance)
            {
                MeshData box = MeshData.Create();
                AddBeveledBox(box, Vector3.zero, new Vector3(l * 0.52f, h * 0.50f, w * 0.94f), 0.025f, AtlasRect(0.25f, 0.5f, 0.25f, 0.25f));
                CreatePaintMesh(ctx, "MedBox", box, ctx.BodyMat, new Vector3(-l * 0.14f, baseY + bodyH + h * 0.25f, 0f));
                CreateCrossDecal(ctx, new Vector3(-l * 0.14f, baseY + bodyH + h * 0.28f, w * 0.475f), 0.35f);
            }
            else if (kind == VehicleKind.Police)
            {
                CreateLightBar(ctx, new Vector3(0f, cabinY + cabinH * 0.55f, 0f), w * 0.42f);
            }
        }

        private static void BuildGlass(BuildCtx ctx, float baseY, float bodyH, float cabinX, float cabinY, float cabinL, float cabinH, float cabinW)
        {
            float t = 0.02f;
            float inset = 0.03f;
            // Windshield
            CreateSimpleBox(ctx, "Windshield", new Vector3(cabinL * 0.08f, cabinH * 0.55f, cabinW - inset * 2f),
                ctx.GlassMat, new Vector3(cabinX + cabinL * 0.45f, cabinY + cabinH * 0.05f, 0f), false);
            // Rear window
            CreateSimpleBox(ctx, "RearGlass", new Vector3(cabinL * 0.07f, cabinH * 0.45f, cabinW - inset * 2f),
                ctx.GlassMat, new Vector3(cabinX - cabinL * 0.45f, cabinY + cabinH * 0.05f, 0f), false);
            // Side windows L/R
            float sideW = cabinL * 0.65f;
            CreateSimpleBox(ctx, "SideGlassL", new Vector3(sideW, cabinH * 0.45f, t),
                ctx.GlassMat, new Vector3(cabinX, cabinY + cabinH * 0.05f, cabinW * 0.5f - t * 0.5f), false);
            CreateSimpleBox(ctx, "SideGlassR", new Vector3(sideW, cabinH * 0.45f, t),
                ctx.GlassMat, new Vector3(cabinX, cabinY + cabinH * 0.05f, -cabinW * 0.5f + t * 0.5f), false);
        }

        private static void BuildLights(BuildCtx ctx)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;
            float baseY = wr * 0.55f;
            float bodyH = h * (ctx.Def.kind == VehicleKind.Sports ? 0.28f : 0.34f);
            float y = baseY + bodyH * 0.6f;
            float zOff = w * 0.28f;
            float xFront = l * 0.48f;
            float xRear = -l * 0.48f;

            CreateLight(ctx, "HeadlightL", PartKind.HeadlightL, ctx.LightMat, new Vector3(xFront, y, zOff), new Vector3(0.08f, 0.08f, 0.12f));
            CreateLight(ctx, "HeadlightR", PartKind.HeadlightR, ctx.LightMat, new Vector3(xFront, y, -zOff), new Vector3(0.08f, 0.08f, 0.12f));
            CreateSimpleBox(ctx, "TailL", new Vector3(0.06f, 0.07f, 0.14f), ctx.TailMat, new Vector3(xRear, y, zOff), false);
            CreateSimpleBox(ctx, "TailR", new Vector3(0.06f, 0.07f, 0.14f), ctx.TailMat, new Vector3(xRear, y, -zOff), false);
        }

        private static void CreateLight(BuildCtx ctx, string name, PartKind kind, Material mat, Vector3 pos, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(ctx.Root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            RemoveCollider(go.GetComponent<BoxCollider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            VehiclePart part = go.AddComponent<VehiclePart>();
            part.Configure(kind, go, null, 2);
            ctx.Instance.RegisterPart(part);
        }

        private static void BuildBumpersMirrors(BuildCtx ctx)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;
            float baseY = wr * 0.45f;
            float by = baseY + 0.08f;

            // Front bumper - serviceable
            GameObject bumper = CreateSimpleBox(ctx, "Bumper", new Vector3(0.12f, 0.16f, w * 0.95f), ctx.DarkMat, new Vector3(l * 0.5f, by, 0f), true);
            VehiclePart bumperPart = bumper.GetComponent<VehiclePart>();
            if (bumperPart == null) bumperPart = bumper.AddComponent<VehiclePart>();
            GameObject bumperGood = bumper;
            GameObject bumperDam = CreateSimpleBox(ctx, "BumperDamaged", new Vector3(0.14f, 0.14f, w * 0.98f), MakeMat("BumperDam", new Color(0.25f, 0.25f, 0.27f, 1f), 0.1f, 0.2f), new Vector3(l * 0.5f, by, 0f), false, bumper.transform.parent);
            bumperDam.transform.SetParent(bumper.transform, false);
            bumperDam.transform.localPosition = Vector3.zero;
            bumperDam.transform.localRotation = Quaternion.identity;
            bumperDam.transform.localScale = Vector3.one;
            // restructure: make wrapper
            GameObject bumperRoot = new GameObject("BumperRoot");
            bumperRoot.transform.SetParent(ctx.Root, false);
            bumperRoot.transform.localPosition = new Vector3(l * 0.5f, by, 0f);
            bumper.transform.SetParent(bumperRoot.transform, false);
            bumper.transform.localPosition = Vector3.zero;
            bumperDam.transform.SetParent(bumperRoot.transform, false);
            bumperPart = bumperRoot.GetComponent<VehiclePart>();
            if (bumperPart == null) bumperPart = bumperRoot.AddComponent<VehiclePart>();
            bumperPart.Configure(PartKind.Bumper, bumper, bumperDam, 4);
            ctx.Instance.RegisterPart(bumperPart);

            // Rear bumper
            CreateSimpleBox(ctx, "RearBumper", new Vector3(0.1f, 0.14f, w * 0.92f), ctx.DarkMat, new Vector3(-l * 0.5f, by, 0f), false);

            // Mirrors
            float mirrorY = baseY + h * 0.55f;
            float mirrorX = l * 0.12f;
            float mirrorZ = w * 0.52f;
            CreateMirror(ctx, "MirrorL", PartKind.MirrorL, new Vector3(mirrorX, mirrorY, mirrorZ), new Vector3(0.12f, 0.08f, 0.16f));
            CreateMirror(ctx, "MirrorR", PartKind.MirrorR, new Vector3(mirrorX, mirrorY, -mirrorZ), new Vector3(0.12f, 0.08f, -0.16f));
        }

        private static void CreateMirror(BuildCtx ctx, string name, PartKind kind, Vector3 pos, Vector3 size)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(ctx.Root, false);
            root.transform.localPosition = pos;
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale = new Vector3(0.08f, 0.03f, Mathf.Abs(size.z) * 0.5f);
            arm.transform.localPosition = new Vector3(0f, 0f, size.z * 0.25f);
            RemoveCollider(arm.GetComponent<BoxCollider>());
            arm.GetComponent<Renderer>().sharedMaterial = ctx.DarkMat;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = new Vector3(size.x, size.y, Mathf.Abs(size.z) * 0.35f);
            head.transform.localPosition = new Vector3(0f, 0f, size.z * 0.55f);
            RemoveCollider(head.GetComponent<BoxCollider>());
            head.GetComponent<Renderer>().sharedMaterial = ctx.DarkMat;
            MeshCollider mc = head.AddComponent<MeshCollider>();
            mc.sharedMesh = head.GetComponent<MeshFilter>().sharedMesh;
            VehiclePart part = root.AddComponent<VehiclePart>();
            part.Configure(kind, root, null, 2);
            ctx.Instance.RegisterPart(part);
        }

        private static void BuildInterior(BuildCtx ctx)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;
            float baseY = wr * 0.55f;
            float bodyH = h * (ctx.Def.kind == VehicleKind.Sports ? 0.28f : 0.34f);
            float cabinH = h * 0.36f;
            float cabinY = baseY + bodyH + cabinH * 0.25f;
            float cabinX = -l * 0.04f;
            if (ctx.Def.kind == VehicleKind.Bus) { cabinX = 0f; cabinH = h * 0.52f; cabinY = baseY + bodyH + cabinH * 0.3f; }

            // Dashboard
            CreateSimpleBox(ctx, "Dashboard", new Vector3(l * 0.08f, cabinH * 0.18f, w * 0.7f), ctx.InteriorMat,
                new Vector3(cabinX + l * 0.12f, cabinY, 0f), false);
            // Seats
            CreateSeat(ctx, new Vector3(cabinX + 0.05f, cabinY - cabinH * 0.1f, w * 0.18f));
            CreateSeat(ctx, new Vector3(cabinX + 0.05f, cabinY - cabinH * 0.1f, -w * 0.18f));
            CreateSeat(ctx, new Vector3(cabinX - l * 0.18f, cabinY - cabinH * 0.1f, w * 0.18f));
            CreateSeat(ctx, new Vector3(cabinX - l * 0.18f, cabinY - cabinH * 0.1f, -w * 0.18f));
            // Steering wheel
            GameObject steer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            steer.name = "Steering";
            steer.transform.SetParent(ctx.Root, false);
            steer.transform.localPosition = new Vector3(cabinX + l * 0.1f, cabinY + 0.05f, w * 0.18f);
            steer.transform.localScale = new Vector3(0.18f, 0.01f, 0.18f);
            steer.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            RemoveCollider(steer.GetComponent<CapsuleCollider>());
            steer.GetComponent<Renderer>().sharedMaterial = ctx.DarkMat;
        }

        private static void CreateSeat(BuildCtx ctx, Vector3 pos)
        {
            CreateSimpleBox(ctx, "SeatBase", new Vector3(0.28f, 0.08f, 0.22f), ctx.InteriorMat, pos, false);
            CreateSimpleBox(ctx, "SeatBack", new Vector3(0.08f, 0.28f, 0.22f), ctx.InteriorMat, pos + new Vector3(-0.1f, 0.16f, 0f), false);
        }

        private static void BuildWheels(BuildCtx ctx)
        {
            float l = ctx.L, w = ctx.W, wr = ctx.WheelR, ww = ctx.WheelW;
            float frontX = l * 0.32f;
            float rearX = -l * 0.32f;
            if (ctx.Def.kind == VehicleKind.Bus || ctx.Def.kind == VehicleKind.Truck || ctx.Def.kind == VehicleKind.FireTruck || ctx.Def.kind == VehicleKind.DumpTruck)
            { frontX = l * 0.34f; rearX = -l * 0.30f; }
            float z = w * 0.5f - ww * 0.2f;

            CreateWheel(ctx, PartKind.WheelFL, new Vector3(frontX, wr, z), false);
            CreateWheel(ctx, PartKind.WheelFR, new Vector3(frontX, wr, -z), true);
            CreateWheel(ctx, PartKind.WheelRL, new Vector3(rearX, wr, z), false);
            CreateWheel(ctx, PartKind.WheelRR, new Vector3(rearX, wr, -z), true);
        }

        private static void CreateWheel(BuildCtx ctx, PartKind kind, Vector3 pos, bool mirror)
        {
            GameObject anchor = new GameObject(kind.ToString());
            anchor.transform.SetParent(ctx.Root, false);
            anchor.transform.localPosition = pos;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(anchor.transform, false);

            float r = ctx.WheelR;
            float w = ctx.WheelW;
            int segments = 20;

            // Tire
            MeshData tire = MeshData.Create();
            AddCylinder(tire, Vector3.zero, r, r * 0.78f, w, segments, AtlasRect(0f, 0f, 0.1f, 0.1f));
            GameObject tireGo = CreateMeshObject("Tire", visual.transform, tire, ctx.TireMat);
            // Rim
            MeshData rim = MeshData.Create();
            AddCylinder(rim, Vector3.zero, r * 0.76f, r * 0.22f, w * 0.85f, segments, AtlasRect(0f, 0f, 0.1f, 0.1f));
            GameObject rimGo = CreateMeshObject("Rim", visual.transform, rim, ctx.ChromeMat);
            ctx.Instance.RegisterRimRenderer(rimGo.GetComponent<Renderer>());
            // Hub cap
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(visual.transform, false);
            hub.transform.localScale = new Vector3(r * 0.35f, w * 0.45f, r * 0.35f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            RemoveCollider(hub.GetComponent<CapsuleCollider>());
            Renderer hubR = hub.GetComponent<Renderer>();
            hubR.sharedMaterial = ctx.ChromeMat;
            ctx.Instance.RegisterRimRenderer(hubR);

            // Five simple spokes read as a rim at gameplay camera distance.
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spoke.name = "Spoke" + i;
                spoke.transform.SetParent(visual.transform, false);
                spoke.transform.localPosition = new Vector3(Mathf.Cos(a) * r * 0.40f, Mathf.Sin(a) * r * 0.40f, 0f);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                spoke.transform.localScale = new Vector3(r * 0.10f, r * 0.52f, w * 0.62f);
                RemoveCollider(spoke.GetComponent<BoxCollider>());
                Renderer spokeR = spoke.GetComponent<Renderer>();
                spokeR.sharedMaterial = ctx.ChromeMat;
                ctx.Instance.RegisterRimRenderer(spokeR);
            }

            // Collider on anchor for raycast/selection
            BoxCollider bc = anchor.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = new Vector3(r * 2f, r * 2f, w * 1.2f);

            VehiclePart part = anchor.AddComponent<VehiclePart>();
            GameObject damaged = CreateDamagedWheel(ctx, anchor.transform, r, w);
            damaged.SetActive(false);
            part.Configure(kind, visual, damaged, 4);
            ctx.Instance.RegisterPart(part);
            ctx.Instance.RegisterWheel(kind, anchor.transform, visual.transform);
        }

        private static GameObject CreateDamagedWheel(BuildCtx ctx, Transform parent, float r, float w)
        {
            GameObject damaged = new GameObject("Damaged");
            damaged.transform.SetParent(parent, false);
            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.transform.SetParent(damaged.transform, false);
            tire.transform.localScale = new Vector3(r * 1.7f, w * 0.4f, r * 1.4f);
            tire.transform.localRotation = Quaternion.Euler(90f, 0f, 15f);
            RemoveCollider(tire.GetComponent<CapsuleCollider>());
            Material dm = MakeMat("DamagedTire", new Color(0.18f, 0.14f, 0.12f, 1f), 0f, 0.15f);
            tire.GetComponent<Renderer>().sharedMaterial = dm;
            return damaged;
        }

        private static void BuildServiceParts(BuildCtx ctx, Transform hoodPivot)
        {
            float l = ctx.L, w = ctx.W, h = ctx.H, wr = ctx.WheelR;
            float baseY = wr * 0.55f;
            float bodyH = h * (ctx.Def.kind == VehicleKind.Sports ? 0.28f : 0.34f);

            // Engine under hood
            GameObject engine = CreateSimpleBox(ctx, "Engine", new Vector3(l * 0.22f, bodyH * 0.45f, w * 0.45f),
                MakeMat("EngineMat", new Color(0.35f, 0.36f, 0.38f, 1f), 0.6f, 0.4f),
                new Vector3(l * 0.28f, baseY + bodyH * 0.55f, 0f), true);
            VehiclePart enginePart = engine.GetComponent<VehiclePart>();
            if (enginePart == null) enginePart = engine.AddComponent<VehiclePart>();
            GameObject engineDam = CreateSimpleBox(ctx, "EngineDamaged", new Vector3(l * 0.22f, bodyH * 0.45f, w * 0.45f),
                MakeMat("EngineDam", new Color(0.25f, 0.18f, 0.15f, 1f), 0.2f, 0.2f),
                Vector3.zero, false, engine.transform.parent);
            engineDam.transform.SetParent(engine.transform, false);
            engineDam.transform.localPosition = Vector3.zero;
            enginePart.Configure(PartKind.Engine, engine, engineDam, 3);
            ctx.Instance.RegisterPart(enginePart);

            // Battery
            GameObject battery = CreateSimpleBox(ctx, "Battery", new Vector3(0.18f, 0.14f, 0.12f),
                MakeMat("BatteryMat", new Color(0.15f, 0.15f, 0.18f, 1f), 0.2f, 0.3f),
                new Vector3(l * 0.18f, baseY + bodyH * 0.45f, w * 0.18f), true);
            VehiclePart batPart = battery.GetComponent<VehiclePart>();
            if (batPart == null) batPart = battery.AddComponent<VehiclePart>();
            GameObject batDam = CreateSimpleBox(ctx, "BatteryDamaged", new Vector3(0.18f, 0.14f, 0.12f),
                MakeMat("BatDam", new Color(0.3f, 0.2f, 0.1f, 1f), 0.1f, 0.15f), Vector3.zero, false, battery.transform.parent);
            batDam.transform.SetParent(battery.transform, false);
            batDam.transform.localPosition = Vector3.zero;
            batPart.Configure(PartKind.Battery, battery, batDam, 2);
            ctx.Instance.RegisterPart(batPart);

            // Windshield as serviceable part
            float cabinH = h * 0.36f;
            float cabinY = baseY + bodyH + cabinH * 0.5f;
            float cabinX = -l * 0.04f;
            float cabinL = l * 0.44f;
            float cabinW = w * 0.86f;
            if (ctx.Def.kind == VehicleKind.Sports) { cabinL = l * 0.36f; cabinX = -l * 0.08f; cabinH = h * 0.24f; }
            GameObject wsRoot = new GameObject("WindshieldPart");
            wsRoot.transform.SetParent(ctx.Root, false);
            wsRoot.transform.localPosition = new Vector3(cabinX + cabinL * 0.45f, cabinY + cabinH * 0.05f, 0f);
            GameObject wsGood = CreateSimpleBox(ctx, "WindshieldGood", new Vector3(cabinL * 0.08f, cabinH * 0.55f, cabinW - 0.06f), ctx.GlassMat, Vector3.zero, false, wsRoot.transform);
            GameObject wsDam = CreateSimpleBox(ctx, "WindshieldDamaged", new Vector3(cabinL * 0.08f, cabinH * 0.55f, cabinW - 0.06f),
                MakeMat("CrackedGlass", new Color(0.7f, 0.85f, 0.92f, 0.45f), 0f, 0.5f, true), Vector3.zero, false, wsRoot.transform);
            wsDam.SetActive(false);
            VehiclePart wsPart = wsRoot.AddComponent<VehiclePart>();
            wsPart.Configure(PartKind.Windshield, wsGood, wsDam, 2);
            ctx.Instance.RegisterPart(wsPart);
        }

        // ---- Mesh helpers ----

        private static Rect AtlasRect(float x, float y, float w, float h)
        {
            return new Rect(x, y, w, h);
        }

        private static void AddVert(MeshData m, Vector3 v, Vector3 n, Vector2 uv)
        {
            m.V.Add(v); m.N.Add(n.normalized); m.Uv.Add(uv);
        }

        private static void AddQuad(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uvRect)
        {
            // The emitted triangles are (a,c,b) and (a,d,c). Derive the stored normal
            // from that winding so lighting agrees with the visible front face.
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            int i = m.V.Count;
            AddVert(m, a, n, new Vector2(uvRect.xMin, uvRect.yMin));
            AddVert(m, b, n, new Vector2(uvRect.xMax, uvRect.yMin));
            AddVert(m, c, n, new Vector2(uvRect.xMax, uvRect.yMax));
            AddVert(m, d, n, new Vector2(uvRect.xMin, uvRect.yMax));
            m.T.Add(i); m.T.Add(i + 2); m.T.Add(i + 1);
            m.T.Add(i); m.T.Add(i + 3); m.T.Add(i + 2);
        }

        private static void AddQuadN(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 ignoredNormalHint, Rect uvRect)
        {
            // Kept for call sites that previously supplied a normal hint. The hint was
            // wrong on one side of the car, so derive the normal from winding instead.
            AddQuad(m, a, b, c, d, uvRect);
        }

        private static void AddBeveledBox(MeshData m, Vector3 center, Vector3 size, float bevel, Rect uv)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            bevel = Mathf.Clamp(bevel, 0f, Mathf.Min(hx, hy, hz) * 0.45f);
            float ix = hx - bevel, iy = hy - bevel, iz = hz - bevel;
            Vector3 c = center;

            // Faces with inset
            // Front (+X)
            AddQuad(m, c + new Vector3(hx, -iy, -iz), c + new Vector3(hx, -iy, iz), c + new Vector3(hx, iy, iz), c + new Vector3(hx, iy, -iz), uv);
            // Back (-X)
            AddQuad(m, c + new Vector3(-hx, -iy, iz), c + new Vector3(-hx, -iy, -iz), c + new Vector3(-hx, iy, -iz), c + new Vector3(-hx, iy, iz), uv);
            // Top (+Y)
            AddQuad(m, c + new Vector3(-ix, hy, -iz), c + new Vector3(ix, hy, -iz), c + new Vector3(ix, hy, iz), c + new Vector3(-ix, hy, iz), uv);
            // Bottom (-Y)
            AddQuad(m, c + new Vector3(-ix, -hy, iz), c + new Vector3(ix, -hy, iz), c + new Vector3(ix, -hy, -iz), c + new Vector3(-ix, -hy, -iz), uv);
            // Right (+Z)
            AddQuad(m, c + new Vector3(-ix, -iy, hz), c + new Vector3(-ix, iy, hz), c + new Vector3(ix, iy, hz), c + new Vector3(ix, -iy, hz), uv);
            // Left (-Z)
            AddQuad(m, c + new Vector3(ix, -iy, -hz), c + new Vector3(ix, iy, -hz), c + new Vector3(-ix, iy, -hz), c + new Vector3(-ix, -iy, -hz), uv);

            if (bevel <= 0.0001f) return;

            // Bevel strips (12 edges) - simplified as quads
            // Top-front edge
            AddQuad(m, c + new Vector3(hx, iy, -iz), c + new Vector3(hx, iy, iz), c + new Vector3(ix, hy, iz), c + new Vector3(ix, hy, -iz), uv);
            // Top-back
            AddQuad(m, c + new Vector3(-ix, hy, -iz), c + new Vector3(-ix, hy, iz), c + new Vector3(-hx, iy, iz), c + new Vector3(-hx, iy, -iz), uv);
            // Bottom-front
            AddQuad(m, c + new Vector3(ix, -hy, -iz), c + new Vector3(ix, -hy, iz), c + new Vector3(hx, -iy, iz), c + new Vector3(hx, -iy, -iz), uv);
            // Bottom-back
            AddQuad(m, c + new Vector3(-hx, -iy, -iz), c + new Vector3(-hx, -iy, iz), c + new Vector3(-ix, -hy, iz), c + new Vector3(-ix, -hy, -iz), uv);
            // Top-right
            AddQuad(m, c + new Vector3(-ix, iy, hz), c + new Vector3(-ix, hy, iz), c + new Vector3(ix, hy, iz), c + new Vector3(ix, iy, hz), uv);
            // Top-left
            AddQuad(m, c + new Vector3(ix, iy, -hz), c + new Vector3(ix, hy, -iz), c + new Vector3(-ix, hy, -iz), c + new Vector3(-ix, iy, -hz), uv);
            // Bottom-right
            AddQuad(m, c + new Vector3(-ix, -hy, iz), c + new Vector3(-ix, -iy, hz), c + new Vector3(ix, -iy, hz), c + new Vector3(ix, -hy, iz), uv);
            // Bottom-left
            AddQuad(m, c + new Vector3(ix, -hy, -iz), c + new Vector3(ix, -iy, -hz), c + new Vector3(-ix, -iy, -hz), c + new Vector3(-ix, -hy, -iz), uv);
            // Front-right
            AddQuad(m, c + new Vector3(hx, -iy, iz), c + new Vector3(ix, -iy, hz), c + new Vector3(ix, iy, hz), c + new Vector3(hx, iy, iz), uv);
            // Front-left
            AddQuad(m, c + new Vector3(hx, iy, -iz), c + new Vector3(ix, iy, -hz), c + new Vector3(ix, -iy, -hz), c + new Vector3(hx, -iy, -iz), uv);
            // Back-right
            AddQuad(m, c + new Vector3(-hx, iy, iz), c + new Vector3(-ix, iy, hz), c + new Vector3(-ix, -iy, hz), c + new Vector3(-hx, -iy, iz), uv);
            // Back-left
            AddQuad(m, c + new Vector3(-hx, -iy, -iz), c + new Vector3(-ix, -iy, -hz), c + new Vector3(-ix, iy, -hz), c + new Vector3(-hx, iy, -iz), uv);

            // 8 corner triangles. Each one is oriented toward its outward octant so
            // no bevel is back-face-culled when the camera orbits the car.
            AddTri(m, c + new Vector3(ix, hy, iz), c + new Vector3(hx, iy, iz), c + new Vector3(ix, iy, hz), new Vector3(1f, 1f, 1f), uv);
            AddTri(m, c + new Vector3(ix, hy, -iz), c + new Vector3(ix, iy, -hz), c + new Vector3(hx, iy, -iz), new Vector3(1f, 1f, -1f), uv);
            AddTri(m, c + new Vector3(-ix, hy, iz), c + new Vector3(-ix, iy, hz), c + new Vector3(-hx, iy, iz), new Vector3(-1f, 1f, 1f), uv);
            AddTri(m, c + new Vector3(-ix, hy, -iz), c + new Vector3(-hx, iy, -iz), c + new Vector3(-ix, iy, -hz), new Vector3(-1f, 1f, -1f), uv);
            AddTri(m, c + new Vector3(ix, -hy, iz), c + new Vector3(ix, -iy, hz), c + new Vector3(hx, -iy, iz), new Vector3(1f, -1f, 1f), uv);
            AddTri(m, c + new Vector3(ix, -hy, -iz), c + new Vector3(hx, -iy, -iz), c + new Vector3(ix, -iy, -hz), new Vector3(1f, -1f, -1f), uv);
            AddTri(m, c + new Vector3(-ix, -hy, iz), c + new Vector3(-hx, -iy, iz), c + new Vector3(-ix, -iy, hz), new Vector3(-1f, -1f, 1f), uv);
            AddTri(m, c + new Vector3(-ix, -hy, -iz), c + new Vector3(-ix, -iy, -hz), c + new Vector3(-hx, -iy, -iz), new Vector3(-1f, -1f, -1f), uv);
        }

        private static void AddTri(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Rect uv)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
                n = -n;
            }
            n = n.normalized;
            int i = m.V.Count;
            AddVert(m, a, n, new Vector2(uv.xMin, uv.yMin));
            AddVert(m, b, n, new Vector2(uv.xMax, uv.yMin));
            AddVert(m, c, n, new Vector2(uv.xMin, uv.yMax));
            m.T.Add(i); m.T.Add(i + 1); m.T.Add(i + 2);
        }

        private static void AddBoxFrame(MeshData m, Vector3 size, float wall, Rect uv)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            // floor
            AddQuad(m, new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz), uv);
            // walls
            AddQuad(m, new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), uv);
            AddQuad(m, new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), uv);
            AddQuad(m, new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), uv);
            AddQuad(m, new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), uv);
            // inner faces
            float ihx = hx - wall, ihy = hy, ihz = hz - wall;
            AddQuad(m, new Vector3(-ihx, -hy + wall, ihz), new Vector3(-ihx, hy, ihz), new Vector3(ihx, hy, ihz), new Vector3(ihx, -hy + wall, ihz), uv);
            AddQuad(m, new Vector3(ihx, -hy + wall, -ihz), new Vector3(ihx, hy, -ihz), new Vector3(-ihx, hy, -ihz), new Vector3(-ihx, -hy + wall, -ihz), uv);
            AddQuad(m, new Vector3(ihx, -hy + wall, ihz), new Vector3(ihx, hy, ihz), new Vector3(ihx, hy, -ihz), new Vector3(ihx, -hy + wall, -ihz), uv);
            AddQuad(m, new Vector3(-ihx, -hy + wall, -ihz), new Vector3(-ihx, hy, -ihz), new Vector3(-ihx, hy, ihz), new Vector3(-ihx, -hy + wall, ihz), uv);
            AddQuad(m, new Vector3(-ihx, -hy + wall, -ihz), new Vector3(-ihx, -hy + wall, ihz), new Vector3(ihx, -hy + wall, ihz), new Vector3(ihx, -hy + wall, -ihz), uv);
        }

        private static void AddDumpHopper(MeshData m, Vector3 size, Rect uv)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float taper = 0.85f;
            // bottom
            AddQuad(m, new Vector3(-hx * taper, -hy, -hz * taper), new Vector3(hx * taper, -hy, -hz * taper), new Vector3(hx * taper, -hy, hz * taper), new Vector3(-hx * taper, -hy, hz * taper), uv);
            // sides slanted out
            AddQuad(m, new Vector3(-hx * taper, -hy, hz * taper), new Vector3(hx * taper, -hy, hz * taper), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), uv);
            AddQuad(m, new Vector3(hx * taper, -hy, -hz * taper), new Vector3(-hx * taper, -hy, -hz * taper), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), uv);
            AddQuad(m, new Vector3(-hx * taper, -hy, -hz * taper), new Vector3(-hx * taper, -hy, hz * taper), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), uv);
            AddQuad(m, new Vector3(hx * taper, -hy, hz * taper), new Vector3(hx * taper, -hy, -hz * taper), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), uv);
            // back
            AddQuad(m, new Vector3(-hx, -hy * 0.2f, -hz), new Vector3(-hx, -hy * 0.2f, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), uv);
        }

        private static void AddWheelArches(MeshData m, BuildCtx ctx, float baseY, float bodyH)
        {
            // Simple wheel well cutout approximation by adding dark arch strips as part of body mesh is not necessary;
            // arches are suggested by wheel placement and bumpers; keep body simple to avoid invalid geometry.
        }

        private static void AddCylinder(MeshData m, Vector3 center, float radiusOuter, float radiusInner, float width, int segments, Rect uvRect)
        {
            float hw = width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 p0o = center + new Vector3(Mathf.Cos(a0) * radiusOuter, Mathf.Sin(a0) * radiusOuter, -hw);
                Vector3 p1o = center + new Vector3(Mathf.Cos(a1) * radiusOuter, Mathf.Sin(a1) * radiusOuter, -hw);
                Vector3 p0o2 = center + new Vector3(Mathf.Cos(a0) * radiusOuter, Mathf.Sin(a0) * radiusOuter, hw);
                Vector3 p1o2 = center + new Vector3(Mathf.Cos(a1) * radiusOuter, Mathf.Sin(a1) * radiusOuter, hw);
                // Outer surface: reversed from the old order so the visible front
                // face points away from the axle instead of being culled.
                AddQuad(m, p0o2, p1o2, p1o, p0o, uvRect);

                Vector3 p0i = center + new Vector3(Mathf.Cos(a0) * radiusInner, Mathf.Sin(a0) * radiusInner, -hw);
                Vector3 p1i = center + new Vector3(Mathf.Cos(a1) * radiusInner, Mathf.Sin(a1) * radiusInner, -hw);
                Vector3 p0i2 = center + new Vector3(Mathf.Cos(a0) * radiusInner, Mathf.Sin(a0) * radiusInner, hw);
                Vector3 p1i2 = center + new Vector3(Mathf.Cos(a1) * radiusInner, Mathf.Sin(a1) * radiusInner, hw);
                // Inner surface faces the axle.
                AddQuad(m, p0i, p1i, p1i2, p0i2, uvRect);
                // Cap rings face +Z / -Z respectively.
                AddQuad(m, p0i2, p1i2, p1o2, p0o2, uvRect);
                AddQuad(m, p0o, p1o, p1i, p0i, uvRect);
            }
        }

        private static void CreatePaintMesh(BuildCtx ctx, string name, MeshData data, Material mat, Vector3 localPos, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : ctx.Root, false);
            go.transform.localPosition = localPos;

            Mesh mesh = data.ToMesh(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            ctx.Instance.RegisterPaintTarget(mc, mr);
        }

        private static GameObject CreateMeshObject(string name, Transform parent, MeshData data, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Mesh mesh = data.ToMesh(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateSimpleBox(BuildCtx ctx, string name, Vector3 size, Material mat, Vector3 localPos, bool addCollider, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : ctx.Root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            RemoveCollider(go.GetComponent<BoxCollider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (addCollider)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one;
            }
            return go;
        }

        private static void CreateLightBar(BuildCtx ctx, Vector3 pos, float width)
        {
            GameObject root = new GameObject("LightBar");
            root.transform.SetParent(ctx.Root, false);
            root.transform.localPosition = pos;
            GameObject baseBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBar.name = "Base";
            baseBar.transform.SetParent(root.transform, false);
            baseBar.transform.localScale = new Vector3(0.08f, 0.04f, width);
            RemoveCollider(baseBar.GetComponent<BoxCollider>());
            baseBar.GetComponent<Renderer>().sharedMaterial = ctx.DarkMat;
            GameObject red = GameObject.CreatePrimitive(PrimitiveType.Cube);
            red.name = "Red";
            red.transform.SetParent(root.transform, false);
            red.transform.localPosition = new Vector3(0f, 0.03f, width * 0.2f);
            red.transform.localScale = new Vector3(0.09f, 0.05f, width * 0.35f);
            RemoveCollider(red.GetComponent<BoxCollider>());
            red.GetComponent<Renderer>().sharedMaterial = MakeMat("LightRed", new Color(1f, 0.15f, 0.1f, 1f), 0.2f, 0.8f);
            GameObject blue = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blue.name = "Blue";
            blue.transform.SetParent(root.transform, false);
            blue.transform.localPosition = new Vector3(0f, 0.03f, -width * 0.2f);
            blue.transform.localScale = new Vector3(0.09f, 0.05f, width * 0.35f);
            RemoveCollider(blue.GetComponent<BoxCollider>());
            blue.GetComponent<Renderer>().sharedMaterial = MakeMat("LightBlue", new Color(0.1f, 0.3f, 1f, 1f), 0.2f, 0.8f);
        }

        private static void CreateLadder(BuildCtx ctx, Vector3 pos, float length, float width)
        {
            GameObject root = new GameObject("Ladder");
            root.transform.SetParent(ctx.Root, false);
            root.transform.localPosition = pos;
            for (int i = 0; i < 2; i++)
            {
                float z = (i == 0 ? 1f : -1f) * width * 0.5f;
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Rail";
                rail.transform.SetParent(root.transform, false);
                rail.transform.localPosition = new Vector3(0f, 0f, z);
                rail.transform.localScale = new Vector3(length, 0.03f, 0.03f);
                RemoveCollider(rail.GetComponent<BoxCollider>());
                rail.GetComponent<Renderer>().sharedMaterial = ctx.ChromeMat;
            }
            int rungs = 6;
            for (int i = 0; i < rungs; i++)
            {
                float x = Mathf.Lerp(-length * 0.45f, length * 0.45f, i / (float)(rungs - 1));
                GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rung.name = "Rung";
                rung.transform.SetParent(root.transform, false);
                rung.transform.localPosition = new Vector3(x, 0f, 0f);
                rung.transform.localScale = new Vector3(0.03f, 0.03f, width);
                RemoveCollider(rung.GetComponent<BoxCollider>());
                rung.GetComponent<Renderer>().sharedMaterial = ctx.ChromeMat;
            }
        }

        private static void CreateCrossDecal(BuildCtx ctx, Vector3 pos, float size)
        {
            GameObject root = new GameObject("CrossDecal");
            root.transform.SetParent(ctx.Root, false);
            root.transform.localPosition = pos;
            Material red = MakeMat("CrossRed", new Color(0.9f, 0.12f, 0.12f, 1f), 0f, 0.4f);
            GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.transform.SetParent(root.transform, false);
            v.transform.localScale = new Vector3(size * 0.25f, size, 0.02f);
            RemoveCollider(v.GetComponent<BoxCollider>());
            v.GetComponent<Renderer>().sharedMaterial = red;
            GameObject h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.transform.SetParent(root.transform, false);
            h.transform.localScale = new Vector3(size, size * 0.25f, 0.02f);
            RemoveCollider(h.GetComponent<BoxCollider>());
            h.GetComponent<Renderer>().sharedMaterial = red;
        }

        /// <summary>
        /// Drops the collider Unity adds to every primitive. Destroy() is illegal in edit
        /// mode (it logs an error that fails EditMode tests), so use DestroyImmediate
        /// whenever the factory runs outside play mode.
        /// </summary>
        private static void RemoveCollider(Component collider)
        {
            if (collider == null) return;
            if (Application.isPlaying)
                Object.Destroy(collider);
            else
                Object.DestroyImmediate(collider);
        }
    }
}
