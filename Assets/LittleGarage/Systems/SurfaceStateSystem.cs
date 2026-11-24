using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// 256x256 RGBA surface state map. R=dirt, G=wet, B=foam, A=polish.
    /// Maintains cached channel sums and applies circular brush strokes with no per-stroke allocations.
    /// </summary>
    public sealed class SurfaceStateSystem
    {
        public const int Size = 256;
        private const float InvSize = 1f / Size;
        private const int PixelCount = Size * Size;
        private const float AutoFinishDuration = 1.0f;

        private readonly Color32[] _pixels;
        private readonly Color32[] _applyBuffer;
        private Texture2D _map;

        private int _dirtyMinX;
        private int _dirtyMinY;
        private int _dirtyMaxX;
        private int _dirtyMaxY;
        private bool _dirty;

        private long _sumDirt;
        private long _sumWet;
        private long _sumFoam;
        private long _sumPolish;

        private bool _autoFinishing;
        private float _autoFinishTime;
        private float _autoFinishDirtStart;
        private float _autoFinishWetStart;
        private float _autoFinishFoamStart;

        public Texture2D Map { get { return _map; } }

        public float Cleanliness { get { return 1f - Average(_sumDirt); } }
        public float FoamCoverage { get { return Average(_sumFoam); } }
        public float Dryness { get { return 1f - Average(_sumWet); } }
        public float Polish { get { return Average(_sumPolish); } }

        public SurfaceStateSystem()
        {
            _pixels = new Color32[PixelCount];
            _applyBuffer = new Color32[PixelCount];
            CreateMap();
            Clear(0f);
        }

        public void Reset(VehicleDef def)
        {
            float dirt = def != null ? Mathf.Clamp01(def.dirt) : 0.9f;
            int seed = def != null ? StableHash(def.id) : 12345;
            FillDeterministic(dirt, seed);
            _autoFinishing = false;
            _autoFinishTime = 0f;
            MarkAllDirty();
            ApplyDirtyRect();
        }

        public bool Paint(Vector2 uv, float radius, WashTool tool)
        {
            return PaintInternal(uv, radius, tool);
        }

        public bool PaintLine(Vector2 a, Vector2 b, float radius, WashTool tool)
        {
            float dist = Vector2.Distance(a, b);
            float step = Mathf.Max(radius * 0.35f, 0.002f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / step));
            bool changed = false;
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : (float)i / steps;
                Vector2 uv = Vector2.Lerp(a, b, t);
                changed |= PaintInternal(uv, radius, tool);
            }

            return changed;
        }

        public void BeginAutoFinish()
        {
            if (_autoFinishing) return;
            _autoFinishing = true;
            _autoFinishTime = 0f;
            _autoFinishDirtStart = Average(_sumDirt);
            _autoFinishWetStart = Average(_sumWet);
            _autoFinishFoamStart = Average(_sumFoam);
        }

        public bool AdvanceAutoFinish(float dt)
        {
            if (!_autoFinishing) return IsWashFinished();

            _autoFinishTime += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(_autoFinishTime / AutoFinishDuration);
            float smooth = t * t * (3f - 2f * t);

            float targetDirt = Mathf.Lerp(_autoFinishDirtStart, 0f, smooth);
            float targetWet = Mathf.Lerp(_autoFinishWetStart, 0f, smooth);
            float targetFoam = Mathf.Lerp(_autoFinishFoamStart, 0f, smooth);

            bool changed = false;
            for (int i = 0; i < PixelCount; i++)
            {
                Color32 p = _pixels[i];
                byte dirt = MoveToward(p.r, targetDirt, smooth);
                byte wet = MoveToward(p.g, targetWet, smooth);
                byte foam = MoveToward(p.b, targetFoam, smooth);
                if (dirt == p.r && wet == p.g && foam == p.b) continue;

                _sumDirt += dirt - p.r;
                _sumWet += wet - p.g;
                _sumFoam += foam - p.b;
                p.r = dirt;
                p.g = wet;
                p.b = foam;
                _pixels[i] = p;
                changed = true;
            }

            if (changed)
            {
                MarkAllDirty();
                ApplyDirtyRect();
            }

            if (t >= 1f)
            {
                _autoFinishing = false;
                ForceFinish();
                return true;
            }

            return IsWashFinished();
        }

        public void ForceFinish()
        {
            bool changed = false;
            for (int i = 0; i < PixelCount; i++)
            {
                Color32 p = _pixels[i];
                if (p.r == 0 && p.g == 0 && p.b == 0 && p.a >= 250) continue;
                _sumDirt -= p.r;
                _sumWet -= p.g;
                _sumFoam -= p.b;
                _sumPolish += (byte)255 - p.a;
                p.r = 0;
                p.g = 0;
                p.b = 0;
                p.a = 255;
                _pixels[i] = p;
                changed = true;
            }
            _autoFinishing = false;
            if (changed)
            {
                MarkAllDirty();
                ApplyDirtyRect();
            }
        }

        public bool IsWashFinished()
        {
            return Average(_sumDirt) <= 0.001f && Average(_sumWet) <= 0.001f && Average(_sumFoam) <= 0.001f;
        }

        private bool PaintInternal(Vector2 uv, float radius, WashTool tool)
        {
            if (_map == null || radius <= 0f) return false;

            int centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (Size - 1)), 0, Size - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (Size - 1)), 0, Size - 1);
            int pixelRadius = Mathf.Max(1, Mathf.CeilToInt(radius * Size));
            int minX = Mathf.Max(0, centerX - pixelRadius);
            int maxX = Mathf.Min(Size - 1, centerX + pixelRadius);
            int minY = Mathf.Max(0, centerY - pixelRadius);
            int maxY = Mathf.Min(Size - 1, centerY + pixelRadius);
            float radiusSq = (float)pixelRadius * pixelRadius;

            bool changed = false;
            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - centerY;
                int row = y * Size;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - centerX;
                    float distSq = dx * dx + dy * dy;
                    if (distSq > radiusSq) continue;

                    float edge = 1f - Mathf.Clamp01(Mathf.Sqrt(distSq) / pixelRadius);
                    float strength = edge * edge;
                    if (strength <= 0f) continue;

                    int idx = row + x;
                    if (ApplyTool(ref _pixels[idx], tool, strength))
                    {
                        changed = true;
                        ExpandDirty(x, y);
                    }
                }
            }

            if (changed) ApplyDirtyRect();
            return changed;
        }

        private bool ApplyTool(ref Color32 pixel, WashTool tool, float strength)
        {
            byte r = pixel.r;
            byte g = pixel.g;
            byte b = pixel.b;
            byte a = pixel.a;

            float dirt = r / 255f;
            float wet = g / 255f;
            float foam = b / 255f;
            float polish = a / 255f;

            switch (tool)
            {
                case WashTool.Pressure:
                    dirt = Mathf.Max(0f, dirt - strength * 0.32f);
                    wet = Mathf.Clamp01(wet + strength * 0.55f);
                    foam = Mathf.Max(0f, foam - strength * 0.10f);
                    break;
                case WashTool.Foam:
                    if (wet > 0.05f || dirt > 0.05f)
                        foam = Mathf.Clamp01(foam + strength * 0.75f);
                    wet = Mathf.Clamp01(wet + strength * 0.20f);
                    break;
                case WashTool.Sponge:
                    if (foam > 0.02f)
                    {
                        float scrub = strength * (0.35f + foam * 0.65f);
                        dirt = Mathf.Max(0f, dirt - scrub * 0.45f);
                        foam = Mathf.Max(0f, foam - scrub * 0.35f);
                        wet = Mathf.Clamp01(wet + scrub * 0.08f);
                    }
                    break;
                case WashTool.Rinse:
                    foam = Mathf.Max(0f, foam - strength * 0.85f);
                    dirt = Mathf.Max(0f, dirt - strength * 0.08f);
                    wet = Mathf.Clamp01(Mathf.Max(wet, strength * 0.45f));
                    break;
                case WashTool.Dryer:
                    wet = Mathf.Max(0f, wet - strength * 0.60f);
                    foam = Mathf.Max(0f, foam - strength * 0.15f);
                    break;
                case WashTool.Polisher:
                    if (dirt <= 0.03f && wet <= 0.03f && foam <= 0.03f)
                        polish = Mathf.Clamp01(polish + strength * 0.45f);
                    break;
            }

            byte nr = ToByte(dirt);
            byte ng = ToByte(wet);
            byte nb = ToByte(foam);
            byte na = ToByte(polish);
            if (nr == r && ng == g && nb == b && na == a) return false;

            _sumDirt += nr - r;
            _sumWet += ng - g;
            _sumFoam += nb - b;
            _sumPolish += na - a;
            pixel.r = nr;
            pixel.g = ng;
            pixel.b = nb;
            pixel.a = na;
            return true;
        }

        private void CreateMap()
        {
            _map = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            _map.name = "SurfaceState";
            _map.wrapMode = TextureWrapMode.Clamp;
            _map.filterMode = FilterMode.Bilinear;
        }

        private void Clear(float dirt)
        {
            byte d = ToByte(Mathf.Clamp01(dirt));
            _sumDirt = (long)d * PixelCount;
            _sumWet = 0;
            _sumFoam = 0;
            _sumPolish = 0;
            for (int i = 0; i < PixelCount; i++)
            {
                _pixels[i] = new Color32(d, 0, 0, 0);
            }
        }

        private void FillDeterministic(float dirtAmount, int seed)
        {
            _sumDirt = 0;
            _sumWet = 0;
            _sumFoam = 0;
            _sumPolish = 0;
            for (int y = 0; y < Size; y++)
            {
                int row = y * Size;
                for (int x = 0; x < Size; x++)
                {
                    float nx = x * InvSize;
                    float ny = y * InvSize;
                    float noise = Fract(Mathf.Sin((nx * 127.1f + ny * 311.7f + seed * 0.013f) * 43758.5453f));
                    float blotch = Fract(Mathf.Sin((nx * 19.7f + ny * 43.3f + seed * 0.071f) * 125.123f));
                    float amount = Mathf.Clamp01(dirtAmount * (0.55f + noise * 0.45f) * (0.75f + blotch * 0.35f));
                    byte d = ToByte(amount);
                    int idx = row + x;
                    _pixels[idx] = new Color32(d, 0, 0, 0);
                    _sumDirt += d;
                }
            }
        }

        private void MarkAllDirty()
        {
            _dirty = true;
            _dirtyMinX = 0;
            _dirtyMinY = 0;
            _dirtyMaxX = Size - 1;
            _dirtyMaxY = Size - 1;
        }

        private void ExpandDirty(int x, int y)
        {
            if (!_dirty)
            {
                _dirty = true;
                _dirtyMinX = _dirtyMaxX = x;
                _dirtyMinY = _dirtyMaxY = y;
                return;
            }

            if (x < _dirtyMinX) _dirtyMinX = x;
            if (x > _dirtyMaxX) _dirtyMaxX = x;
            if (y < _dirtyMinY) _dirtyMinY = y;
            if (y > _dirtyMaxY) _dirtyMaxY = y;
        }

        private void ApplyDirtyRect()
        {
            if (!_dirty || _map == null) return;

            int width = _dirtyMaxX - _dirtyMinX + 1;
            int height = _dirtyMaxY - _dirtyMinY + 1;
            if (width <= 0 || height <= 0)
            {
                _dirty = false;
                return;
            }

            // Full texture buffer is persistent; copy dirty rect into apply buffer to avoid per-stroke allocations.
            for (int y = 0; y < height; y++)
            {
                int srcRow = (_dirtyMinY + y) * Size + _dirtyMinX;
                int dstRow = y * width;
                Array.Copy(_pixels, srcRow, _applyBuffer, dstRow, width);
            }

            _map.SetPixels32(_dirtyMinX, _dirtyMinY, width, height, _applyBuffer);
            _map.Apply(false, false);

            _dirty = false;
        }

        private static byte MoveToward(byte current, float target01, float smooth)
        {
            float current01 = current / 255f;
            float next = Mathf.Lerp(current01, target01, Mathf.Clamp01(smooth));
            if (Mathf.Abs(next - target01) < 0.004f) next = target01;
            return ToByte(next);
        }

        private static float Average(long sum)
        {
            return Mathf.Clamp01(sum / (PixelCount * 255f));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static float Fract(float value)
        {
            return value - Mathf.Floor(value);
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }
    }
}
