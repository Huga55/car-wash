using UnityEngine;

/// <summary>Cartoon drivers drawn from simple shapes. Ten friendly animals,
/// zero image files, one tiny 160x160 texture created per reward.</summary>
public static class AnimalArt
{
    private static readonly Color32 Dark = new Color32(48, 52, 66, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Pink = new Color32(255, 154, 172, 255);
    private static readonly Color32 Mouth = new Color32(128, 62, 72, 255);

    public static Color32[] Render(AnimalKind kind, int width, int height)
    {
        var p = new Painter(width, height, Fur(kind), Belly(kind));
        p.Ears(kind);

        // Body first, then head so the face always stays in front.
        p.Ellipse(0.50f, 0.20f, 0.25f, 0.20f, p.Belly);
        p.Ellipse(0.50f, 0.52f, 0.28f, 0.29f, p.Fur);

        p.Face(kind);
        return p.Pixels;
    }

    public static Texture2D Texture(AnimalKind kind)
    {
        const int size = 160;
        return CarArt.MakeTexture(Render(kind, size, size), size, size, "Driver" + kind);
    }

    private static Color32 Fur(AnimalKind kind)
    {
        switch (kind)
        {
            case AnimalKind.Dog: return new Color32(206, 158, 104, 255);
            case AnimalKind.Bear: return new Color32(154, 110, 76, 255);
            case AnimalKind.Hare: return new Color32(232, 226, 214, 255);
            case AnimalKind.Fox: return new Color32(232, 126, 62, 255);
            case AnimalKind.Panda: return new Color32(250, 250, 246, 255);
            case AnimalKind.Raccoon: return new Color32(142, 150, 162, 255);
            case AnimalKind.Frog: return new Color32(112, 196, 106, 255);
            case AnimalKind.Lion: return new Color32(238, 178, 72, 255);
            case AnimalKind.Elephant: return new Color32(158, 172, 188, 255);
            default: return new Color32(238, 190, 120, 255);
        }
    }

    private static Color32 Belly(AnimalKind kind)
    {
        switch (kind)
        {
            case AnimalKind.Panda:
            case AnimalKind.Raccoon: return new Color32(76, 82, 92, 255);
            case AnimalKind.Frog: return new Color32(192, 232, 172, 255);
            case AnimalKind.Elephant: return new Color32(182, 194, 206, 255);
            default: return Fur(kind);
        }
    }

    private sealed class Painter
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Pixels;
        public readonly Color32 Fur;
        public readonly Color32 Belly;

        public Painter(int w, int h, Color32 fur, Color32 belly)
        {
            W = w;
            H = h;
            Fur = fur;
            Belly = belly;
            Pixels = new Color32[w * h];
        }

        private void Plot(int x, int y, Color32 color, float alpha = 1f)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0f) return;
            int i = y * W + x;
            Color32 d = Pixels[i];
            float da = d.a / 255f;
            float sa = Mathf.Clamp01(alpha) * color.a / 255f;
            float outA = sa + da * (1f - sa);
            if (outA <= 0f) return;
            float inv = da * (1f - sa);
            Pixels[i] = new Color32(
                (byte)((color.r * sa + d.r * inv) / outA),
                (byte)((color.g * sa + d.g * inv) / outA),
                (byte)((color.b * sa + d.b * inv) / outA),
                (byte)(outA * 255f));
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - rx) * W));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt((cx + rx) * W));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((cy - ry) * H));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt((cy + ry) * H));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f) / W - cx;
                    float dy = (y + 0.5f) / H - cy;
                    float d = dx * dx / (rx * rx) + dy * dy / (ry * ry);
                    if (d <= 1f) Plot(x, y, color, Mathf.Clamp01((1.02f - d) * 40f));
                }
            }
        }

        private void Circle(float cx, float cy, float r, Color32 color)
        {
            Ellipse(cx, cy, r, r, color);
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * W));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * W));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * H));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * H));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = (x + 0.5f) / W;
                    float py = (y + 0.5f) / H;
                    if (Inside(px, py, a, b, c)) Plot(x, y, color);
                }
            }
        }

        private static bool Inside(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (x - b.x) * (a.y - b.y) - (a.x - b.x) * (y - b.y);
            float d2 = (x - c.x) * (b.y - c.y) - (b.x - c.x) * (y - c.y);
            float d3 = (x - a.x) * (c.y - a.y) - (c.x - a.x) * (y - a.y);
            return (d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0);
        }

        public void Ears(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Cat:
                case AnimalKind.Fox:
                    Triangle(new Vector2(0.26f, 0.68f), new Vector2(0.30f, 0.95f), new Vector2(0.43f, 0.74f), Fur);
                    Triangle(new Vector2(0.74f, 0.68f), new Vector2(0.70f, 0.95f), new Vector2(0.57f, 0.74f), Fur);
                    if (kind == AnimalKind.Cat)
                    {
                        Triangle(new Vector2(0.29f, 0.71f), new Vector2(0.315f, 0.90f), new Vector2(0.39f, 0.75f), Pink);
                        Triangle(new Vector2(0.71f, 0.71f), new Vector2(0.685f, 0.90f), new Vector2(0.61f, 0.75f), Pink);
                    }
                    break;
                case AnimalKind.Bear:
                case AnimalKind.Panda:
                    Circle(0.26f, 0.76f, 0.10f, Fur);
                    Circle(0.74f, 0.76f, 0.10f, Fur);
                    Circle(0.26f, 0.76f, 0.055f, kind == AnimalKind.Panda ? Dark : Pink);
                    Circle(0.74f, 0.76f, 0.055f, kind == AnimalKind.Panda ? Dark : Pink);
                    break;
                case AnimalKind.Hare:
                    Ellipse(0.38f, 0.86f, 0.075f, 0.19f, Fur);
                    Ellipse(0.62f, 0.86f, 0.075f, 0.19f, Fur);
                    Ellipse(0.38f, 0.86f, 0.035f, 0.14f, Pink);
                    Ellipse(0.62f, 0.86f, 0.035f, 0.14f, Pink);
                    break;
                case AnimalKind.Dog:
                    Ellipse(0.22f, 0.58f, 0.10f, 0.18f, Fur);
                    Ellipse(0.78f, 0.58f, 0.10f, 0.18f, Fur);
                    break;
                case AnimalKind.Elephant:
                    Ellipse(0.18f, 0.55f, 0.15f, 0.20f, new Color32(138, 152, 170, 255));
                    Ellipse(0.82f, 0.55f, 0.15f, 0.20f, new Color32(138, 152, 170, 255));
                    break;
                case AnimalKind.Frog:
                    Circle(0.32f, 0.75f, 0.115f, Fur);
                    Circle(0.68f, 0.75f, 0.115f, Fur);
                    break;
                case AnimalKind.Lion:
                    Circle(0.50f, 0.52f, 0.37f, new Color32(190, 112, 46, 255));
                    break;
                case AnimalKind.Raccoon:
                    Triangle(new Vector2(0.25f, 0.68f), new Vector2(0.31f, 0.91f), new Vector2(0.43f, 0.72f), Fur);
                    Triangle(new Vector2(0.75f, 0.68f), new Vector2(0.69f, 0.91f), new Vector2(0.57f, 0.72f), Fur);
                    break;
            }
        }

        public void Face(AnimalKind kind)
        {
            if (kind == AnimalKind.Panda || kind == AnimalKind.Raccoon)
            {
                Ellipse(0.39f, 0.55f, 0.11f, 0.085f, Dark);
                Ellipse(0.61f, 0.55f, 0.11f, 0.085f, Dark);
            }

            Circle(0.39f, 0.55f, 0.055f, White);
            Circle(0.61f, 0.55f, 0.055f, White);
            Circle(0.40f, 0.55f, 0.028f, Dark);
            Circle(0.60f, 0.55f, 0.028f, Dark);
            Circle(0.41f, 0.56f, 0.010f, White);
            Circle(0.61f, 0.56f, 0.010f, White);

            Circle(0.50f, 0.45f, 0.032f, kind == AnimalKind.Frog ? Mouth : Dark);
            Circle(0.50f, 0.385f, 0.012f, Mouth);
            Circle(0.445f, 0.405f, 0.026f, Pink);
            Circle(0.555f, 0.405f, 0.026f, Pink);

            if (kind == AnimalKind.Dog)
            {
                Ellipse(0.50f, 0.43f, 0.11f, 0.075f, White);
                Circle(0.50f, 0.45f, 0.035f, Dark);
            }
            else if (kind == AnimalKind.Bear)
            {
                Ellipse(0.50f, 0.42f, 0.10f, 0.075f, new Color32(226, 196, 158, 255));
                Circle(0.50f, 0.44f, 0.032f, Dark);
            }
            else if (kind == AnimalKind.Fox)
            {
                Triangle(new Vector2(0.36f, 0.47f), new Vector2(0.64f, 0.47f), new Vector2(0.50f, 0.34f), White);
                Circle(0.50f, 0.45f, 0.030f, Dark);
            }
            else if (kind == AnimalKind.Elephant)
            {
                Ellipse(0.50f, 0.28f, 0.075f, 0.17f, Fur);
                Circle(0.50f, 0.14f, 0.065f, Fur);
            }
            else if (kind == AnimalKind.Cat)
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.47f - i * 0.025f;
                    Plot(Mathf.RoundToInt(0.30f * W), Mathf.RoundToInt(y * H), Dark);
                    Plot(Mathf.RoundToInt(0.70f * W), Mathf.RoundToInt(y * H), Dark);
                    Plot(Mathf.RoundToInt(0.26f * W), Mathf.RoundToInt((y - 0.015f) * H), Dark);
                    Plot(Mathf.RoundToInt(0.74f * W), Mathf.RoundToInt((y - 0.015f) * H), Dark);
                }
            }
        }
    }
}
