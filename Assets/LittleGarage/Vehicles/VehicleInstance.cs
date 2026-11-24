using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    public sealed class VehicleInstance : MonoBehaviour
    {
        public VehicleDef Def;
        public SurfaceStateSystem Surface;
        public Transform Root;
        public Transform HoodPivot;
        public readonly Dictionary<PartKind, VehiclePart> Parts = new Dictionary<PartKind, VehiclePart>();

        private readonly List<MeshCollider> _paintColliders = new List<MeshCollider>();
        private readonly List<Renderer> _paintRenderers = new List<Renderer>();
        private readonly List<Renderer> _rimRenderers = new List<Renderer>();
        private readonly List<Renderer> _decalRenderers = new List<Renderer>();
        private readonly List<Transform> _wheelVisuals = new List<Transform>();
        private readonly Dictionary<PartKind, Transform> _wheelAnchors = new Dictionary<PartKind, Transform>();

        private VehicleAppearance _appearance;
        private VehicleAppearance _defaultAppearance;
        private float _hoodAngle;
        private float _hoodTarget;
        private float _wheelSpin;
        private MaterialPropertyBlock _mpb;

        public MeshCollider[] PaintColliders { get { return _paintColliders.ToArray(); } }
        public Renderer[] PaintRenderers { get { return _paintRenderers.ToArray(); } }
        public IReadOnlyDictionary<PartKind, VehiclePart> PartsMap { get { return Parts; } }

        public void Initialize(VehicleDef def, Transform root, Transform hoodPivot)
        {
            Def = def;
            Root = root != null ? root : transform;
            HoodPivot = hoodPivot;
            _mpb = new MaterialPropertyBlock();
            _defaultAppearance = new VehicleAppearance
            {
                Body = def != null ? def.bodyColor : Color.white,
                Rim = 0,
                Decal = 0
            };
            _appearance = _defaultAppearance;
        }

        public void RegisterPaintTarget(MeshCollider collider, Renderer renderer)
        {
            if (collider != null && !_paintColliders.Contains(collider)) _paintColliders.Add(collider);
            if (renderer != null && !_paintRenderers.Contains(renderer)) _paintRenderers.Add(renderer);
        }

        public void RegisterRimRenderer(Renderer renderer)
        {
            if (renderer != null && !_rimRenderers.Contains(renderer)) _rimRenderers.Add(renderer);
        }

        public void RegisterDecalRenderer(Renderer renderer)
        {
            if (renderer != null && !_decalRenderers.Contains(renderer)) _decalRenderers.Add(renderer);
        }

        public void RegisterWheel(PartKind kind, Transform anchor, Transform visual)
        {
            if (anchor != null) _wheelAnchors[kind] = anchor;
            if (visual != null && !_wheelVisuals.Contains(visual)) _wheelVisuals.Add(visual);
        }

        public void RegisterPart(VehiclePart part)
        {
            if (part == null) return;
            Parts[part.Kind] = part;
        }

        public void SetBodyColor(Color c)
        {
            _appearance.Body = c;
            ApplyBodyColor(c);
        }

        private void ApplyBodyColor(Color c)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < _paintRenderers.Count; i++)
            {
                Renderer r = _paintRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", c);
                _mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void SetRimStyle(int i)
        {
            _appearance.Rim = Mathf.Max(0, i);
            Color rimColor;
            float metallic;
            float smooth;
            switch (_appearance.Rim % 3)
            {
                case 1:
                    rimColor = new Color(0.28f, 0.30f, 0.34f, 1f);
                    metallic = 0.75f;
                    smooth = 0.55f;
                    break;
                case 2:
                    rimColor = new Color(0.88f, 0.90f, 0.94f, 1f);
                    metallic = 1f;
                    smooth = 0.92f;
                    break;
                default:
                    rimColor = new Color(0.72f, 0.74f, 0.78f, 1f);
                    metallic = 0.65f;
                    smooth = 0.72f;
                    break;
            }
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int r = 0; r < _rimRenderers.Count; r++)
            {
                Renderer rend = _rimRenderers[r];
                if (rend == null) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", rimColor);
                _mpb.SetColor("_BaseColor", rimColor);
                _mpb.SetFloat("_Metallic", metallic);
                _mpb.SetFloat("_Glossiness", smooth);
                rend.SetPropertyBlock(_mpb);
                Material mat = rend.sharedMaterial;
                if (mat != null)
                {
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
                }
            }
        }

        public void SetDecal(int i)
        {
            _appearance.Decal = Mathf.Max(0, i);
            bool on = _appearance.Decal != 0;
            for (int d = 0; d < _decalRenderers.Count; d++)
            {
                Renderer r = _decalRenderers[d];
                if (r == null) continue;
                r.enabled = on;
                if (on && _mpb != null)
                {
                    r.GetPropertyBlock(_mpb);
                    Color decalColor = _appearance.Decal == 1 ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.88f, 0.2f, 0.95f);
                    _mpb.SetColor("_Color", decalColor);
                    _mpb.SetColor("_BaseColor", decalColor);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        public void RestoreAppearance()
        {
            ApplyAppearance(_defaultAppearance);
        }

        public Transform WheelAnchor(PartKind k)
        {
            Transform t;
            if (_wheelAnchors.TryGetValue(k, out t)) return t;
            VehiclePart part;
            if (Parts.TryGetValue(k, out part) && part != null) return part.transform;
            return null;
        }

        public VehicleAppearance CaptureAppearance()
        {
            return _appearance;
        }

        public void ApplyAppearance(VehicleAppearance a)
        {
            _appearance = a;
            ApplyBodyColor(a.Body);
            SetRimStyle(a.Rim);
            SetDecal(a.Decal);
        }

        public void SetHoodOpen(bool open)
        {
            _hoodTarget = open ? 52f : 0f;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (HoodPivot != null)
            {
                _hoodAngle = Mathf.MoveTowards(_hoodAngle, _hoodTarget, dt * 90f);
                HoodPivot.localRotation = Quaternion.Euler(_hoodAngle, 0f, 0f);
            }

            _wheelSpin += dt * 180f;
            for (int i = 0; i < _wheelVisuals.Count; i++)
            {
                Transform w = _wheelVisuals[i];
                if (w == null) continue;
                w.localRotation = Quaternion.Euler(0f, 0f, _wheelSpin);
            }

            foreach (KeyValuePair<PartKind, VehiclePart> kv in Parts)
            {
                if (kv.Value != null) kv.Value.Tick(dt);
            }
        }

        public void SetHighlight(bool on)
        {
            foreach (KeyValuePair<PartKind, VehiclePart> kv in Parts)
            {
                if (kv.Value != null) kv.Value.SetHighlight(on);
            }
        }

        public void SetPartHighlight(PartKind kind, bool on)
        {
            VehiclePart part;
            if (Parts.TryGetValue(kind, out part) && part != null)
                part.SetHighlight(on);
        }
    }
}
