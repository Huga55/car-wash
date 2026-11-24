using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// Real multi-step repair state machine with gesture-driven interactions.
    /// </summary>
    public sealed class RepairSystem : MonoBehaviour
    {
        private enum RepairMode
        {
            None,
            Wheel,
            Engine,
            Windshield,
            SimpleDetachAttach
        }

        private enum WheelStage
        {
            Unscrew,
            DetachWheel,
            DragNewWheel,
            AttachWheel,
            Tighten,
            Done
        }

        private enum EngineStage
        {
            OpenHood,
            RemoveOld,
            InsertNew,
            CloseHood,
            Done
        }

        private enum WindshieldStage
        {
            RemoveBroken,
            WipeFrame,
            InstallNew,
            Done
        }

        private enum SimpleStage
        {
            Detach,
            Attach,
            Done
        }

        public float Progress { get; private set; }
        public bool IsComplete { get; private set; }
        public string CurrentInstructionRu { get; private set; }

        public event Action Completed;

        private VehicleInstance _car;
        private PartKind _part;
        private RepairMode _mode;
        private Camera _camera;

        private WheelStage _wheelStage;
        private EngineStage _engineStage;
        private WindshieldStage _windshieldStage;
        private SimpleStage _simpleStage;

        private readonly List<Transform> _fasteners = new List<Transform>(8);
        private readonly List<Vector3> _fastenerLocalStarts = new List<Vector3>(8);
        private readonly List<float> _fastenerProgress = new List<float>(8);
        private int _fastenerIndex;

        private VehiclePart _targetPart;
        private Transform _targetTransform;
        private Vector3 _targetStartLocalPos;
        private Quaternion _targetStartLocalRot;
        private Vector3 _targetStartLocalScale;
        private Vector3 _targetRemovedLocalPos;
        private Vector3 _targetInstallLocalPos;

        private GameObject _tray;
        private GameObject _newPartProxy;
        private Vector3 _newPartStartPos;
        private bool _draggingNewPart;
        private bool _draggingTarget;
        private Vector3 _dragOffsetWorld;
        private float _wipeAmount;
        private float _hoodT;
        private bool _hoodOpen;
        private bool _completionFired;

        public void Begin(VehicleInstance car, PartKind part)
        {
            CleanupTransient();
            _car = car;
            _part = part;
            _camera = Camera.main;
            _targetPart = FindPart(car, part);
            _targetTransform = _targetPart != null ? _targetPart.transform : FindPartTransform(car, part);
            CacheTargetTransform();

            _mode = ResolveMode(part);
            _wheelStage = WheelStage.Unscrew;
            _engineStage = EngineStage.OpenHood;
            _windshieldStage = WindshieldStage.RemoveBroken;
            _simpleStage = SimpleStage.Detach;
            _fasteners.Clear();
            _fastenerLocalStarts.Clear();
            _fastenerProgress.Clear();
            _fastenerIndex = 0;
            _draggingNewPart = false;
            _draggingTarget = false;
            _wipeAmount = 0f;
            _hoodT = 0f;
            _hoodOpen = false;
            _completionFired = false;
            IsComplete = false;
            Progress = 0f;

            SetupMode();
            UpdateInstruction();
            UpdateProgress();
        }

        public void PointerDown(GameObject hitObject, Vector2 screen)
        {
            if (IsComplete || _car == null) return;
            if (_camera == null) _camera = Camera.main;

            switch (_mode)
            {
                case RepairMode.Wheel:
                    HandleWheelPointerDown(hitObject, screen);
                    break;
                case RepairMode.Engine:
                    HandleEnginePointerDown(hitObject, screen);
                    break;
                case RepairMode.Windshield:
                    HandleWindshieldPointerDown(hitObject, screen);
                    break;
                case RepairMode.SimpleDetachAttach:
                    HandleSimplePointerDown(hitObject, screen);
                    break;
            }

            UpdateInstruction();
            UpdateProgress();
        }

        public void PointerMove(Vector2 screen)
        {
            if (IsComplete || _car == null) return;
            if (_camera == null) _camera = Camera.main;

            switch (_mode)
            {
                case RepairMode.Wheel:
                    HandleWheelPointerMove(screen);
                    break;
                case RepairMode.Engine:
                    HandleEnginePointerMove(screen);
                    break;
                case RepairMode.Windshield:
                    HandleWindshieldPointerMove(screen);
                    break;
                case RepairMode.SimpleDetachAttach:
                    HandleSimplePointerMove(screen);
                    break;
            }

            UpdateInstruction();
            UpdateProgress();
        }

        public void PointerUp()
        {
            _draggingNewPart = false;
            _draggingTarget = false;
            if (_mode == RepairMode.Wheel && _wheelStage == WheelStage.Tighten)
            {
                // Tighten progresses by repeated clicks/taps in PointerDown; release does nothing.
            }
        }

        public void Advance(float dt)
        {
            if (IsComplete) return;
            dt = Mathf.Max(0f, dt);

            AnimateFasteners(dt);
            AnimateTarget(dt);
            AnimateHood(dt);
            AnimateNewPart(dt);

            UpdateInstruction();
            UpdateProgress();
            CompleteIfNeeded();
        }

        private void SetupMode()
        {
            if (_targetPart != null)
                _targetPart.SetBroken(true);

            switch (_mode)
            {
                case RepairMode.Wheel:
                    SetupWheel();
                    break;
                case RepairMode.Engine:
                    SetupEngine();
                    break;
                case RepairMode.Windshield:
                    SetupWindshield();
                    break;
                case RepairMode.SimpleDetachAttach:
                    SetupSimple();
                    break;
            }
        }

        private void SetupWheel()
        {
            CreateFasteners(_targetTransform, 4);
            _wheelStage = WheelStage.Unscrew;
            EnsureTray();
            CreateNewPartProxy();
            if (_newPartProxy != null) _newPartProxy.SetActive(false);
        }

        private void SetupEngine()
        {
            _engineStage = EngineStage.OpenHood;
            _hoodOpen = false;
            _hoodT = 0f;
            EnsureTray();
            CreateNewPartProxy();
            if (_newPartProxy != null) _newPartProxy.SetActive(false);
            if (_targetPart != null) _targetPart.SetInstalled(true);
        }

        private void SetupWindshield()
        {
            _windshieldStage = WindshieldStage.RemoveBroken;
            EnsureTray();
            CreateNewPartProxy();
            if (_newPartProxy != null) _newPartProxy.SetActive(false);
        }

        private void SetupSimple()
        {
            _simpleStage = SimpleStage.Detach;
            EnsureTray();
            CreateNewPartProxy();
            if (_newPartProxy != null) _newPartProxy.SetActive(false);
        }

        private void HandleWheelPointerDown(GameObject hitObject, Vector2 screen)
        {
            if (_wheelStage == WheelStage.Unscrew)
            {
                int idx = HitFastenerIndex(hitObject);
                if (idx >= 0 && idx == _fastenerIndex)
                {
                    _fastenerProgress[idx] = Mathf.Min(1f, _fastenerProgress[idx] + 0.34f);
                    if (_fastenerProgress[idx] >= 1f)
                    {
                        _fastenerIndex++;
                        if (_fastenerIndex >= _fasteners.Count)
                        {
                            _wheelStage = WheelStage.DetachWheel;
                        }
                    }
                }
                return;
            }

            if (_wheelStage == WheelStage.DetachWheel)
            {
                if (IsTargetHit(hitObject))
                {
                    _draggingTarget = true;
                    BeginDrag(screen, _targetTransform, out _dragOffsetWorld);
                }
                return;
            }

            if (_wheelStage == WheelStage.DragNewWheel)
            {
                if (_newPartProxy != null && IsHitObject(hitObject, _newPartProxy))
                {
                    _draggingNewPart = true;
                    BeginDrag(screen, _newPartProxy.transform, out _dragOffsetWorld);
                }
                return;
            }

            if (_wheelStage == WheelStage.AttachWheel)
            {
                if (IsTargetHit(hitObject) || (_newPartProxy != null && IsHitObject(hitObject, _newPartProxy)))
                {
                    _wheelStage = WheelStage.Tighten;
                    _fastenerIndex = 0;
                    for (int i = 0; i < _fastenerProgress.Count; i++) _fastenerProgress[i] = 0f;
                    SetFastenersVisible(true);
                    ResetFastenersLocal();
                }
                return;
            }

            if (_wheelStage == WheelStage.Tighten)
            {
                int idx = HitFastenerIndex(hitObject);
                if (idx >= 0 && idx == _fastenerIndex)
                {
                    _fastenerProgress[idx] = Mathf.Min(1f, _fastenerProgress[idx] + 0.34f);
                    if (_fastenerProgress[idx] >= 1f)
                    {
                        _fastenerIndex++;
                        if (_fastenerIndex >= _fasteners.Count)
                            _wheelStage = WheelStage.Done;
                    }
                }
            }
        }

        private void HandleWheelPointerMove(Vector2 screen)
        {
            if (_wheelStage == WheelStage.DetachWheel && _draggingTarget && _targetTransform != null)
            {
                DragTransform(screen, _targetTransform, _dragOffsetWorld);
                if (IsNearTray(_targetTransform.position))
                {
                    _draggingTarget = false;
                    SetTargetInstalled(false);
                    SetTargetVisible(false);
                    _wheelStage = WheelStage.DragNewWheel;
                    if (_newPartProxy != null)
                    {
                        _newPartProxy.SetActive(true);
                        _newPartProxy.transform.position = _newPartStartPos;
                    }
                }
                return;
            }

            if (_wheelStage == WheelStage.DragNewWheel && _draggingNewPart && _newPartProxy != null)
            {
                DragTransform(screen, _newPartProxy.transform, _dragOffsetWorld);
                if (IsNearTargetMount(_newPartProxy.transform.position))
                {
                    _draggingNewPart = false;
                    SnapNewPartToMount();
                    SetTargetVisible(true);
                    SetTargetInstalled(true);
                    if (_targetPart != null) _targetPart.SetBroken(false);
                    _wheelStage = WheelStage.AttachWheel;
                }
            }
        }

        private void HandleEnginePointerDown(GameObject hitObject, Vector2 screen)
        {
            if (_engineStage == EngineStage.OpenHood)
            {
                if (IsHoodHit(hitObject) || IsTargetHit(hitObject))
                {
                    _hoodOpen = true;
                    _engineStage = EngineStage.RemoveOld;
                    if (_targetPart != null) _targetPart.SetBroken(true);
                }
                return;
            }

            if (_engineStage == EngineStage.RemoveOld)
            {
                if (IsTargetHit(hitObject))
                {
                    _draggingTarget = true;
                    BeginDrag(screen, _targetTransform, out _dragOffsetWorld);
                }
                return;
            }

            if (_engineStage == EngineStage.InsertNew)
            {
                if (_newPartProxy != null && IsHitObject(hitObject, _newPartProxy))
                {
                    _draggingNewPart = true;
                    BeginDrag(screen, _newPartProxy.transform, out _dragOffsetWorld);
                }
                return;
            }

            if (_engineStage == EngineStage.CloseHood)
            {
                if (IsHoodHit(hitObject) || IsTargetHit(hitObject))
                {
                    _hoodOpen = false;
                    _engineStage = EngineStage.Done;
                }
            }
        }

        private void HandleEnginePointerMove(Vector2 screen)
        {
            if (_engineStage == EngineStage.RemoveOld && _draggingTarget && _targetTransform != null)
            {
                DragTransform(screen, _targetTransform, _dragOffsetWorld);
                if (IsNearTray(_targetTransform.position))
                {
                    _draggingTarget = false;
                    SetTargetInstalled(false);
                    SetTargetVisible(false);
                    _engineStage = EngineStage.InsertNew;
                    if (_newPartProxy != null)
                    {
                        _newPartProxy.SetActive(true);
                        _newPartProxy.transform.position = _newPartStartPos;
                    }
                }
                return;
            }

            if (_engineStage == EngineStage.InsertNew && _draggingNewPart && _newPartProxy != null)
            {
                DragTransform(screen, _newPartProxy.transform, _dragOffsetWorld);
                if (IsNearTargetMount(_newPartProxy.transform.position))
                {
                    _draggingNewPart = false;
                    SnapNewPartToMount();
                    SetTargetVisible(true);
                    SetTargetInstalled(true);
                    if (_targetPart != null) _targetPart.SetBroken(false);
                    _engineStage = EngineStage.CloseHood;
                }
            }
        }

        private void HandleWindshieldPointerDown(GameObject hitObject, Vector2 screen)
        {
            if (_windshieldStage == WindshieldStage.RemoveBroken)
            {
                if (IsTargetHit(hitObject))
                {
                    _draggingTarget = true;
                    BeginDrag(screen, _targetTransform, out _dragOffsetWorld);
                }
                return;
            }

            if (_windshieldStage == WindshieldStage.WipeFrame)
            {
                if (IsTargetHit(hitObject) || IsNearTargetMount(ScreenToWorldOnTargetPlane(screen)))
                {
                    _wipeAmount = Mathf.Clamp01(_wipeAmount + 0.12f);
                    if (_wipeAmount >= 1f)
                    {
                        _windshieldStage = WindshieldStage.InstallNew;
                        if (_newPartProxy != null)
                        {
                            _newPartProxy.SetActive(true);
                            _newPartProxy.transform.position = _newPartStartPos;
                        }
                    }
                }
                return;
            }

            if (_windshieldStage == WindshieldStage.InstallNew)
            {
                if (_newPartProxy != null && IsHitObject(hitObject, _newPartProxy))
                {
                    _draggingNewPart = true;
                    BeginDrag(screen, _newPartProxy.transform, out _dragOffsetWorld);
                }
            }
        }

        private void HandleWindshieldPointerMove(Vector2 screen)
        {
            if (_windshieldStage == WindshieldStage.RemoveBroken && _draggingTarget && _targetTransform != null)
            {
                DragTransform(screen, _targetTransform, _dragOffsetWorld);
                if (IsNearTray(_targetTransform.position))
                {
                    _draggingTarget = false;
                    SetTargetInstalled(false);
                    SetTargetVisible(false);
                    _windshieldStage = WindshieldStage.WipeFrame;
                    _wipeAmount = 0f;
                }
                return;
            }

            if (_windshieldStage == WindshieldStage.WipeFrame)
            {
                if (Input.GetMouseButton(0) || Input.touchCount > 0)
                {
                    _wipeAmount = Mathf.Clamp01(_wipeAmount + Time.deltaTime * 0.35f);
                    if (_wipeAmount >= 1f)
                    {
                        _windshieldStage = WindshieldStage.InstallNew;
                        if (_newPartProxy != null)
                        {
                            _newPartProxy.SetActive(true);
                            _newPartProxy.transform.position = _newPartStartPos;
                        }
                    }
                }
                return;
            }

            if (_windshieldStage == WindshieldStage.InstallNew && _draggingNewPart && _newPartProxy != null)
            {
                DragTransform(screen, _newPartProxy.transform, _dragOffsetWorld);
                if (IsNearTargetMount(_newPartProxy.transform.position))
                {
                    _draggingNewPart = false;
                    SnapNewPartToMount();
                    SetTargetVisible(true);
                    SetTargetInstalled(true);
                    if (_targetPart != null) _targetPart.SetBroken(false);
                    _windshieldStage = WindshieldStage.Done;
                }
            }
        }

        private void HandleSimplePointerDown(GameObject hitObject, Vector2 screen)
        {
            if (_simpleStage == SimpleStage.Detach)
            {
                if (IsTargetHit(hitObject))
                {
                    _draggingTarget = true;
                    BeginDrag(screen, _targetTransform, out _dragOffsetWorld);
                }
                return;
            }

            if (_simpleStage == SimpleStage.Attach)
            {
                if (_newPartProxy != null && IsHitObject(hitObject, _newPartProxy))
                {
                    _draggingNewPart = true;
                    BeginDrag(screen, _newPartProxy.transform, out _dragOffsetWorld);
                }
            }
        }

        private void HandleSimplePointerMove(Vector2 screen)
        {
            if (_simpleStage == SimpleStage.Detach && _draggingTarget && _targetTransform != null)
            {
                DragTransform(screen, _targetTransform, _dragOffsetWorld);
                if (IsNearTray(_targetTransform.position))
                {
                    _draggingTarget = false;
                    SetTargetInstalled(false);
                    SetTargetVisible(false);
                    _simpleStage = SimpleStage.Attach;
                    if (_newPartProxy != null)
                    {
                        _newPartProxy.SetActive(true);
                        _newPartProxy.transform.position = _newPartStartPos;
                    }
                }
                return;
            }

            if (_simpleStage == SimpleStage.Attach && _draggingNewPart && _newPartProxy != null)
            {
                DragTransform(screen, _newPartProxy.transform, _dragOffsetWorld);
                if (IsNearTargetMount(_newPartProxy.transform.position))
                {
                    _draggingNewPart = false;
                    SnapNewPartToMount();
                    SetTargetVisible(true);
                    SetTargetInstalled(true);
                    if (_targetPart != null) _targetPart.SetBroken(false);
                    _simpleStage = SimpleStage.Done;
                }
            }
        }

        private void CreateFasteners(Transform around, int count)
        {
            ClearFasteners();
            if (around == null) return;

            Vector3 center = around.position;
            Vector3 right = around.right;
            Vector3 up = around.up;
            float radius = 0.18f;
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count;
                Vector3 local = right * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius;
                GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bolt.name = "Fastener_" + i;
                bolt.transform.SetParent(_car != null ? _car.transform : around, true);
                bolt.transform.position = center + local + around.forward * 0.08f;
                bolt.transform.rotation = Quaternion.LookRotation(around.forward, up);
                bolt.transform.localScale = new Vector3(0.04f, 0.015f, 0.04f);
                SetRendererColor(bolt, new Color(0.75f, 0.75f, 0.78f));
                _fasteners.Add(bolt.transform);
                _fastenerLocalStarts.Add(bolt.transform.localPosition);
                _fastenerProgress.Add(0f);
            }
        }

        private void ClearFasteners()
        {
            for (int i = 0; i < _fasteners.Count; i++)
            {
                if (_fasteners[i] != null)
                    Destroy(_fasteners[i].gameObject);
            }
            _fasteners.Clear();
            _fastenerLocalStarts.Clear();
            _fastenerProgress.Clear();
            _fastenerIndex = 0;
        }

        private void SetFastenersVisible(bool visible)
        {
            for (int i = 0; i < _fasteners.Count; i++)
            {
                if (_fasteners[i] != null)
                    _fasteners[i].gameObject.SetActive(visible);
            }
        }

        private void ResetFastenersLocal()
        {
            for (int i = 0; i < _fasteners.Count; i++)
            {
                if (_fasteners[i] == null) continue;
                if (i < _fastenerLocalStarts.Count)
                    _fasteners[i].localPosition = _fastenerLocalStarts[i];
            }
        }

        private int HitFastenerIndex(GameObject hitObject)
        {
            if (hitObject == null) return -1;
            for (int i = 0; i < _fasteners.Count; i++)
            {
                Transform f = _fasteners[i];
                if (f == null) continue;
                if (hitObject.transform == f || hitObject.transform.IsChildOf(f))
                    return i;
            }
            return -1;
        }

        private void AnimateFasteners(float dt)
        {
            if (_fasteners.Count == 0) return;
            float speed = dt * 8f;
            for (int i = 0; i < _fasteners.Count; i++)
            {
                Transform f = _fasteners[i];
                if (f == null || i >= _fastenerLocalStarts.Count) continue;
                Vector3 start = _fastenerLocalStarts[i];
                float p = i < _fastenerProgress.Count ? _fastenerProgress[i] : 0f;

                Vector3 target;
                if (_mode == RepairMode.Wheel && _wheelStage == WheelStage.Tighten)
                {
                    // During tighten, 0 means loose/out, 1 means fully tightened in.
                    target = start + Vector3.up * (1f - p) * 0.08f;
                }
                else
                {
                    target = start + Vector3.up * p * 0.08f;
                }

                f.localPosition = Vector3.Lerp(f.localPosition, target, speed);
                float s = 1f - Mathf.Clamp01(p) * 0.15f;
                f.localScale = Vector3.Lerp(f.localScale, new Vector3(0.04f * s, 0.015f, 0.04f * s), speed);
            }
        }

        private void AnimateTarget(float dt)
        {
            if (_targetTransform == null) return;

            if (_draggingTarget || _draggingNewPart) return;

            if (_mode == RepairMode.Wheel)
            {
                if (_wheelStage == WheelStage.AttachWheel)
                {
                    _targetTransform.localPosition = Vector3.Lerp(_targetTransform.localPosition, _targetStartLocalPos, dt * 8f);
                    _targetTransform.localRotation = Quaternion.Slerp(_targetTransform.localRotation, _targetStartLocalRot, dt * 8f);
                }
                else if (_wheelStage == WheelStage.Tighten || _wheelStage == WheelStage.Done)
                {
                    _targetTransform.localPosition = Vector3.Lerp(_targetTransform.localPosition, _targetStartLocalPos, dt * 10f);
                    _targetTransform.localRotation = Quaternion.Slerp(_targetTransform.localRotation, _targetStartLocalRot, dt * 10f);
                }
            }
            else if ((_mode == RepairMode.Engine && (_engineStage == EngineStage.CloseHood || _engineStage == EngineStage.Done)) ||
                     (_mode == RepairMode.Windshield && _windshieldStage == WindshieldStage.Done) ||
                     (_mode == RepairMode.SimpleDetachAttach && _simpleStage == SimpleStage.Done))
            {
                _targetTransform.localPosition = Vector3.Lerp(_targetTransform.localPosition, _targetStartLocalPos, dt * 8f);
                _targetTransform.localRotation = Quaternion.Slerp(_targetTransform.localRotation, _targetStartLocalRot, dt * 8f);
            }
        }

        private void AnimateHood(float dt)
        {
            Transform hood = FindHoodPivot(_car);
            if (hood == null) return;
            float target = _hoodOpen ? 1f : 0f;
            _hoodT = Mathf.MoveTowards(_hoodT, target, dt * 2.2f);
            float angle = Mathf.Lerp(0f, -58f, Mathf.SmoothStep(0f, 1f, _hoodT));
            Vector3 e = hood.localEulerAngles;
            e.x = angle;
            hood.localEulerAngles = e;
        }

        private void AnimateNewPart(float dt)
        {
            if (_newPartProxy == null || !_newPartProxy.activeSelf || _draggingNewPart) return;
            if (_mode == RepairMode.Wheel && _wheelStage == WheelStage.AttachWheel)
            {
                _newPartProxy.transform.position = Vector3.Lerp(_newPartProxy.transform.position, GetMountWorldPosition(), dt * 8f);
            }
        }

        private void UpdateInstruction()
        {
            switch (_mode)
            {
                case RepairMode.Wheel:
                    CurrentInstructionRu = WheelInstruction(_wheelStage);
                    break;
                case RepairMode.Engine:
                    CurrentInstructionRu = EngineInstruction(_engineStage);
                    break;
                case RepairMode.Windshield:
                    CurrentInstructionRu = WindshieldInstruction(_windshieldStage);
                    break;
                case RepairMode.SimpleDetachAttach:
                    CurrentInstructionRu = SimpleInstruction(_simpleStage);
                    break;
                default:
                    CurrentInstructionRu = "Ремонт завершён";
                    break;
            }
        }

        private void UpdateProgress()
        {
            float p = 0f;
            switch (_mode)
            {
                case RepairMode.Wheel:
                    p = WheelProgress();
                    IsComplete = _wheelStage == WheelStage.Done;
                    break;
                case RepairMode.Engine:
                    p = EngineProgress();
                    IsComplete = _engineStage == EngineStage.Done;
                    break;
                case RepairMode.Windshield:
                    p = WindshieldProgress();
                    IsComplete = _windshieldStage == WindshieldStage.Done;
                    break;
                case RepairMode.SimpleDetachAttach:
                    p = SimpleProgress();
                    IsComplete = _simpleStage == SimpleStage.Done;
                    break;
            }

            Progress = Mathf.Clamp01(p);
        }

        private float WheelProgress()
        {
            float unscrew = AverageFasteners();
            float detach = _wheelStage > WheelStage.DetachWheel ? 1f : 0f;
            float dragNew = _wheelStage > WheelStage.DragNewWheel ? 1f : (_wheelStage == WheelStage.DragNewWheel && _newPartProxy != null ? 0.2f : 0f);
            float attach = _wheelStage > WheelStage.AttachWheel ? 1f : 0f;
            float tighten = _wheelStage == WheelStage.Tighten ? AverageFasteners() : (_wheelStage > WheelStage.Tighten ? 1f : 0f);
            return (unscrew + detach + dragNew + attach + tighten) / 5f;
        }

        private float EngineProgress()
        {
            float open = _engineStage > EngineStage.OpenHood ? 1f : Mathf.Clamp01(_hoodT);
            float remove = _engineStage > EngineStage.RemoveOld ? 1f : 0f;
            float insert = _engineStage > EngineStage.InsertNew ? 1f : 0f;
            float close = _engineStage == EngineStage.Done ? 1f : (_engineStage == EngineStage.CloseHood ? 1f - Mathf.Clamp01(_hoodT) * 0.5f : 0f);
            return (open + remove + insert + close) / 4f;
        }

        private float WindshieldProgress()
        {
            float remove = _windshieldStage > WindshieldStage.RemoveBroken ? 1f : 0f;
            float wipe = _windshieldStage == WindshieldStage.WipeFrame ? _wipeAmount : (_windshieldStage > WindshieldStage.WipeFrame ? 1f : 0f);
            float install = _windshieldStage == WindshieldStage.Done ? 1f : 0f;
            return (remove + wipe + install) / 3f;
        }

        private float SimpleProgress()
        {
            float detach = _simpleStage > SimpleStage.Detach ? 1f : 0f;
            float attach = _simpleStage == SimpleStage.Done ? 1f : 0f;
            return (detach + attach) / 2f;
        }

        private float AverageFasteners()
        {
            if (_fastenerProgress.Count == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < _fastenerProgress.Count; i++) sum += Mathf.Clamp01(_fastenerProgress[i]);
            return sum / _fastenerProgress.Count;
        }

        private void CompleteIfNeeded()
        {
            if (!IsComplete || _completionFired) return;
            _completionFired = true;
            if (_targetPart != null)
            {
                _targetPart.SetBroken(false);
                _targetPart.SetInstalled(true);
            }
            Completed?.Invoke();
        }

        private string WheelInstruction(WheelStage stage)
        {
            switch (stage)
            {
                case WheelStage.Unscrew: return "Открути крепления колеса";
                case WheelStage.DetachWheel: return "Сними колесо и перетащи его на поддон";
                case WheelStage.DragNewWheel: return "Перетащи новое колесо из поддона к оси";
                case WheelStage.AttachWheel: return "Установи колесо на место";
                case WheelStage.Tighten: return "Закрути крепления колеса";
                default: return "Колесо заменено";
            }
        }

        private string EngineInstruction(EngineStage stage)
        {
            switch (stage)
            {
                case EngineStage.OpenHood: return "Открой капот";
                case EngineStage.RemoveOld: return "Сними старую деталь двигателя";
                case EngineStage.InsertNew: return "Установи новую деталь двигателя";
                case EngineStage.CloseHood: return "Закрой капот";
                default: return "Двигатель отремонтирован";
            }
        }

        private string WindshieldInstruction(WindshieldStage stage)
        {
            switch (stage)
            {
                case WindshieldStage.RemoveBroken: return "Сними разбитое стекло";
                case WindshieldStage.WipeFrame: return "Протри рамку стекла";
                case WindshieldStage.InstallNew: return "Установи новое стекло";
                default: return "Стекло заменено";
            }
        }

        private string SimpleInstruction(SimpleStage stage)
        {
            if (stage == SimpleStage.Detach) return "Сними деталь: " + _part.Ru();
            if (stage == SimpleStage.Attach) return "Установи новую деталь: " + _part.Ru();
            return "Деталь установлена";
        }

        private static RepairMode ResolveMode(PartKind part)
        {
            if (GameTypes.IsWheel(part)) return RepairMode.Wheel;
            if (part == PartKind.Engine) return RepairMode.Engine;
            if (part == PartKind.Windshield) return RepairMode.Windshield;
            return RepairMode.SimpleDetachAttach;
        }

        private void CacheTargetTransform()
        {
            if (_targetTransform == null)
            {
                _targetStartLocalPos = Vector3.zero;
                _targetStartLocalRot = Quaternion.identity;
                _targetStartLocalScale = Vector3.one;
                _targetRemovedLocalPos = Vector3.zero;
                _targetInstallLocalPos = Vector3.zero;
                return;
            }

            _targetStartLocalPos = _targetTransform.localPosition;
            _targetStartLocalRot = _targetTransform.localRotation;
            _targetStartLocalScale = _targetTransform.localScale;
            _targetRemovedLocalPos = _targetStartLocalPos + Vector3.up * 0.3f;
            _targetInstallLocalPos = _targetStartLocalPos + Vector3.up * 0.12f;
        }

        private void EnsureTray()
        {
            if (_tray != null) return;
            _tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _tray.name = "RepairTray";
            _tray.transform.SetParent(_car != null ? _car.transform.parent : null, true);
            Vector3 basePos = _car != null ? _car.transform.position : Vector3.zero;
            _tray.transform.position = basePos + new Vector3(1.2f, 0.06f, -0.3f);
            _tray.transform.localScale = new Vector3(0.7f, 0.08f, 0.5f);
            SetRendererColor(_tray, new Color(0.28f, 0.3f, 0.34f));
            Collider c = _tray.GetComponent<Collider>();
            if (c != null) c.isTrigger = false;
        }

        private void CreateNewPartProxy()
        {
            if (_newPartProxy != null) Destroy(_newPartProxy);
            _newPartProxy = CreateProxyForPart(_part, _targetTransform);
            _newPartProxy.name = "NewPart_" + _part;
            _newPartProxy.transform.SetParent(_car != null ? _car.transform.parent : null, true);
            _newPartStartPos = (_tray != null ? _tray.transform.position : Vector3.zero) + Vector3.up * 0.18f;
            _newPartProxy.transform.position = _newPartStartPos;
            _newPartProxy.SetActive(false);
        }

        private GameObject CreateProxyForPart(PartKind part, Transform reference)
        {
            GameObject go;
            if (GameTypes.IsWheel(part))
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(0.7f, 0.18f, 0.7f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                SetRendererColor(go, new Color(0.15f, 0.15f, 0.16f));
            }
            else if (part == PartKind.Engine)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(0.6f, 0.35f, 0.5f);
                SetRendererColor(go, new Color(0.45f, 0.45f, 0.5f));
            }
            else if (part == PartKind.Windshield)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(1.2f, 0.6f, 0.05f);
                SetRendererColor(go, new Color(0.7f, 0.9f, 1f, 0.85f));
            }
            else if (part == PartKind.Battery)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(0.35f, 0.22f, 0.25f);
                SetRendererColor(go, new Color(0.2f, 0.2f, 0.22f));
            }
            else if (part == PartKind.HeadlightL || part == PartKind.HeadlightR)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(0.22f, 0.22f, 0.12f);
                SetRendererColor(go, new Color(1f, 0.98f, 0.85f));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = reference != null ? reference.lossyScale : new Vector3(0.35f, 0.2f, 0.2f);
                SetRendererColor(go, new Color(0.7f, 0.7f, 0.75f));
            }

            Collider c = go.GetComponent<Collider>();
            if (c == null) c = go.AddComponent<BoxCollider>();
            c.isTrigger = false;
            return go;
        }

        private void SnapNewPartToMount()
        {
            if (_newPartProxy == null) return;
            Vector3 mount = GetMountWorldPosition();
            _newPartProxy.transform.position = mount;
            if (_targetTransform != null)
            {
                _targetTransform.position = mount;
                _targetTransform.localRotation = _targetStartLocalRot;
                _targetTransform.localScale = _targetStartLocalScale;
            }
        }

        private Vector3 GetMountWorldPosition()
        {
            if (_targetTransform != null && _targetTransform.parent != null)
                return _targetTransform.parent.TransformPoint(_targetStartLocalPos);
            if (_targetTransform != null)
                return _targetTransform.position;
            return _car != null ? _car.transform.position : Vector3.zero;
        }

        private bool IsNearTray(Vector3 worldPos)
        {
            if (_tray == null) return false;
            Vector3 d = worldPos - (_tray.transform.position + Vector3.up * 0.15f);
            return d.sqrMagnitude <= 0.35f * 0.35f;
        }

        private bool IsNearTargetMount(Vector3 worldPos)
        {
            Vector3 mount = GetMountWorldPosition();
            return (worldPos - mount).sqrMagnitude <= 0.35f * 0.35f;
        }

        private bool IsTargetHit(GameObject hitObject)
        {
            if (hitObject == null) return false;
            if (_targetPart != null)
            {
                if (hitObject.transform == _targetPart.transform || hitObject.transform.IsChildOf(_targetPart.transform))
                    return true;
            }
            if (_targetTransform != null)
            {
                if (hitObject.transform == _targetTransform || hitObject.transform.IsChildOf(_targetTransform))
                    return true;
            }
            return HasPartKindHint(hitObject, _part);
        }

        private bool IsHoodHit(GameObject hitObject)
        {
            if (hitObject == null) return false;
            Transform hood = FindHoodPivot(_car);
            if (hood != null && (hitObject.transform == hood || hitObject.transform.IsChildOf(hood)))
                return true;
            string n = hitObject.name.ToLowerInvariant();
            return n.Contains("hood") || n.Contains("kapot") || n.Contains("bonnet");
        }

        private static bool IsHitObject(GameObject hitObject, GameObject target)
        {
            if (hitObject == null || target == null) return false;
            return hitObject.transform == target.transform || hitObject.transform.IsChildOf(target.transform);
        }

        private void BeginDrag(Vector2 screen, Transform t, out Vector3 offsetWorld)
        {
            offsetWorld = Vector3.zero;
            if (_camera == null || t == null) return;
            Plane plane = new Plane(Vector3.up, t.position);
            Ray ray = _camera.ScreenPointToRay(screen);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                offsetWorld = t.position - hit;
            }
        }

        private void DragTransform(Vector2 screen, Transform t, Vector3 offsetWorld)
        {
            if (_camera == null || t == null) return;
            Plane plane = new Plane(Vector3.up, t.position);
            Ray ray = _camera.ScreenPointToRay(screen);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                t.position = hit + offsetWorld;
            }
        }

        private Vector3 ScreenToWorldOnTargetPlane(Vector2 screen)
        {
            if (_camera == null) _camera = Camera.main;
            Vector3 origin = GetMountWorldPosition() + Vector3.up * 0.1f;
            Plane plane = new Plane(_camera != null ? -_camera.transform.forward : Vector3.forward, origin);
            Ray ray = _camera != null ? _camera.ScreenPointToRay(screen) : new Ray(origin, Vector3.forward);
            float enter;
            if (plane.Raycast(ray, out enter))
                return ray.GetPoint(enter);
            return origin;
        }

        private void SetTargetVisible(bool visible)
        {
            if (_targetTransform == null) return;
            Renderer[] renderers = _targetTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
            Collider[] colliders = _targetTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = visible;
        }

        private void SetTargetInstalled(bool installed)
        {
            if (_targetPart != null)
                _targetPart.SetInstalled(installed);
        }

        private static VehiclePart FindPart(VehicleInstance car, PartKind part)
        {
            if (car == null) return null;
            if (car.Parts != null)
            {
                VehiclePart found;
                if (car.Parts.TryGetValue(part, out found) && found != null)
                    return found;
            }

            VehiclePart[] parts = car.GetComponentsInChildren<VehiclePart>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].Kind == part)
                    return parts[i];
            }

            return null;
        }

        private static Transform FindPartTransform(VehicleInstance car, PartKind part)
        {
            if (car == null) return null;
            Transform[] all = car.GetComponentsInChildren<Transform>(true);
            string kindName = part.ToString().ToLowerInvariant();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                VehiclePart vp = t.GetComponent<VehiclePart>();
                if (vp != null && vp.Kind == part) return t;
                string n = t.name.ToLowerInvariant();
                if (n.Contains(kindName)) return t;
            }

            return null;
        }

        private static bool HasPartKindHint(GameObject obj, PartKind part)
        {
            if (obj == null) return false;
            string n = obj.name.ToLowerInvariant();
            string kind = part.ToString().ToLowerInvariant();
            if (n.Contains(kind)) return true;
            if (GameTypes.IsWheel(part) && n.Contains("wheel")) return true;
            if (part == PartKind.Windshield && (n.Contains("glass") || n.Contains("wind"))) return true;
            if (part == PartKind.Engine && (n.Contains("engine") || n.Contains("motor"))) return true;
            return false;
        }

        private static Transform FindHoodPivot(VehicleInstance car)
        {
            if (car == null) return null;
            if (car.HoodPivot != null) return car.HoodPivot;
            Transform[] all = car.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hood") || n.Contains("kapot") || n.Contains("bonnet"))
                    return t;
            }
            return null;
        }

        private static void SetRendererColor(GameObject go, Color color)
        {
            if (go == null) return;
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            Shader shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            r.sharedMaterial = mat;
        }

        private void CleanupTransient()
        {
            ClearFasteners();
            if (_newPartProxy != null)
            {
                Destroy(_newPartProxy);
                _newPartProxy = null;
            }
            if (_tray != null)
            {
                Destroy(_tray);
                _tray = null;
            }
        }

        private void OnDestroy()
        {
            CleanupTransient();
        }
    }
}
