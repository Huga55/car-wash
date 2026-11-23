using System;
using UnityEngine;

public enum WashStage { Wet, Foam, Rinse, Scrub, Dry, Done }

/// <summary>
/// The paintable car surface. Five small masks (wet, foam, clean, scrubbed, dry)
/// are composited into ONE 256x256 texture over the car art. Only the dirty
/// rectangle of the brush is rebuilt and uploaded, keeping this cheap on weak
/// Android tablets.
/// </summary>
public sealed class WashSurface
{
    public const int W = 256;
    public const int H = 256;
    private const int N = W * H;

    // Six simple logical zones (3 columns x upper/lower row). Every stage must
    // reach ZoneFinish in ALL zones before it can advance: painting one spot
    // over and over can no longer finish a stage.
    public const int ZoneCount = 6;
    public const float ZoneFinish = 0.82f;

    private readonly Texture2D _texture;
    private readonly Color32[] _output;
    private readonly Color32[] _upload;
    private readonly float[] _wet = new float[N];
    private readonly float[] _foam = new float[N];
    private readonly float[] _clean = new float[N];
    private readonly float[] _scrub = new float[N];
    private readonly float[] _dry = new float[N];
    private readonly float[] _dirt = new float[N];
    private readonly float[] _bubbles = new float[N];
    private readonly float[] _shine = new float[N];
    private readonly float[] _shade = new float[N];
    private readonly float[] _silhouette = new float[N];
    private readonly byte[] _zone = new byte[N];
    private readonly float[] _zoneTotal = new float[ZoneCount];
    private readonly float[][] _zoneSums =
    {
        new float[ZoneCount], new float[ZoneCount], new float[ZoneCount],
        new float[ZoneCount], new float[ZoneCount]
    };
    private readonly Color32 _dirtColor;

    private float _total;
    private float _wetSum;
    private float _foamSum;
    private float _cleanSum;
    private float _scrubSum;
    private float _drySum;
    private bool _dirty;
    private int _dirtyMinX;
    private int _dirtyMinY;
    private int _dirtyMaxX;
    private int _dirtyMaxY;

    public Texture2D Texture { get { return _texture; } }

    public WashSurface(Color32[] carPixels, int carW, int carH, float dirtAmount, int seed,
        DirtKind dirtType = DirtKind.Mud)
    {
        _output = new Color32[N];
        _upload = new Color32[N];
        _dirtyMinX = W;
        _dirtyMinY = H;
        _dirtyMaxX = -1;
        _dirtyMaxY = -1;
        _dirtColor = DirtColor(dirtType);
        float dirtScale = dirtType == DirtKind.Snow ? 0.72f : (dirtType == DirtKind.Dust ? 0.86f : 1f);

        var glints = new Vector2[6];
        for (int g = 0; g < glints.Length; g++)
        {
            glints[g] = new Vector2(
                0.12f + Hash(31, g, seed) * 0.76f,
                0.18f + Hash(97, g, seed + 11) * 0.64f);
        }

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                int sx = Mathf.Min(carW - 1, (int)((x + 0.5f) * carW / W));
                int sy = Mathf.Min(carH - 1, (int)((y + 0.5f) * carH / H));
                float sil = carPixels[sy * carW + sx].a / 255f;
                _silhouette[i] = sil;
                _total += sil;

                int column = Mathf.Min(2, x * 3 / W);
                byte zone = (byte)((y > H * 0.5f ? 0 : 3) + column);
                _zone[i] = zone;
                _zoneTotal[zone] += sil;

                float n = Mathf.PerlinNoise(x * 0.045f + seed * 0.17f, y * 0.045f + seed * 0.29f);
                float h = Hash(x, y, seed);
                float upper = y > H * 0.62f ? 0.82f : 1f; // windows are a little clearer, not clean
                float patch = dirtType == DirtKind.Leaves
                    ? (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) > 0.45f ? 1.35f : 0.35f)
                    : 1f;
                _dirt[i] = Mathf.Clamp01((0.72f + 0.30f * n + 0.20f * h) * dirtAmount * dirtScale * upper * patch);
                _bubbles[i] = 0.58f + 0.42f * Mathf.PerlinNoise(x * 0.085f + 7f, y * 0.085f + 13f);
                _shade[i] = h;

