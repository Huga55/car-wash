using UnityEngine;

public enum WashStage { Wet, Foam, Rinse, Done }

/// <summary>
/// The "paintable" car surface. Keeps three small masks (wet, foam, clean) and
/// composites them into ONE 256x256 texture displayed over the car art.
/// Only 65k pixels are touched, so this is cheap even on weak tablets.
/// </summary>
public sealed class WashSurface
{
    public const int W = 256;
    public const int H = 256;
    private const int N = W * H;

    private readonly Texture2D _texture;
    private readonly Color32[] _output;
    private readonly float[] _wet = new float[N];
    private readonly float[] _foam = new float[N];
    private readonly float[] _clean = new float[N];
    private readonly float[] _dirt = new float[N];
    private readonly float[] _bubbles = new float[N];
    private readonly float[] _shine = new float[N];
    private readonly float[] _shade = new float[N];
    private readonly float[] _silhouette = new float[N];

    private float _total;
    private float _wetSum;
    private float _foamSum;
    private float _cleanSum;
    private bool _needsUpload;

    public Texture2D Texture { get { return _texture; } }

    public WashSurface(Color32[] carPixels, int carW, int carH, float dirtAmount, int seed)
    {
        _output = new Color32[N];

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

                float n = Mathf.PerlinNoise(x * 0.045f + seed * 0.17f, y * 0.045f + seed * 0.29f);
                float h = Hash(x, y, seed);
                float upper = y > H * 0.62f ? 0.55f : 1f; // leave the windows a bit cleaner
                _dirt[i] = Mathf.Clamp01((0.60f + 0.28f * n + 0.22f * h) * dirtAmount * upper);
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
        Rebuild();
        _needsUpload = true;
        Upload();
    }

    /// <summary>Paint a stroke from UV (0..1) a to b. Radius is a fraction of the car height.</summary>
    public void PaintLine(float u0, float v0, float u1, float v1, float radius, WashStage stage)
    {
        // The overlay is square but displayed in a 3:2 box: correct x by 1.5 so
        // the brush stays round on screen.
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
        float cx = u * W;
        float cy = v * H;
        float prY = Mathf.Max(2f, radius * H);
        float prX = prY / 1.5f;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - prX));
        int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + prX));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - prY));
        int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + prY));

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

                switch (stage)
                {
                    case WashStage.Wet:
                        _wetSum += Add(_wet, i, amount, _silhouette[i]);
                        break;
                    case WashStage.Foam:
                        _foamSum += Add(_foam, i, amount, _silhouette[i]);
                        break;
                    case WashStage.Rinse:
                        _cleanSum += Add(_clean, i, amount, _silhouette[i]);
                        _foam[i] *= 1f - falloff * 0.95f;
                        _wet[i] *= 1f - falloff * 0.75f;
                        break;
                }
            }
        }
        if (stage != WashStage.Done) _needsUpload = true;
    }

    /// <summary>Finish the current stage so no tiny spot is left behind (no failure for kids).</summary>
    public void FillCurrent(WashStage stage)
    {
        if (stage == WashStage.Done) return;
        float[] target = stage == WashStage.Wet ? _wet : (stage == WashStage.Foam ? _foam : _clean);
        float sum = 0f;
        for (int i = 0; i < N; i++)
        {
            if (_silhouette[i] < 0.02f) continue;
            target[i] = 1f;
            sum += _silhouette[i];
        }
        if (stage == WashStage.Wet) _wetSum = sum;
        else if (stage == WashStage.Foam) _foamSum = sum;
        else _cleanSum = sum;
        _needsUpload = true;
        Upload();
    }

    public float Progress(WashStage stage)
    {
        float sum = stage == WashStage.Wet ? _wetSum : (stage == WashStage.Foam ? _foamSum : _cleanSum);
        return Mathf.Clamp01(sum / Mathf.Max(1f, _total));
    }

    public void Upload()
    {
        if (!_needsUpload) return;
        Rebuild();
        _texture.SetPixels32(_output);
        _texture.Apply(false, false);
        _needsUpload = false;
    }

    private static float Add(float[] array, int i, float amount, float silhouette)
    {
        float before = array[i];
        float after = Mathf.Min(1f, before + amount);
        array[i] = after;
        return (after - before) * silhouette;
    }

    private void Rebuild()
    {
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float sil = _silhouette[i];
                if (sil < 0.004f)
                {
                    _output[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float wet = _wet[i];
                float foam = _foam[i];
                float clean = _clean[i];

                float r = 0f, g = 0f, b = 0f, a = 0f;
                // Layer order: wet shine -> mud -> foam -> final sparkle.
                Blend(ref r, ref g, ref b, ref a, 30f, 48f, 62f, wet * 0.44f * (1f - clean));

                float shade = _shade[i];
                Blend(ref r, ref g, ref b, ref a,
                    84f + shade * 46f, 60f + shade * 28f, 38f + shade * 18f,
                    _dirt[i] * (1f - 0.72f * wet) * (1f - clean));

                float bub = _bubbles[i];
                Blend(ref r, ref g, ref b, ref a,
                    226f + 29f * bub, 236f + 19f * bub, 244f + 11f * bub,
                    foam * bub * (1f - clean));

                Blend(ref r, ref g, ref b, ref a, 255f, 255f, 236f, clean * _shine[i] * 0.85f);

                a *= sil;
                _output[i] = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255),
                    (byte)Mathf.Clamp(b, 0, 255), (byte)Mathf.Clamp(a, 0, 255));
            }
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
