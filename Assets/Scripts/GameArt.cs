using UnityEngine;

/// <summary>Bright, simple backgrounds baked into one tiny texture per theme.</summary>
public static class GameArt
{
    private const int Size = 384;

    public static Texture2D Background(int theme)
    {
        int t = Mathf.Abs(theme) % 5;
        var pixels = new Color32[Size * Size];
        var steel = new Color32((byte)(74 + t * 4), (byte)(92 + t * 3), (byte)(110 + t * 3), 255);
        var steelDark = new Color32(46, 60, 76, 255);
        var wallTop = new Color32((byte)(150 + t * 5), (byte)(172 + t * 4), (byte)(188 + t * 2), 255);
        var wallBottom = new Color32(108, 128, 146, 255);
        var floorTop = new Color32(106, 120, 134, 255);
        var floorBottom = new Color32(66, 78, 92, 255);
        var grout = new Color32(88, 104, 120, 255);
        var light = new Color32(244, 252, 255, 255);

        for (int y = 0; y < Size; y++)
        {
            float v = y / (float)Size;
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                Color c;
                if (v > 0.88f)
                {
                    // Canopy underside with beams.
                    c = steelDark;
                    if ((x / 96) % 2 == 0) c = Color.Lerp(c, steel, 0.35f);
                    if (v > 0.965f) c = Color.Lerp(c, light, 0.16f);
                }
                else if (v > 0.42f)
                {
                    // Tiled back wall with a soft top-down light falloff.
                    float k = Mathf.Clamp01((v - 0.42f) / 0.46f);
                    c = Color.Lerp(wallBottom, wallTop, k);
                    if (x % 48 < 2 || ((y + 12) % 40) < 2) c = Color.Lerp(c, grout, 0.55f);
                    // Light strip reflected across the upper wall.
                    if (v > 0.80f && v < 0.845f && u > 0.14f && u < 0.86f)
                        c = Color.Lerp(c, light, 0.55f);
                }
                else
                {
                    // Sloped wet floor converging toward the back wall.
                    float k = Mathf.Clamp01(v / 0.42f);
                    c = Color.Lerp(floorBottom, floorTop, k);
                    float perspective = 1f / Mathf.Max(0.25f, v + 0.22f);
                    if (((x - Size * 0.5f) * perspective + Size * 0.5f) % 64f < 2.5f)
                        c = Color.Lerp(c, grout, 0.45f);
                    if (y % 26 < 2) c = Color.Lerp(c, grout, 0.35f);
                }

                // Side pillars frame the wash bay.
                if (u < 0.045f || u > 0.955f) c = steelDark;
                pixels[y * Size + x] = c;
            }
        }

        // Ceiling strip lights.
        for (int lamp = 0; lamp < 3; lamp++)
            FillRect(pixels, 52 + lamp * 130, 356, 92, 9, light);

        // Wall equipment: foam unit, hanging hose reels and a tool rack.
        FillRect(pixels, 18, 196, 62, 118, steel);
        FillRect(pixels, 24, 250, 50, 52, new Color32(206, 226, 236, 255));
        FillRect(pixels, 30, 300, 38, 12, steelDark);
        FillRect(pixels, 304, 206, 58, 92, steel);
        for (int i = 0; i < 3; i++) Disc(pixels, 318 + i * 18, 274, 6f, steelDark);
        FillRect(pixels, 106, 292, 150, 9, steelDark);
        for (int i = 0; i < 4; i++) FillRect(pixels, 114 + i * 36, 270, 9, 24, new Color32(196, 210, 222, 255));

        // Drain channel and wet reflections on the floor.
        FillRect(pixels, 0, 16, Size, 8, new Color32(38, 50, 62, 255));
        for (int i = 0; i < 11; i++) FillRect(pixels, 12 + i * 35, 16, 18, 4, new Color32(96, 112, 128, 255));
        for (int i = 0; i < 5; i++)
            Ellipse(pixels, 46 + i * 76, 44 + (i % 2) * 20, 30f + i * 3, 8f,
                new Color32(150, 190, 210, 90));
        for (int i = 0; i < 3; i++)
            FillRect(pixels, 70 + i * 120, 54, 46, 3, new Color32(214, 238, 250, 110));

        return CarArt.MakeTexture(pixels, Size, Size, "WashBay" + t);
    }

    private static void FillRect(Color32[] px, int x0, int y0, int w, int h, Color32 color)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < Size && y < Size) px[y * Size + x] = color;
    }

    private static void Disc(Color32[] px, float cx, float cy, float r, Color32 color)
    {
        for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) px[y * Size + x] = color;
            }
    }

    private static void Ellipse(Color32[] px, float cx, float cy, float rx, float ry, Color32 color)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) px[y * Size + x] = color;
            }
    }
}
