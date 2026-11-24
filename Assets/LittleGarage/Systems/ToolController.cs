using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// Tracks selected wash tool and creates simple procedural hand-tool visuals.
    /// </summary>
    public sealed class ToolController : MonoBehaviour
    {
        public WashTool Current { get; private set; }

        public event Action<WashTool> Changed;

        private WashTool[] _available = new WashTool[0];

        public void Select(WashTool t)
        {
            if (!IsAvailable(t) && _available.Length > 0)
                return;
            if (Current == t && _available.Length > 0) return;
            Current = t;
            Changed?.Invoke(Current);
        }

        public void SetAvailable(WashTool[] tools)
        {
            _available = tools != null ? tools : new WashTool[0];
            if (_available.Length == 0) return;
            bool currentAvailable = IsAvailable(Current);
            if (!currentAvailable)
            {
                Current = _available[0];
                Changed?.Invoke(Current);
            }
        }

        public GameObject CreateHandTool(WashTool t)
        {
            GameObject root = new GameObject("Tool_" + t);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            switch (t)
            {
                case WashTool.Pressure:
                    BuildPressure(root.transform);
                    break;
                case WashTool.Foam:
                    BuildFoamGun(root.transform);
                    break;
                case WashTool.Sponge:
                    BuildSponge(root.transform);
                    break;
                case WashTool.Rinse:
                    BuildRinseWand(root.transform);
                    break;
                case WashTool.Dryer:
                    BuildDryer(root.transform);
                    break;
                case WashTool.Polisher:
                    BuildPolisher(root.transform);
                    break;
                default:
                    BuildSponge(root.transform);
                    break;
            }

            return root;
        }

        private bool IsAvailable(WashTool t)
        {
            for (int i = 0; i < _available.Length; i++)
                if (_available[i] == t) return true;
            return false;
        }

        private static void BuildPressure(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Cylinder, "Body", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.22f, 0.12f), new Color(0.2f, 0.55f, 0.95f));
            CreatePart(parent, PrimitiveType.Cylinder, "Nozzle", new Vector3(0.26f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.12f, 0.07f), new Color(0.15f, 0.15f, 0.18f));
            CreatePart(parent, PrimitiveType.Cube, "Handle", new Vector3(-0.12f, -0.12f, 0f), new Vector3(0f, 0f, 15f), new Vector3(0.08f, 0.22f, 0.09f), new Color(0.12f, 0.12f, 0.12f));
            CreatePart(parent, PrimitiveType.Cube, "Trigger", new Vector3(-0.02f, -0.08f, 0f), new Vector3(0f, 0f, -20f), new Vector3(0.03f, 0.12f, 0.05f), new Color(0.9f, 0.3f, 0.2f));
        }

        private static void BuildFoamGun(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Cylinder, "Tank", new Vector3(-0.05f, 0.08f, 0f), Vector3.zero, new Vector3(0.18f, 0.16f, 0.18f), new Color(0.95f, 0.95f, 0.98f));
            CreatePart(parent, PrimitiveType.Cylinder, "Neck", new Vector3(0.08f, 0.02f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.09f, 0.12f, 0.09f), new Color(0.3f, 0.75f, 0.95f));
            CreatePart(parent, PrimitiveType.Cube, "Nozzle", new Vector3(0.24f, 0.02f, 0f), Vector3.zero, new Vector3(0.12f, 0.07f, 0.08f), new Color(0.2f, 0.2f, 0.25f));
            CreatePart(parent, PrimitiveType.Cube, "Handle", new Vector3(-0.08f, -0.12f, 0f), new Vector3(0f, 0f, 10f), new Vector3(0.07f, 0.2f, 0.08f), new Color(0.25f, 0.25f, 0.28f));
        }

        private static void BuildSponge(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Cube, "Sponge", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.22f, 0.12f, 0.14f), new Color(1f, 0.86f, 0.25f));
            CreatePart(parent, PrimitiveType.Cube, "Scrub", new Vector3(0f, -0.065f, 0f), Vector3.zero, new Vector3(0.23f, 0.02f, 0.15f), new Color(0.2f, 0.65f, 0.3f));
        }

        private static void BuildRinseWand(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Cylinder, "Wand", new Vector3(0.1f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.32f, 0.07f), new Color(0.55f, 0.85f, 1f));
            CreatePart(parent, PrimitiveType.Cube, "Head", new Vector3(0.42f, 0f, 0f), Vector3.zero, new Vector3(0.12f, 0.08f, 0.1f), new Color(0.2f, 0.45f, 0.8f));
            CreatePart(parent, PrimitiveType.Cube, "Handle", new Vector3(-0.18f, -0.08f, 0f), new Vector3(0f, 0f, 12f), new Vector3(0.08f, 0.2f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
        }

        private static void BuildDryer(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.16f, 0.2f, 0.16f), new Color(0.95f, 0.72f, 0.2f));
            CreatePart(parent, PrimitiveType.Cylinder, "Nozzle", new Vector3(0.28f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.1f, 0.12f), new Color(0.25f, 0.25f, 0.28f));
            CreatePart(parent, PrimitiveType.Cube, "Handle", new Vector3(-0.12f, -0.14f, 0f), Vector3.zero, new Vector3(0.09f, 0.22f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
        }

        private static void BuildPolisher(Transform parent)
        {
            CreatePart(parent, PrimitiveType.Capsule, "Motor", new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.85f, 0.2f, 0.2f));
            CreatePart(parent, PrimitiveType.Cylinder, "Pad", new Vector3(0f, -0.08f, 0f), Vector3.zero, new Vector3(0.28f, 0.03f, 0.28f), new Color(0.93f, 0.93f, 0.95f));
            CreatePart(parent, PrimitiveType.Cube, "Handle", new Vector3(-0.16f, 0.08f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0.07f, 0.18f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
        }

        private static void CreatePart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                Material mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
                renderer.sharedMaterial = mat;
            }
        }
    }
}