                float sparkle = 0.12f + 0.30f * h;
                for (int g = 0; g < glints.Length; g++)
                {
                    float dx = (x / (float)W - glints[g].x) * 1.5f;
                    float dy = y / (float)H - glints[g].y;
                    sparkle += Mathf.Exp(-(dx * dx + dy * dy) * 900f) * 1.5f;
                }
                _shine[i] = Mathf.Clamp01(sparkle);
            }
        }

        _texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "WashOverlay",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    /// <summary>Paint a stroke from UV (0..1) a to b, interpolating intermediate
    /// points so a fast finger swipe never leaves gaps.</summary>
    public void PaintLine(float u0, float v0, float u1, float v1, float radius, WashStage stage)
    {
        float px = (u1 - u0) * W * 1.5f;
        float py = (v1 - v0) * H;
        float distance = Mathf.Sqrt(px * px + py * py);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (radius * H * 0.45f)));
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            Paint(u0 + (u1 - u0) * t, v0 + (v1 - v0) * t, radius, stage);
        }
        Upload();
    }

    public void Paint(float u, float v, float radius, WashStage stage)
    {
        if (stage == WashStage.Done) return;

        float cx = u * W;
        float cy = v * H;
        float prY = Mathf.Max(2f, radius * H);
        float prX = prY / 1.5f;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - prX));
        int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + prX));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - prY));
        int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + prY));
        bool changed = false;

        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - cy;
            for (int x = x0; x <= x1; x++)
            {
                int i = y * W + x;
                if (_silhouette[i] < 0.02f) continue;
                float dx = (x + 0.5f - cx) * 1.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= prY) continue;

                float t = 1f - d / prY;
                float falloff = t * t * (3f - 2f * t);
                float amount = falloff * 0.92f;
                changed = true;

                PaintMask(stage, i, amount);

                if (stage == WashStage.Rinse)
                {
                    _foam[i] *= 1f - falloff * 0.95f;
                    _wet[i] *= 1f - falloff * 0.75f;
                }
            }
        }
        if (changed) MarkDirty(x0, y0, x1, y1);
    }

    /// <summary>Finish the current stage so no tiny spot is left behind. Only
    /// zones that are already reasonably covered are completed, so a child
    /// still has to visit the whole car before the stage can advance.</summary>
    public void FillCurrent(WashStage stage)
    {
        if (stage == WashStage.Done) return;
        float[] target = Target(stage);
        float[] zoneSum = ZoneSums(stage);

        for (int i = 0; i < N; i++)
        {
            if (_silhouette[i] < 0.02f) continue;
            byte z = _zone[i];
            if (_zoneTotal[z] <= 0f || zoneSum[z] / _zoneTotal[z] >= ZoneFinish) continue;
            target[i] = 1f;
        }

        Recount(stage);
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    /// <summary>Final celebration state: clean, dry and shiny with no dirt left.</summary>
    public void FinishAll()
    {
        for (int i = 0; i < N; i++)
        {
            if (_silhouette[i] < 0.02f) continue;
            _dirt[i] = 0f;
            _wet[i] = 0f;
            _foam[i] = 0f;
            _clean[i] = 1f;
            _scrub[i] = 1f;
            _dry[i] = 1f;
        }
        for (int s = 0; s < (int)WashStage.Done; s++) Recount((WashStage)s);
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    public float Progress(WashStage stage)
    {
        if (stage == WashStage.Done) return 1f;
        return Mathf.Clamp01(Sum(stage) / Mathf.Max(1f, _total));
    }

    /// <summary>Coverage of one logical zone, used for the small completion ticks.</summary>
    public float ZoneProgress(WashStage stage, int zone)
    {
        if (stage == WashStage.Done) return 1f;
        if (zone < 0 || zone >= ZoneCount || _zoneTotal[zone] <= 0f) return 1f;
        return Mathf.Clamp01(ZoneSums(stage)[zone] / _zoneTotal[zone]);
    }

    /// <summary>Number of overlay pixels with meaningful opacity. Used by the
    /// in-Editor verification to prove that dirt is actually visible.</summary>
    public int VisiblePixelCount()
    {
        int count = 0;
        for (int i = 0; i < N; i++) if (_output[i].a > 96) count++;
        return count;
    }

    public bool HasZone(int zone)
    {
        return zone >= 0 && zone < ZoneCount && _zoneTotal[zone] > 0f;
    }

    /// <summary>The weakest zone decides whether the stage is finished.</summary>
    public float MinZoneProgress(WashStage stage)
    {
        if (stage == WashStage.Done) return 1f;
        float weakest = 1f;
        for (int z = 0; z < ZoneCount; z++)
        {
            if (_zoneTotal[z] <= 0f) continue;
            weakest = Mathf.Min(weakest, ZoneProgress(stage, z));
        }
        return weakest;
    }

    private void PaintMask(WashStage stage, int i, float amount)
    {
        float sil = _silhouette[i];
        int z = _zone[i];
        float added;
        switch (stage)
        {
            case WashStage.Wet:
                added = Add(_wet, i, amount, sil);
                _wetSum += added;
                _zoneSums[0][z] += added;
                break;
            case WashStage.Foam:
                added = Add(_foam, i, amount, sil);
                _foamSum += added;
                _zoneSums[1][z] += added;
                break;
            case WashStage.Rinse:
                added = Add(_clean, i, amount, sil);
                _cleanSum += added;
                _zoneSums[2][z] += added;
                break;
            case WashStage.Scrub:
                added = Add(_scrub, i, amount, sil);
                _scrubSum += added;
                _zoneSums[3][z] += added;
                break;
            case WashStage.Dry:
                added = Add(_dry, i, amount, sil);
                _drySum += added;
                _zoneSums[4][z] += added;
                break;
            default:
                return;
        }
    }

    private float[] ZoneSums(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return _zoneSums[0];
            case WashStage.Foam: return _zoneSums[1];
            case WashStage.Rinse: return _zoneSums[2];
            case WashStage.Scrub: return _zoneSums[3];
            case WashStage.Dry: return _zoneSums[4];
            default: return _zoneSums[0];
        }
    }

    private void Recount(WashStage stage)
    {
        float[] mask = Target(stage);
        float[] zones = ZoneSums(stage);
        Array.Clear(zones, 0, zones.Length);
        float total = 0f;
        for (int i = 0; i < N; i++)
        {
            if (_silhouette[i] < 0.02f) continue;
            float value = mask[i] * _silhouette[i];
            total += value;
            zones[_zone[i]] += value;
        }
        SetSum(stage, total);
    }

    public void Upload()
    {
        if (!_dirty) return;

        int w = _dirtyMaxX - _dirtyMinX + 1;
        int h = _dirtyMaxY - _dirtyMinY + 1;
        Rebuild(_dirtyMinX, _dirtyMinY, _dirtyMaxX, _dirtyMaxY);

        for (int y = 0; y < h; y++)
        {
            Array.Copy(_output, (_dirtyMinY + y) * W + _dirtyMinX,
                _upload, y * w, w);
        }
        _texture.SetPixels32(_dirtyMinX, _dirtyMinY, w, h, _upload, 0);
        _texture.Apply(false, false);

        _dirty = false;
        _dirtyMinX = W;
        _dirtyMinY = H;
        _dirtyMaxX = -1;
        _dirtyMaxY = -1;
    }

    private void MarkDirty(int x0, int y0, int x1, int y1)
    {
        if (x1 < x0 || y1 < y0) return;
        _dirty = true;
        if (x0 < _dirtyMinX) _dirtyMinX = x0;
        if (y0 < _dirtyMinY) _dirtyMinY = y0;
        if (x1 > _dirtyMaxX) _dirtyMaxX = x1;
        if (y1 > _dirtyMaxY) _dirtyMaxY = y1;
    }

    private float[] Target(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return _wet;
            case WashStage.Foam: return _foam;
            case WashStage.Rinse: return _clean;
            case WashStage.Scrub: return _scrub;
            case WashStage.Dry: return _dry;
            default: return _clean;
        }
    }

    private float Sum(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return _wetSum;
            case WashStage.Foam: return _foamSum;
            case WashStage.Rinse: return _cleanSum;
            case WashStage.Scrub: return _scrubSum;
            case WashStage.Dry: return _drySum;
            default: return _cleanSum;
        }
    }

    private void SetSum(WashStage stage, float value)
    {
        switch (stage)
        {
            case WashStage.Wet: _wetSum = value; break;
            case WashStage.Foam: _foamSum = value; break;
            case WashStage.Rinse: _cleanSum = value; break;
            case WashStage.Scrub: _scrubSum = value; break;
            case WashStage.Dry: _drySum = value; break;
        }
    }

    private static float Add(float[] array, int i, float amount, float silhouette)
    {
        float before = array[i];
        float after = Mathf.Min(1f, before + amount);
        array[i] = after;
        return (after - before) * silhouette;
    }

    private void Rebuild(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int i = y * W + x;
                float sil = _silhouette[i];
                if (sil < 0.004f)
                {
                    _output[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float wet = _wet[i] * (1f - _dry[i]);
                float foam = _foam[i];
                float clean = _clean[i];
                float scrub = _scrub[i];
                float shade = _shade[i];

                float r = 0f, g = 0f, b = 0f, a = 0f;
                // Layer order: wet shine -> dirt -> foam -> final sparkle.
                Blend(ref r, ref g, ref b, ref a, 30f, 78f, 116f, wet * 0.42f * (1f - clean));

                Blend(ref r, ref g, ref b, ref a,
                    _dirtColor.r + shade * 30f, _dirtColor.g + shade * 24f, _dirtColor.b + shade * 18f,
                    _dirt[i] * (1f - 0.38f * wet) * (1f - clean) * (1f - 0.70f * scrub));

                float bub = _bubbles[i];
                Blend(ref r, ref g, ref b, ref a,
                    226f + 29f * bub, 236f + 19f * bub, 244f + 11f * bub,
                    foam * bub * (1f - clean));

                float shine = Mathf.Clamp01(_shine[i] + _dry[i] * 0.18f);
                Blend(ref r, ref g, ref b, ref a, 255f, 255f, 238f, clean * shine * 0.85f);

                a *= sil;
                // Mathf.Clamp has float and int overloads. Without 255f the
                // float overload is selected and the byte cast truncates every
                // 0..1 channel to zero, leaving the wash overlay invisible.
                _output[i] = new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f),
                    (byte)Mathf.Clamp(b, 0f, 255f), (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }
    }

    private static Color32 DirtColor(DirtKind kind)
    {
        switch (kind)
        {
            case DirtKind.Dust: return new Color32(176, 158, 128, 255);
            case DirtKind.Sand: return new Color32(222, 190, 112, 255);
            case DirtKind.Puddle: return new Color32(84, 106, 132, 255);
            case DirtKind.Leaves: return new Color32(122, 126, 62, 255);
            case DirtKind.Snow: return new Color32(214, 232, 240, 255);
            default: return new Color32(104, 76, 48, 255);
        }
    }

    private static void Blend(ref float r, ref float g, ref float b, ref float a,
        float sr, float sg, float sb, float sa)
    {
        sa = Mathf.Clamp01(sa);
        if (sa <= 0.001f) return;
        float outA = sa + a * (1f - sa);
        float inv = a * (1f - sa);
        r = (sr * sa + r * inv) / outA;
        g = (sg * sa + g * inv) / outA;
        b = (sb * sa + b * inv) / outA;
        a = outA;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xffff) / 65535f;
        }
    }
}
