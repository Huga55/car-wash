using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// Applies visual customization choices to a vehicle and commits them.
    /// </summary>
    public sealed class CustomizationSystem : MonoBehaviour
    {
        public event Action Changed;
        public event Action Completed;

        private VehicleInstance _car;
        private int _colorIndex;
        private int _rimIndex;
        private int _decalIndex;

        public void Begin(VehicleInstance car)
        {
            _car = car;
            _colorIndex = 0;
            _rimIndex = 0;
            _decalIndex = 0;
            if (_car != null)
            {
                VehicleAppearance current = _car.CaptureAppearance();
                _colorIndex = ResolveColorIndex(_car.Def, current.Body);
                _rimIndex = Mathf.Max(0, current.Rim);
                _decalIndex = Mathf.Max(0, current.Decal);
            }
        }

        public void SetColor(int i)
        {
            if (_car == null || _car.Def == null || _car.Def.colors == null || _car.Def.colors.Length == 0) return;
            _colorIndex = Mathf.Clamp(i, 0, _car.Def.colors.Length - 1);
            _car.SetBodyColor(_car.Def.colors[_colorIndex]);
            Changed?.Invoke();
        }

        public void SetRim(int i)
        {
            if (_car == null || _car.Def == null || _car.Def.rimStyles == null || _car.Def.rimStyles.Length == 0) return;
            _rimIndex = Mathf.Clamp(i, 0, _car.Def.rimStyles.Length - 1);
            _car.SetRimStyle(_rimIndex);
            Changed?.Invoke();
        }

        public void SetDecal(int i)
        {
            if (_car == null || _car.Def == null || _car.Def.decals == null || _car.Def.decals.Length == 0) return;
            _decalIndex = Mathf.Clamp(i, 0, _car.Def.decals.Length - 1);
            _car.SetDecal(_decalIndex);
            Changed?.Invoke();
        }

        public void Commit()
        {
            // Orchestrator owns job completion/persistence; customization emits events for UI/game flow.
            Completed?.Invoke();
        }

        private static int ResolveColorIndex(VehicleDef def, Color body)
        {
            if (def == null || def.colors == null || def.colors.Length == 0) return 0;
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < def.colors.Length; i++)
            {
                Color d = def.colors[i] - body;
                float dist = d.r * d.r + d.g * d.g + d.b * d.b;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }
    }
}
