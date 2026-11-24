using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// Drives wash task progression across one or more wash tools.
    /// </summary>
    public sealed class WashingSystem : MonoBehaviour
    {
        public float Progress { get; private set; }
        public bool IsComplete { get; private set; }
        public WashTool CurrentTool { get; private set; }

        public event Action Completed;
        public event Action<WashTool> ToolChanged;

        private VehicleInstance _car;
        private SurfaceStateSystem _surface;
        private WashTool[] _tools = new WashTool[0];
        private float[] _toolProgress = new float[0];
        private float[] _toolRadius = new float[0];
        private int _toolIndex;
        private bool _hasStroke;
        private Vector2 _lastUv;
        private bool _autoFinishing;
        private bool _completionFired;

        private static readonly WashTool[] EmptyTools = new WashTool[0];

        public void Begin(VehicleInstance car, WashTool[] tools, SurfaceStateSystem surface)
        {
            _car = car;
            _surface = surface;
            _tools = tools != null ? tools : EmptyTools;
            _toolProgress = new float[_tools.Length];
            _toolRadius = new float[_tools.Length];
            for (int i = 0; i < _tools.Length; i++)
            {
                _toolProgress[i] = 0f;
                _toolRadius[i] = DefaultRadius(_tools[i]);
            }

            _toolIndex = 0;
            _hasStroke = false;
            _lastUv = default(Vector2);
            _autoFinishing = false;
            _completionFired = false;
            IsComplete = false;
            Progress = 0f;

            if (_tools.Length > 0)
            {
                CurrentTool = _tools[0];
                ToolChanged?.Invoke(CurrentTool);
            }
        }

        public void SetTool(WashTool t)
        {
            int index = IndexOfTool(t);
            if (index < 0) return;
            // Sequential flow: the active tool and the immediately following tool can be
            // selected; later stages stay locked until they become current.
            if (index > _toolIndex + 1) return;
            if (_toolIndex == index && CurrentTool == t) return;

            _toolIndex = index;
            CurrentTool = t;
            _hasStroke = false;
            ToolChanged?.Invoke(CurrentTool);
        }

        public void SetToolRadius(WashTool tool, float radius)
        {
            int index = IndexOfTool(tool);
            if (index >= 0 && radius > 0f)
                _toolRadius[index] = radius;
        }

        public void ApplyStroke(Vector2 uv, float dt)
        {
            if (IsComplete || _surface == null || _tools.Length == 0) return;
            if (dt < 0f) dt = 0f;

            float radius = CurrentRadius();
            bool changed;
            if (_hasStroke)
                changed = _surface.PaintLine(_lastUv, uv, radius, CurrentTool);
            else
                changed = _surface.Paint(uv, radius, CurrentTool);

            _hasStroke = true;
            _lastUv = uv;

            if (changed || dt > 0f)
                UpdateProgressAndAdvance();
        }

        public void Advance(float dt)
        {
            if (IsComplete || _surface == null || _tools.Length == 0) return;

            UpdateProgressAndAdvance();
            if (IsComplete) return;

            if (_autoFinishing)
            {
                bool finished = _surface.AdvanceAutoFinish(Mathf.Max(0f, dt));
                UpdateProgressAndAdvance();
                if (finished)
                {
                    ForceCompleteAll();
                }
                return;
            }

            if (ShouldAutoFinish())
            {
                _autoFinishing = true;
                _surface.BeginAutoFinish();
            }
        }

        private void UpdateProgressAndAdvance()
        {
            if (_tools.Length == 0)
            {
                Progress = 1f;
                CompleteIfNeeded();
                return;
            }

            for (int i = 0; i < _tools.Length; i++)
            {
                float p = EvaluateToolProgress(_tools[i]);
                if (p > _toolProgress[i]) _toolProgress[i] = p;
            }

            while (_toolIndex < _tools.Length - 1 && ToolGoalReached(_tools[_toolIndex]))
            {
                _toolProgress[_toolIndex] = 1f;
                _toolIndex++;
                CurrentTool = _tools[_toolIndex];
                _hasStroke = false;
                ToolChanged?.Invoke(CurrentTool);
                float p = EvaluateToolProgress(CurrentTool);
                if (p > _toolProgress[_toolIndex]) _toolProgress[_toolIndex] = p;
            }

            Progress = ComputeOverallProgress();
            if (AllToolsDone())
            {
                ForceCompleteAll();
            }
        }

        private float ComputeOverallProgress()
        {
            if (_toolProgress.Length == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < _toolProgress.Length; i++)
                sum += Mathf.Clamp01(_toolProgress[i]);
            return Mathf.Clamp01(sum / _toolProgress.Length);
        }

        private bool AllToolsDone()
        {
            for (int i = 0; i < _tools.Length; i++)
            {
                if (!ToolGoalReached(_tools[i])) return false;
            }
            return _tools.Length > 0;
        }

        private void ForceCompleteAll()
        {
            for (int i = 0; i < _toolProgress.Length; i++)
                _toolProgress[i] = 1f;
            Progress = 1f;
            CompleteIfNeeded();
        }

        private void CompleteIfNeeded()
        {
            if (IsComplete) return;
            IsComplete = true;
            Progress = 1f;
            if (!_completionFired)
            {
                _completionFired = true;
                Completed?.Invoke();
            }
        }

        private bool ShouldAutoFinish()
        {
            if (_surface == null || _tools.Length == 0) return false;
            if (_toolIndex != _tools.Length - 1) return false;

            // Auto-finish only when all earlier tools are done and final tool is close enough.
            for (int i = 0; i < _tools.Length - 1; i++)
            {
                if (!ToolGoalReached(_tools[i])) return false;
            }

            float final = EvaluateToolProgress(_tools[_tools.Length - 1]);
            return final >= 0.995f;
        }

        private float EvaluateToolProgress(WashTool tool)
        {
            if (_surface == null) return 0f;
            switch (tool)
            {
                case WashTool.Pressure:
                    return Mathf.Clamp01(_surface.Cleanliness / 0.90f);
                case WashTool.Foam:
                    return Mathf.Clamp01(_surface.FoamCoverage / 0.85f);
                case WashTool.Sponge:
                    return Mathf.Clamp01(_surface.Cleanliness / 0.97f);
                case WashTool.Rinse:
                {
                    float foamGone = 1f - Mathf.Clamp01(_surface.FoamCoverage / 0.08f);
                    float clean = Mathf.Clamp01(_surface.Cleanliness / 0.98f);
                    return Mathf.Clamp01(foamGone * 0.75f + clean * 0.25f);
                }
                case WashTool.Dryer:
                    return Mathf.Clamp01(_surface.Dryness / 0.98f);
                case WashTool.Polisher:
                    return Mathf.Clamp01(_surface.Polish / 0.85f);
                default:
                    return 0f;
            }
        }

        private bool ToolGoalReached(WashTool tool)
        {
            if (_surface == null) return false;
            switch (tool)
            {
                case WashTool.Pressure:
                    return _surface.Cleanliness >= 0.90f;
                case WashTool.Foam:
                    return _surface.FoamCoverage >= 0.85f;
                case WashTool.Sponge:
                    return _surface.Cleanliness >= 0.97f && _surface.FoamCoverage <= 0.30f;
                case WashTool.Rinse:
                    return _surface.FoamCoverage <= 0.03f && _surface.Cleanliness >= 0.98f;
                case WashTool.Dryer:
                    return _surface.Dryness >= 0.98f && _surface.FoamCoverage <= 0.02f;
                case WashTool.Polisher:
                    return _surface.Polish >= 0.85f && _surface.Cleanliness >= 0.99f && _surface.Dryness >= 0.98f;
                default:
                    return false;
            }
        }

        private int IndexOfTool(WashTool tool)
        {
            for (int i = 0; i < _tools.Length; i++)
                if (_tools[i] == tool) return i;
            return -1;
        }

        private float CurrentRadius()
        {
            if (_toolIndex >= 0 && _toolIndex < _toolRadius.Length && _toolRadius[_toolIndex] > 0f)
                return _toolRadius[_toolIndex];
            return DefaultRadius(CurrentTool);
        }

        private static float DefaultRadius(WashTool tool)
        {
            switch (tool)
            {
                case WashTool.Pressure: return 0.14f;
                case WashTool.Foam: return 0.15f;
                case WashTool.Sponge: return 0.12f;
                case WashTool.Rinse: return 0.16f;
                case WashTool.Dryer: return 0.17f;
                case WashTool.Polisher: return 0.14f;
                default: return 0.08f;
            }
        }
    }
}
