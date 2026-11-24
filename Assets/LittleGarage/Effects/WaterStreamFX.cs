using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// World-space nozzle + continuous stream used by the active LittleGarage wash path.
    /// It deliberately uses only LineRenderers (no primitive colliders) so it cannot
    /// intercept the washing raycast, and it is driven from GameManager.Update so a
    /// stationary pointer still shows and applies water.
    /// </summary>
    public sealed class WaterStreamFX : MonoBehaviour
    {
        private Camera _camera;
        private LineRenderer _stream;
        private LineRenderer _tool;
        private bool _active;
        private Vector3 _target;
        private Vector3 _hitNormal = Vector3.up;
        private WashTool _activeTool = WashTool.Pressure;

        public bool IsActive { get { return _active; } }
        public Vector3 Target { get { return _target; } }
        public Vector3 HitNormal { get { return _hitNormal; } }
        public WashTool Tool { get { return _activeTool; } }
        public LineRenderer StreamRenderer { get { return _stream; } }
        public LineRenderer NozzleRenderer { get { return _tool; } }

        public void Initialize(Camera camera)
        {
            _camera = camera != null ? camera : Camera.main;
            if (_stream == null) _stream = CreateLine("Water Stream", 2);
            if (_tool == null) _tool = CreateLine("Pressure Nozzle", 2);

            _stream.useWorldSpace = true;
            _tool.useWorldSpace = true;
            _tool.startColor = new Color(0.16f, 0.19f, 0.24f, 1f);
            _tool.endColor = new Color(0.34f, 0.38f, 0.44f, 1f);
            _tool.startWidth = 0.085f;
            _tool.endWidth = 0.045f;

            _target = NozzlePoint + Vector3.forward * 2f;
            Stop();
        }

        public void Begin(WashTool tool)
        {
            if (_stream == null || _tool == null) Initialize(_camera);
            _activeTool = tool;
            _active = true;
            ApplyStyle(tool);
            _stream.enabled = true;
            _tool.enabled = true;
            RefreshPositions();
        }

        public void Retarget(Vector3 point, Vector3 normal)
        {
            _target = point;
            _hitNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            if (_active) RefreshPositions();
        }

        public void Stop()
        {
            _active = false;
            if (_stream != null) _stream.enabled = false;
            if (_tool != null) _tool.enabled = false;
        }

        private void LateUpdate()
        {
            if (_active) RefreshPositions();
        }

        private void RefreshPositions()
        {
            if (_stream == null || _tool == null) return;
            if (_camera == null) _camera = Camera.main;

            Vector3 nozzle = NozzlePoint;
            Vector3 grip = GripPoint;
            _tool.SetPosition(0, grip);
            _tool.SetPosition(1, nozzle);

            // Offset the visible stream slightly along the surface normal so the beam
            // does not z-fight with the paint mesh at the impact point.
            Vector3 impact = _target + _hitNormal * 0.035f;
            _stream.SetPosition(0, nozzle);
            _stream.SetPosition(1, impact);
        }

        private Vector3 NozzlePoint
        {
            get
            {
                if (_camera == null) return transform.position + new Vector3(0.9f, -0.55f, 1.5f);
                return _camera.ViewportToWorldPoint(new Vector3(0.82f, 0.16f, 1.6f));
            }
        }

        private Vector3 GripPoint
        {
            get
            {
                if (_camera == null) return transform.position + new Vector3(0.5f, -0.9f, 1.2f);
                return _camera.ViewportToWorldPoint(new Vector3(0.68f, 0.04f, 1.3f));
            }
        }

        private void ApplyStyle(WashTool tool)
        {
            Color color;
            float width;
            switch (tool)
            {
                case WashTool.Foam:
                    color = new Color(0.96f, 0.98f, 1f, 0.95f);
                    width = 0.048f;
                    break;
                case WashTool.Sponge:
                    color = new Color(1f, 0.84f, 0.36f, 0.92f);
                    width = 0.055f;
                    break;
                case WashTool.Rinse:
                    color = new Color(0.36f, 0.94f, 1f, 0.9f);
                    width = 0.015f;
                    break;
                case WashTool.Dryer:
                    color = new Color(0.82f, 0.94f, 1f, 0.55f);
                    width = 0.038f;
                    break;
                case WashTool.Polisher:
                    color = new Color(1f, 0.66f, 0.9f, 0.9f);
                    width = 0.052f;
                    break;
                default:
                    color = new Color(0.52f, 0.84f, 1f, 0.92f);
                    width = 0.02f;
                    break;
            }

            _stream.startColor = color;
            _stream.endColor = new Color(color.r, color.g, color.b, color.a * 0.75f);
            _stream.startWidth = width;
            _stream.endWidth = width * 1.6f;
        }

        private static LineRenderer CreateLine(string name, int positions)
        {
            GameObject go = new GameObject(name);
            LineRenderer line = go.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.name = name + " Mat";
                line.sharedMaterial = mat;
            }
            line.positionCount = positions;
            line.enabled = false;
            return line;
        }
    }
}
