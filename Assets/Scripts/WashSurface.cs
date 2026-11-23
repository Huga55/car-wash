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
    private readonly float[] _runoff = new float[N];
    private readonly float[] _droplets = new float[N];
    private readonly float[] _soap = new float[N];
    private readonly float[] _shine = new float[N];
    private readonly float[] _shade = new float[N];
    // -1 = no sweep; 0..1 = one diagonal polish pass across the dry paint.
    private float _shineSweep = -1f;
    private readonly float[] _silhouette = new float[N];
    private readonly byte[] _zone = new byte[N];

    // Smooth "the child did 82%, the game finishes the last 18%" animation.
    // These arrays are allocated once and reused for every stage transition.
    private readonly int[] _finishIndices = new int[N];
    private readonly float[] _finishFrom = new float[N];
    private int _finishCount;
    private float[] _finishTarget;
    private WashStage _finishStage = WashStage.Done;
    private int _finishMinX, _finishMinY, _finishMaxX, _finishMaxY;
    private Rect _bodyBounds = new Rect(0f, 0f, 0f, 0f);
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

    /// <summary>UV rectangle of the actual paintable panels. Input hit testing
    /// and brush radius use this instead of the whole transparent car sprite,
    /// which is what made buses and trucks feel like they had phantom edges.</summary>
    public Rect BodyBounds { get { return _bodyBounds; } }

    public WashSurface(Color32[] carPixels, int carW, int carH, float dirtAmount, int seed,
        DirtKind dirtType = DirtKind.Mud, byte[] panelMask = null)
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
                int sourceIndex = sy * carW + sx;
                float alpha = carPixels[sourceIndex].a / 255f;
                // panelMask comes from CarArt and contains only body panels.
                // Without it (legacy callers) the alpha channel is the best
                // available approximation of the visible surface.
                float panel = panelMask != null ? panelMask[sourceIndex] / 255f : alpha;
                float sil = alpha * panel;
                _silhouette[i] = sil;
                _total += sil;

                int column = Mathf.Min(2, x * 3 / W);
                byte zone = (byte)((y > H * 0.5f ? 0 : 3) + column);
                _zone[i] = zone;
                _zoneTotal[zone] += sil;

                if (sil > 0.18f)
                {
                    float u = (x + 0.5f) / W;
                    float v = (y + 0.5f) / H;
                    if (_bodyBounds.width <= 0f)
                        _bodyBounds = new Rect(u, v, 0f, 0f);
                    else
                    {
                        float left = Mathf.Min(_bodyBounds.xMin, u);
                        float bottom = Mathf.Min(_bodyBounds.yMin, v);
                        float right = Mathf.Max(_bodyBounds.xMax, u);
                        float top = Mathf.Max(_bodyBounds.yMax, v);
                        _bodyBounds = new Rect(left, bottom, right - left, top - bottom);
                    }
                }

                float n = Mathf.PerlinNoise(x * 0.045f + seed * 0.17f, y * 0.045f + seed * 0.29f);
                float h = Hash(x, y, seed);
                float upper = y > H * 0.62f ? 0.82f : 1f; // windows are a little clearer, not clean
                float patch = dirtType == DirtKind.Leaves
                    ? (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) > 0.45f ? 1.35f : 0.35f)
                    : 1f;
                _dirt[i] = Mathf.Clamp01((0.72f + 0.30f * n + 0.20f * h) * dirtAmount * dirtScale * upper * patch);
                _bubbles[i] = 0.58f + 0.42f * Mathf.PerlinNoise(x * 0.085f + 7f, y * 0.085f + 13f);
                // Elongated vertical noise gives water and foam believable
                // gravity-driven streaks instead of a uniform dark tint.
                _runoff[i] = Mathf.Clamp01(0.22f + 0.95f *
                    Mathf.PerlinNoise(x * 0.085f + seed * 0.13f, y * 0.012f + seed * 0.07f));
                _droplets[i] = Mathf.Clamp01((Hash(x, y, seed + 41) - 0.91f) * 11f) *
                               (y < H * 0.72f ? 1f : 0.35f);
                _soap[i] = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.06f + 19f, y * 0.11f + 3f) - 0.36f) * 2.3f);
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

        if (_bodyBounds.width <= 0f || _bodyBounds.height <= 0f)
            _bodyBounds = new Rect(0.05f, 0.06f, 0.90f, 0.86f);

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
                float sil = _silhouette[i];
                if (sil < 0.02f) continue;
                float dx = (x + 0.5f - cx) * 1.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= prY) continue;

                float t = 1f - d / prY;
                float falloff = t * t * (3f - 2f * t);
                float amount = falloff * 0.92f;
                changed = true;

                PaintMask(stage, i, amount);

                if (stage == WashStage.Scrub)
                {
                    // A sponge pushes foam aside as it lifts dirt. Removing it
                    // from the same foam mask keeps coverage numbers honest and
                    // makes "partially scrubbed" visibly different from foam.
                    float before = _foam[i];
                    float after = before * (1f - falloff * 0.78f);
                    float removed = (before - after) * sil;
                    _foam[i] = after;
                    _foamSum = Mathf.Max(0f, _foamSum - removed);
                    _zoneSums[1][_zone[i]] = Mathf.Max(0f, _zoneSums[1][_zone[i]] - removed);
                }
                else if (stage == WashStage.Rinse)
                {
                    _foam[i] *= 1f - falloff * 0.95f;
                }
            }
        }
        if (changed)
        {
            // Rebuild a few rows below the brush as well: runoff and foam
            // trails sample the rows above them, so those pixels must be
            // refreshed even though the finger never touched them.
            MarkDirty(x0, Mathf.Max(0, y0 - 20), x1, y1);
        }
    }

    /// <summary>Prepares the last few uncovered texels for a smooth finish.
    /// The game calls this only after the child has reached ZoneFinish in
    /// every zone, so the remaining area is small. No texture changes yet.</summary>
    public bool BeginCompletion(WashStage stage)
    {
        if (stage == WashStage.Done) return false;
        float[] target = Target(stage);
        float[] zoneSum = ZoneSums(stage);
        _finishCount = 0;
        _finishStage = stage;
        _finishTarget = target;
        _finishMinX = W;
        _finishMinY = H;
        _finishMaxX = -1;
        _finishMaxY = -1;

        for (int i = 0; i < N; i++)
        {
            float sil = _silhouette[i];
            if (sil < 0.02f || target[i] >= 0.999f) continue;
            byte z = _zone[i];
            if (_zoneTotal[z] <= 0f || zoneSum[z] / _zoneTotal[z] < ZoneFinish) continue;
            int x = i % W;
            int y = i / W;
            _finishIndices[_finishCount] = i;
            _finishFrom[_finishCount] = target[i];
            _finishCount++;
            if (x < _finishMinX) _finishMinX = x;
            if (y < _finishMinY) _finishMinY = y;
            if (x > _finishMaxX) _finishMaxX = x;
            if (y > _finishMaxY) _finishMaxY = y;
        }
        return _finishCount > 0;
    }

    /// <summary>Advances the prepared completion animation. Coverages and zone
    /// sums are updated from the same values that are uploaded to the texture,
    /// so the UI progress bar and the visible surface can never disagree.</summary>
    public float AdvanceCompletion(float t)
    {
        if (_finishCount == 0 || _finishTarget == null) return 1f;
        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        float sum = Sum(_finishStage);
        float[] zones = ZoneSums(_finishStage);
        for (int k = 0; k < _finishCount; k++)
        {
            int i = _finishIndices[k];
            float before = _finishTarget[i];
            float after = Mathf.Lerp(_finishFrom[k], 1f, eased);
            _finishTarget[i] = after;
            float delta = (after - before) * _silhouette[i];
            sum += delta;
            zones[_zone[i]] += delta;
        }
        SetSum(_finishStage, sum);
        if (_finishMinX <= _finishMaxX && _finishMinY <= _finishMaxY)
            MarkDirty(_finishMinX, Mathf.Max(0, _finishMinY - 20), _finishMaxX, _finishMaxY);
        Upload();

        if (t >= 1f)
        {
            _finishCount = 0;
            _finishTarget = null;
            _finishStage = WashStage.Done;
        }
        return eased;
    }

    /// <summary>One slow polishing pass after the last droplet disappears. It is
    /// a narrow moving highlight, not a whole-surface flash, so the dried paint
    /// stays polished instead of turning white.</summary>
    public void BeginFinalShine()
    {
        _shineSweep = 0f;
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    public void AdvanceFinalShine(float t)
    {
        if (_shineSweep < 0f) return;
        _shineSweep = Mathf.Clamp01(t);
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    public void EndFinalShine()
    {
        if (_shineSweep < 0f) return;
        _shineSweep = -1f;
        MarkDirty(0, 0, W - 1, H - 1);
        Upload();
    }

    /// <summary>Instant variant kept for editor checks and tests. Gameplay uses
    /// BeginCompletion + AdvanceCompletion so the change is always visible.</summary>
    public void FillCurrent(WashStage stage)
    {
        if (BeginCompletion(stage)) AdvanceCompletion(1f);
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

    /// <summary>True only when the finger is over a real body panel. A
    /// one-texel neighbourhood keeps edge strokes from falling through when
    /// the car image bobs or rotates by a fraction of a degree.</summary>
    public bool IsPaintable(float u, float v)
    {
        int cx = Mathf.Clamp(Mathf.FloorToInt(u * W), 0, W - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(v * H), 0, H - 1);
        for (int oy = -1; oy <= 1; oy++)
        {
            int y = cy + oy;
            if (y < 0 || y >= H) continue;
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox;
                if (x < 0 || x >= W) continue;
                if (_silhouette[y * W + x] > 0.06f) return true;
            }
        }
        return false;
    }

    /// <summary>Reads the composited overlay pixel; used by visual tests to
    /// compare states by colour instead of trusting internal flags.</summary>
    public Color32 OverlayPixel(int x, int y)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return new Color32(0, 0, 0, 0);
        return _output[y * W + x];
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
                float bub = _bubbles[i];

                // Water already applied higher on the body leaves a thinner,
                // noisy trail below the brush. Sampling a few rows up is far
                // cheaper than a fluid sim and reads as runoff on vertical panels.
                float trail = 0f;
                if (y >= 8) trail += _wet[(y - 8) * W + x] * 0.34f;
                if (y >= 19) trail += _wet[(y - 19) * W + x] * 0.17f;
                // Rinsing removes foam, not water. A cleaned panel keeps a thin
                // clear film until the dryer arrives, so "clean wet" and
                // "clean dry" are visibly different states.
                float rinseFilm = clean * (1f - _dry[i]) * 0.46f;
                float wetLayer = Mathf.Clamp01(wet + trail * _runoff[i] + rinseFilm) *
                                 (1f - _dry[i]);

                float r = 0f, g = 0f, b = 0f, a = 0f;
                // Wet: a dark, saturated film with a cool moving reflection.
                // This is much stronger than a faint tint so a child can see
                // instantly where the hose has already been.
                Blend(ref r, ref g, ref b, ref a, 20f, 54f, 88f, wetLayer * 0.68f);
                Blend(ref r, ref g, ref b, ref a, 104f, 172f, 212f,
                    wetLayer * _runoff[i] * 0.36f);

                // Dry dirt is matte and uneven; wet dirt darkens and loses
                // contrast instead of disappearing behind the water film.
                float dirtAlpha = _dirt[i] * (1f - clean) * (1f - 0.72f * scrub) *
                                  (1f - 0.50f * wetLayer);
                Blend(ref r, ref g, ref b, ref a,
                    _dirtColor.r + shade * 26f, _dirtColor.g + shade * 21f, _dirtColor.b + shade * 16f,
                    dirtAlpha * 0.96f);

                // Droplets, small streaks and a narrow moving glint sit on top
                // of the dirt, so wet areas read as glossy even in a small HUD.
                Blend(ref r, ref g, ref b, ref a, 196f, 232f, 252f,
                    wetLayer * _droplets[i] * 0.94f);
                Blend(ref r, ref g, ref b, ref a, 238f, 250f, 255f,
                    wetLayer * _shine[i] * _runoff[i] * 0.44f);

                float foamTrail = 0f;
                if (y >= 6) foamTrail += _foam[(y - 6) * W + x] * 0.22f;
                float foamLayer = Mathf.Clamp01(foam + foamTrail * _runoff[i]) * bub * (1f - clean);
                Blend(ref r, ref g, ref b, ref a,
                    230f + 25f * bub, 238f + 17f * bub, 246f + 9f * bub, foamLayer);
                // Brighter bubble caps break up the flat white foam layer.
                Blend(ref r, ref g, ref b, ref a, 255f, 255f, 255f,
                    foamLayer * Mathf.Clamp01((bub - 0.72f) * 3.6f) * 0.62f);

                float soap = scrub * foam * _soap[i] * (1f - clean);
                Blend(ref r, ref g, ref b, ref a, 232f, 242f, 238f, soap * 0.48f);

                // Drying replaces the wet film with a restrained polished
                // lacquer: a cool reflection plus a few warm anisotropic
                // highlights. It never floods the whole panel with white.
                float dry = _dry[i] * clean;
                float band = 1f - Mathf.Abs(Mathf.Sin(
                    (x * 0.046f + y * 0.022f + shade * 1.7f) * Mathf.PI)) * 3.2f;
                float polish = dry * Mathf.Clamp01(0.30f + _shine[i] * 0.85f +
                                                   Mathf.Clamp01(band) * 0.55f);
                Blend(ref r, ref g, ref b, ref a, 178f, 218f, 248f, dry * _runoff[i] * 0.20f);
                Blend(ref r, ref g, ref b, ref a, 255f, 252f, 232f, polish * 0.56f);

                if (_shineSweep >= 0f)
                {
                    float sweepPos = Mathf.Lerp(-0.25f, 1.25f, _shineSweep);
                    float diagonal = (x / (float)W) * 0.72f + (y / (float)H) * 0.28f;
                    float offset = (diagonal - sweepPos) * 9f;
                    float sweepBand = Mathf.Exp(-offset * offset) * dry;
                    Blend(ref r, ref g, ref b, ref a, 255f, 250f, 226f, sweepBand * 0.48f);
                }

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
