using UnityEngine;

namespace LittleGarage
{
    public sealed class GarageEnvironment : MonoBehaviour
    {
        private Transform _washAnchor;
        private Transform _repairAnchor;
        private Transform _paintAnchor;
        private WorkZone _currentZone = WorkZone.WashBay;
        private Light _washLight;
        private Light _repairLight;
        private Light _paintLight;
        private GameObject _washProps;
        private GameObject _repairProps;
        private GameObject _paintProps;

        public void Build()
        {
            ClearChildren();
            BuildShell();
            BuildLights();
            _washProps = BuildWashBay();
            _repairProps = BuildRepairBay();
            _paintProps = BuildPaintBay();
            BuildAnchors();
            SetZone(WorkZone.WashBay);
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void BuildShell()
        {
            Material floorMat = MakeMat("FloorMat", new Color(0.62f, 0.63f, 0.66f, 1f), 0.05f, 0.35f);
            Material wallMat = MakeMat("WallMat", new Color(0.86f, 0.87f, 0.88f, 1f), 0.0f, 0.2f);
            Material ceilingMat = MakeMat("CeilingMat", new Color(0.78f, 0.79f, 0.82f, 1f), 0.0f, 0.15f);
            Material stripeMat = MakeMat("StripeMat", new Color(0.95f, 0.78f, 0.18f, 1f), 0.0f, 0.25f);
            Material drainMat = MakeMat("DrainMat", new Color(0.25f, 0.26f, 0.28f, 1f), 0.2f, 0.3f);

            // Floor
            GameObject floor = CreateBox("Floor", new Vector3(18f, 0.2f, 12f), floorMat, new Vector3(0f, -0.1f, 0f));
            floor.isStatic = true;

            // Drain channels
            for (int i = -1; i <= 1; i++)
            {
                CreateBox("Drain_" + i, new Vector3(8f, 0.03f, 0.2f), drainMat, new Vector3(0f, 0.015f, i * 1.6f));
            }

            // Wet patches
            Material wetMat = MakeMat("WetMat", new Color(0.55f, 0.60f, 0.66f, 0.65f), 0.1f, 0.85f, true);
            CreateBox("WetPatch1", new Vector3(2.6f, 0.01f, 1.4f), wetMat, new Vector3(-4.2f, 0.01f, 0.2f));
            CreateBox("WetPatch2", new Vector3(1.8f, 0.01f, 1.0f), wetMat, new Vector3(-3.2f, 0.01f, -1.2f));

            // Safety stripes
            for (int i = 0; i < 8; i++)
            {
                float x = -6f + i * 1.6f;
                CreateBox("Stripe_" + i, new Vector3(0.6f, 0.02f, 0.12f), stripeMat, new Vector3(x, 0.012f, 5.4f));
                CreateBox("StripeB_" + i, new Vector3(0.6f, 0.02f, 0.12f), stripeMat, new Vector3(x, 0.012f, -5.4f));
            }

            // Walls
            CreateBox("WallBack", new Vector3(18f, 5f, 0.3f), wallMat, new Vector3(0f, 2.5f, -5.8f));
            CreateBox("WallFront", new Vector3(18f, 5f, 0.3f), wallMat, new Vector3(0f, 2.5f, 5.8f));
            CreateBox("WallLeft", new Vector3(0.3f, 5f, 12f), wallMat, new Vector3(-8.8f, 2.5f, 0f));
            CreateBox("WallRight", new Vector3(0.3f, 5f, 12f), wallMat, new Vector3(8.8f, 2.5f, 0f));
            // Ceiling
            CreateBox("Ceiling", new Vector3(18f, 0.2f, 12f), ceilingMat, new Vector3(0f, 5.1f, 0f));

            // Trusses
            Material trussMat = MakeMat("TrussMat", new Color(0.35f, 0.36f, 0.38f, 1f), 0.5f, 0.4f);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 3.2f;
                CreateBox("Truss_" + i, new Vector3(0.12f, 0.12f, 11f), trussMat, new Vector3(x, 4.8f, 0f));
                CreateBox("TrussV_" + i, new Vector3(0.12f, 0.8f, 0.12f), trussMat, new Vector3(x, 4.45f, -2.5f));
                CreateBox("TrussV2_" + i, new Vector3(0.12f, 0.8f, 0.12f), trussMat, new Vector3(x, 4.45f, 2.5f));
            }
        }

