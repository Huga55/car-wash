using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleGarage
{
    /// <summary>
    /// Unified pointer/touch router. Mouse: LMB paints, RMB/MMB orbits, wheel zoom.
    /// Touch: one finger paints, two fingers orbit/pinch.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action<Vector2> PointerDown;
        public event Action<Vector2> PointerMove;
        public event Action<Vector2> PointerUp;
        public event Action<Vector2> OrbitDelta;
        public event Action<float> ZoomDelta;

        public Vector2 LastPosition { get; private set; }

        private bool _paintEnabled = true;
        private bool _mousePainting;
        private bool _mouseOrbiting;
        private bool _touchPainting;
        private bool _touchOrbiting;
        private int _paintFingerId = -1;
        private Vector2 _lastMouse;
        private Vector2 _lastTouchPaint;
        private Vector2 _lastTouchMid;
        private float _lastPinchDistance;
        private readonly List<int> _uiBlockTouches = new List<int>(8);

        public void SetPaintEnabled(bool v)
        {
            _paintEnabled = v;
            if (!_paintEnabled)
            {
                if (_mousePainting)
                {
                    _mousePainting = false;
                    PointerUp?.Invoke(LastPosition);
                }

                if (_touchPainting)
                {
                    _touchPainting = false;
                    PointerUp?.Invoke(LastPosition);
                }
            }
        }

        private void Update()
        {
            if (Input.touchCount > 0)
            {
                HandleTouch();
                return;
            }

            HandleMouse();
        }

        private void HandleMouse()
        {
            Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            LastPosition = mouse;
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.0001f)
                ZoomDelta?.Invoke(wheel);

            bool overUI = IsPointerOverUI(-1);

            if (Input.GetMouseButtonDown(0) && _paintEnabled && !overUI)
            {
                _mousePainting = true;
                _lastMouse = mouse;
                PointerDown?.Invoke(mouse);
            }

            if (_mousePainting && Input.GetMouseButton(0))
            {
                if ((mouse - _lastMouse).sqrMagnitude > 0.001f)
                {
                    _lastMouse = mouse;
                    PointerMove?.Invoke(mouse);
                }
            }

            if (_mousePainting && Input.GetMouseButtonUp(0))
            {
                _mousePainting = false;
                PointerUp?.Invoke(mouse);
            }

            bool orbitButton = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (orbitButton && !_mouseOrbiting)
            {
                _mouseOrbiting = true;
                _lastMouse = mouse;
            }
            else if (orbitButton && _mouseOrbiting)
            {
                Vector2 delta = mouse - _lastMouse;
                _lastMouse = mouse;
                if (delta.sqrMagnitude > 0.001f)
                    OrbitDelta?.Invoke(delta);
            }
            else if (!orbitButton)
            {
                _mouseOrbiting = false;
            }
        }

        private void HandleTouch()
        {
            int touchCount = Input.touchCount;
            if (touchCount >= 2)
            {
                CancelTouchPaint();
                HandleTwoFingerOrbit();
                return;
            }

            _touchOrbiting = false;
            Touch touch = Input.GetTouch(0);
            LastPosition = touch.position;

            if (touch.phase == TouchPhase.Began)
            {
                bool blocked = IsPointerOverUI(touch.fingerId);
                if (blocked)
                {
                    TrackBlockedTouch(touch.fingerId);
                    return;
                }

                if (_paintEnabled)
                {
                    _touchPainting = true;
                    _paintFingerId = touch.fingerId;
                    _lastTouchPaint = touch.position;
                    PointerDown?.Invoke(touch.position);
                }

                return;
            }

            if (IsTouchBlocked(touch.fingerId))
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    UntrackBlockedTouch(touch.fingerId);
                return;
            }

            if (!_touchPainting || touch.fingerId != _paintFingerId) return;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if ((touch.position - _lastTouchPaint).sqrMagnitude > 0.001f)
                {
                    _lastTouchPaint = touch.position;
                    PointerMove?.Invoke(touch.position);
                }
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _touchPainting = false;
                _paintFingerId = -1;
                PointerUp?.Invoke(touch.position);
            }
        }

        private void HandleTwoFingerOrbit()
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            Vector2 mid = (t0.position + t1.position) * 0.5f;
            float pinch = Vector2.Distance(t0.position, t1.position);

            if (!_touchOrbiting)
            {
                _touchOrbiting = true;
                _lastTouchMid = mid;
                _lastPinchDistance = pinch;
                return;
            }

            Vector2 delta = mid - _lastTouchMid;
            float pinchDelta = pinch - _lastPinchDistance;
            _lastTouchMid = mid;
            _lastPinchDistance = pinch;

            if (delta.sqrMagnitude > 0.001f)
                OrbitDelta?.Invoke(delta);
            if (Mathf.Abs(pinchDelta) > 0.5f)
                ZoomDelta?.Invoke(pinchDelta * 0.005f);
        }

        private void CancelTouchPaint()
        {
            if (!_touchPainting) return;
            _touchPainting = false;
            _paintFingerId = -1;
            PointerUp?.Invoke(LastPosition);
        }

        private void TrackBlockedTouch(int fingerId)
        {
            if (!_uiBlockTouches.Contains(fingerId))
                _uiBlockTouches.Add(fingerId);
        }

        private bool IsTouchBlocked(int fingerId)
        {
            return _uiBlockTouches.Contains(fingerId);
        }

        private void UntrackBlockedTouch(int fingerId)
        {
            _uiBlockTouches.Remove(fingerId);
        }

        private static bool IsPointerOverUI(int fingerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (fingerId >= 0)
                return es.IsPointerOverGameObject(fingerId);
            return es.IsPointerOverGameObject();
        }
    }
}
