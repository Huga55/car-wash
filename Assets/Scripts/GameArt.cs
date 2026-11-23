using UnityEngine;

/// <summary>Bright, simple backgrounds baked into one tiny texture per theme.</summary>
public static class GameArt
{
    private const int Size = 384;

    private static readonly Color32[] SkyTop =
    {
        new Color32(126, 202, 255, 255), new Color32(112, 180, 240, 255),
        new Color32(118, 205, 248, 255), new Color32(255, 168, 148, 255),
        new Color32(140, 214, 236, 255)
    };
    private static readonly Color32[] SkyBottom =
    {
        new Color32(226, 246, 255, 255), new Color32(216, 236, 250, 255),
        new Color32(255, 242, 208, 255), new Color32(255, 226, 182, 255),
        new Color32(236, 250, 236, 255)
    };
    private static readonly Color32[] HillA =
    {
        new Color32(126, 205, 126, 255), new Color32(156, 176, 196, 255),
        new Color32(244, 220, 152, 255), new Color32(184, 142, 164, 255),
        new Color32(146, 212, 150, 255)
    };
    private static readonly Color32[] HillB =
    {
        new Color32(86, 170, 104, 255), new Color32(112, 138, 164, 255),
        new Color32(228, 198, 130, 255), new Color32(144, 108, 142, 255),
        new Color32(96, 176, 112, 255)
    };
    private static readonly Color32[] Road =
    {
        new Color32(126, 132, 144, 255), new Color32(112, 118, 132, 255),
        new Color32(204, 184, 146, 255), new Color32(130, 122, 134, 255),
        new Color32(134, 140, 150, 255)
    };

    public static Texture2D Background(int theme)
    {
        int t = Mathf.Abs(theme) % SkyTop.Length;
        Color top = SkyTop[t];
        Color bottom = SkyBottom[t];
        Color hillA = HillA[t];
        Color hillB = HillB[t];
        Color road = Road[t];
        Color sun = new Color32(255, 244, 176, 255);
        Color cloud = new Color32(255, 255, 255, 255);

        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            float v = y / (float)Size;
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                Color c = Color.Lerp(bottom, top, v);

                float sunDist = Mathf.Sqrt((u - 0.79f) * (u - 0.79f) + (v - 0.81f) * (v - 0.81f));
                c = Color.Lerp(c, sun, Mathf.Clamp01((0.22f - sunDist) * 2.2f) * 0.65f);
                if (sunDist < 0.058f) c = sun;

                c = Color.Lerp(c, cloud, Cloud(u, v, 0.14f, 0.72f, 0.075f));
                c = Color.Lerp(c, cloud, Cloud(u, v, 0.43f, 0.84f, 0.055f));
                c = Color.Lerp(c, cloud, Cloud(u, v, 0.62f, 0.69f, 0.065f));

                float h1 = 0.19f + 0.050f * Mathf.Sin(u * 7f + t);
                float h2 = 0.12f + 0.035f * Mathf.Sin(u * 10f + 2f + t);
                if (v < h1) c = Color.Lerp(c, hillA, 0.96f);
                if (v < h2) c = Color.Lerp(c, hillB, 0.98f);
                if (v < 0.065f)
                {
                    c = road;
                    if (v > 0.030f && v < 0.038f && (x / 46) % 2 == 0) c = Color.Lerp(road, Color.white, 0.7f);
                }
                pixels[y * Size + x] = c;
            }
        }
        return CarArt.MakeTexture(pixels, Size, Size, "Background" + t);
    }

    private static float Cloud(float u, float v, float cx, float cy, float r)
    {
        float a = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        float b = Mathf.Sqrt((u - cx - r * 0.85f) * (u - cx - r * 0.85f) + (v - cy - r * 0.18f) * (v - cy - r * 0.18f));
        float d = Mathf.Min(a, b);
        return Mathf.Clamp01((r - d) * 22f);
    }
}