        private void BuildLights()
        {
            // Ambient-ish fill via light settings is scene-level; use real lights sparingly
            GameObject mainLightGo = new GameObject("MainLight");
            mainLightGo.transform.SetParent(transform, false);
            mainLightGo.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            mainLightGo.transform.localRotation = Quaternion.Euler(75f, -20f, 0f);
            Light main = mainLightGo.AddComponent<Light>();
            main.type = LightType.Directional;
            main.intensity = 0.95f;
            main.color = new Color(1f, 0.98f, 0.94f);
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.45f;

            _washLight = CreateSpot("WashLight", new Vector3(-4.5f, 4.2f, 0f), new Color(0.9f, 0.95f, 1f), 1.1f);
            _repairLight = CreateSpot("RepairLight", new Vector3(0f, 4.2f, 0f), new Color(1f, 0.97f, 0.9f), 1.0f);
            _paintLight = CreateSpot("PaintLight", new Vector3(4.5f, 4.2f, 0f), new Color(1f, 0.96f, 0.92f), 1.0f);
        }

        private Light CreateSpot(string name, Vector3 pos, Color color, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = 12f;
            l.spotAngle = 85f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.35f;
            return l;
        }

        private GameObject BuildWashBay()
        {
            GameObject root = new GameObject("WashBay");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-4.5f, 0f, 0f);

            Material metal = MakeMat("WashMetal", new Color(0.55f, 0.58f, 0.62f, 1f), 0.7f, 0.55f);
            Material blue = MakeMat("WashBlue", new Color(0.2f, 0.55f, 0.9f, 1f), 0.1f, 0.5f);
            Material hoseMat = MakeMat("HoseMat", new Color(0.15f, 0.45f, 0.2f, 1f), 0.0f, 0.35f);

            // Arches / rings
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 1.5f;
                CreateBox("ArchL_" + i, new Vector3(0.12f, 2.4f, 0.12f), metal, new Vector3(x, 1.2f, 1.6f), root.transform);
                CreateBox("ArchR_" + i, new Vector3(0.12f, 2.4f, 0.12f), metal, new Vector3(x, 1.2f, -1.6f), root.transform);
                CreateBox("ArchTop_" + i, new Vector3(0.12f, 0.12f, 3.32f), metal, new Vector3(x, 2.4f, 0f), root.transform);
            }
            // Hose reel
            CreateBox("HoseBox", new Vector3(0.5f, 0.8f, 0.4f), blue, new Vector3(-2.2f, 0.4f, 2.2f), root.transform);
            CreateCylinder("Hose", 0.18f, 0.12f, hoseMat, new Vector3(-2.2f, 0.85f, 2.2f), root.transform);
            // Drain grate
            CreateBox("Grate", new Vector3(2.8f, 0.03f, 1.0f), MakeMat("Grate", new Color(0.22f, 0.23f, 0.25f, 1f), 0.4f, 0.3f), new Vector3(0f, 0.02f, 0f), root.transform);

