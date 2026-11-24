using UnityEngine;

namespace LittleGarage
{
    public sealed class GarageCamera : MonoBehaviour
    {
        private Camera _camera;
        private Vector3 _target;
        private float _distance = 7.5f;
        private float _yaw = -32f;
        private float _pitch = 24f;
        private float _targetDistance = 7.5f;
        private Vector3 _targetFocus;
        private float _smooth = 8f;

        private void Awake()
        {
            EnsureCamera();
            _target = transform.position;
            _targetFocus = _target;
        }

        private void LateUpdate()
        {
            EnsureCamera();
            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_smooth * Time.deltaTime));
            _target = Vector3.Lerp(_target, _targetFocus, 1f - Mathf.Exp(-_smooth * Time.deltaTime));

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = _target - rot * Vector3.forward * _distance;
            _camera.transform.position = pos;
            _camera.transform.rotation = rot;
        }

        public void Focus(Vector3 target, float distance)
        {
            _targetFocus = target;
            _targetDistance = Mathf.Clamp(distance, 2.5f, 18f);
        }

        public void SetZone(WorkZone zone)
        {
            switch (zone)
            {
                case WorkZone.RepairBay:
                    _yaw = -24f;
                    _pitch = 28f;
                    break;
                case WorkZone.PaintBay:
                    _yaw = -40f;
                    _pitch = 22f;
                    break;
                default:
                    _yaw = -32f;
                    _pitch = 24f;
                    break;
            }
        }

        public void Orbit(Vector2 delta)
        {
            _yaw += delta.x * 0.15f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.12f, 12f, 55f);
        }

        public void Zoom(float delta)
        {
            _targetDistance = Mathf.Clamp(_targetDistance - delta * 0.02f, 2.5f, 18f);
        }

        private void EnsureCamera()
        {
            if (_camera != null) return;
            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f, 1f);
            // Game.unity ships an orthographic camera, but this rig's focus/zoom and
            // FOV are authored for perspective. Without this the car framing never
            // changes with cameraDistance and raycast targets are misframed.
            _camera.orthographic = false;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 80f;
            _camera.fieldOfView = 38f;
            if (_camera.GetComponent<AudioListener>() == null)
                _camera.gameObject.AddComponent<AudioListener>();
        }
    }
}