            return root;
        }

        private GameObject BuildRepairBay()
        {
            GameObject root = new GameObject("RepairBay");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            Material metal = MakeMat("RepairMetal", new Color(0.45f, 0.47f, 0.5f, 1f), 0.6f, 0.45f);
            Material dark = MakeMat("RepairDark", new Color(0.2f, 0.21f, 0.23f, 1f), 0.2f, 0.3f);
            Material red = MakeMat("RepairRed", new Color(0.8f, 0.18f, 0.14f, 1f), 0.1f, 0.5f);

            // Two-post lift
            CreateBox("LiftPostL", new Vector3(0.25f, 2.6f, 0.25f), red, new Vector3(-1.2f, 1.3f, 1.0f), root.transform);
            CreateBox("LiftPostR", new Vector3(0.25f, 2.6f, 0.25f), red, new Vector3(-1.2f, 1.3f, -1.0f), root.transform);
            CreateBox("LiftPostL2", new Vector3(0.25f, 2.6f, 0.25f), red, new Vector3(1.2f, 1.3f, 1.0f), root.transform);
            CreateBox("LiftPostR2", new Vector3(0.25f, 2.6f, 0.25f), red, new Vector3(1.2f, 1.3f, -1.0f), root.transform);
            CreateBox("LiftArm1", new Vector3(1.2f, 0.12f, 0.18f), metal, new Vector3(-0.6f, 0.35f, 0.85f), root.transform);
            CreateBox("LiftArm2", new Vector3(1.2f, 0.12f, 0.18f), metal, new Vector3(-0.6f, 0.35f, -0.85f), root.transform);
            CreateBox("LiftArm3", new Vector3(1.2f, 0.12f, 0.18f), metal, new Vector3(0.6f, 0.35f, 0.85f), root.transform);
            CreateBox("LiftArm4", new Vector3(1.2f, 0.12f, 0.18f), metal, new Vector3(0.6f, 0.35f, -0.85f), root.transform);

            // Tool rack and workbench
            CreateBox("Workbench", new Vector3(2.2f, 0.9f, 0.7f), dark, new Vector3(0f, 0.45f, 2.6f), root.transform);
            CreateBox("WorkbenchTop", new Vector3(2.3f, 0.08f, 0.8f), metal, new Vector3(0f, 0.94f, 2.6f), root.transform);
            CreateToolRack(root.transform, new Vector3(2.4f, 0f, 2.4f));

            return root;
        }

        private void CreateToolRack(Transform parent, Vector3 pos)
        {
            Material metal = MakeMat("RackMetal", new Color(0.5f, 0.52f, 0.55f, 1f), 0.6f, 0.5f);
            Material red = MakeMat("ToolRed", new Color(0.85f, 0.2f, 0.15f, 1f), 0.1f, 0.4f);
            Material blue = MakeMat("ToolBlue", new Color(0.2f, 0.4f, 0.85f, 1f), 0.1f, 0.4f);
            GameObject rack = new GameObject("ToolRack");
            rack.transform.SetParent(parent, false);
            rack.transform.localPosition = pos;
            CreateBox("RackBoard", new Vector3(0.08f, 1.4f, 1.2f), metal, new Vector3(0f, 1.2f, 0f), rack.transform);
            for (int i = 0; i < 3; i++)
            {
                CreateBox("ToolA" + i, new Vector3(0.16f, 0.05f, 0.35f), i % 2 == 0 ? red : blue, new Vector3(-0.1f, 0.8f + i * 0.25f, -0.25f + i * 0.2f), rack.transform);
            }
        }

        private GameObject BuildPaintBay()
        {
            GameObject root = new GameObject("PaintBay");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(4.5f, 0f, 0f);

            Material metal = MakeMat("PaintMetal", new Color(0.55f, 0.57f, 0.6f, 1f), 0.6f, 0.5f);
            Material floor = MakeMat("PaintFloor", new Color(0.7f, 0.72f, 0.75f, 1f), 0.05f, 0.3f);

            // Turntable
            CreateCylinder("Turntable", 2.0f, 0.08f, metal, new Vector3(0f, 0.04f, 0f), root.transform);
            CreateCylinder("TurntableInner", 1.5f, 0.09f, floor, new Vector3(0f, 0.05f, 0f), root.transform);

            // Color swatches stand
            GameObject swatch = new GameObject("Swatches");
            swatch.transform.SetParent(root.transform, false);
            swatch.transform.localPosition = new Vector3(2.2f, 0f, 1.8f);
            CreateBox("SwatchPole", new Vector3(0.08f, 1.2f, 0.08f), metal, new Vector3(0f, 0.6f, 0f), swatch.transform);
            Color[] colors = { new Color(0.9f,0.2f,0.2f), new Color(0.2f,0.6f,1f), new Color(0.2f,0.8f,0.4f), new Color(1f,0.85f,0.2f) };
            for (int i = 0; i < colors.Length; i++)
            {
                CreateBox("Swatch_" + i, new Vector3(0.3f, 0.3f, 0.04f), MakeMat("Swatch" + i, colors[i], 0.1f, 0.7f), new Vector3(0f, 0.5f + i * 0.25f, 0.08f), swatch.transform);
            }

            // Paint booth frame
            CreateBox("BoothL", new Vector3(0.1f, 2.6f, 0.1f), metal, new Vector3(-2.1f, 1.3f, 2.0f), root.transform);
            CreateBox("BoothR", new Vector3(0.1f, 2.6f, 0.1f), metal, new Vector3(-2.1f, 1.3f, -2.0f), root.transform);
            CreateBox("BoothTop", new Vector3(0.1f, 0.1f, 4.1f), metal, new Vector3(-2.1f, 2.6f, 0f), root.transform);

            return root;
        }

        private void BuildAnchors()
        {
            GameObject wash = new GameObject("WashAnchor");
            wash.transform.SetParent(transform, false);
            wash.transform.localPosition = new Vector3(-4.5f, 0f, 0f);
            _washAnchor = wash.transform;

            GameObject repair = new GameObject("RepairAnchor");
            repair.transform.SetParent(transform, false);
            repair.transform.localPosition = new Vector3(0f, 0f, 0f);
            _repairAnchor = repair.transform;

            GameObject paint = new GameObject("PaintAnchor");
            paint.transform.SetParent(transform, false);
            paint.transform.localPosition = new Vector3(4.5f, 0f, 0f);
            _paintAnchor = paint.transform;
        }

        public Transform BayAnchor(WorkZone z)
        {
            switch (z)
            {
                case WorkZone.RepairBay: return _repairAnchor != null ? _repairAnchor : transform;
                case WorkZone.PaintBay: return _paintAnchor != null ? _paintAnchor : transform;
                default: return _washAnchor != null ? _washAnchor : transform;
            }
        }

        public void SetZone(WorkZone z)
        {
            _currentZone = z;
            if (_washLight != null) _washLight.intensity = z == WorkZone.WashBay ? 1.35f : 0.45f;
            if (_repairLight != null) _repairLight.intensity = z == WorkZone.RepairBay ? 1.3f : 0.45f;
            if (_paintLight != null) _paintLight.intensity = z == WorkZone.PaintBay ? 1.3f : 0.45f;
            if (_washProps != null) SetPropsEmphasis(_washProps, z == WorkZone.WashBay);
            if (_repairProps != null) SetPropsEmphasis(_repairProps, z == WorkZone.RepairBay);
            if (_paintProps != null) SetPropsEmphasis(_paintProps, z == WorkZone.PaintBay);
        }

        private static void SetPropsEmphasis(GameObject root, bool active)
        {
            // Light emphasis only; keep objects visible
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null) continue;
                // no-op visual change to avoid material instancing explosion; intensity handled by lights
            }
        }

        private GameObject CreateBox(string name, Vector3 size, Material mat, Vector3 pos, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private GameObject CreateCylinder(string name, float diameter, float height, Material mat, Vector3 pos, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(diameter, height, diameter);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeMat(string name, Color color, float metallic, float smoothness, bool transparent = false)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material m = new Material(shader);
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
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
    }
}
