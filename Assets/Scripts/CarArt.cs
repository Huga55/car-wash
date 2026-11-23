using UnityEngine;

/// <summary>
/// Draws every car with simple 2D shapes straight into a small texture.
/// No models, no image assets, no materials: fast to load and easy on GPUs.
/// All coordinates are normalised (0..1), with y pointing up.
/// </summary>
public static class CarArt
{
    private static readonly Color32 Tire = new Color32(46, 48, 58, 255);
    private static readonly Color32 Hub = new Color32(222, 228, 236, 255);
    private static readonly Color32 Glass = new Color32(146, 212, 236, 255);
    private static readonly Color32 Light = new Color32(255, 238, 152, 255);
    private static readonly Color32 Red = new Color32(236, 74, 72, 255);
    private static readonly Color32 Blue = new Color32(72, 132, 236, 255);
    private static readonly Color32 White = new Color32(252, 252, 252, 255);
    private static readonly Color32 Dark = new Color32(52, 58, 70, 255);
    private static readonly Color32 Metal = new Color32(178, 190, 202, 255);
    private static readonly Color32 Mud = new Color32(126, 92, 58, 255);

    public static Color32[] Render(CarKind kind, Color body, Color accent, float scale, int width, int height)
    {
        var p = new Painter(width, height, scale, body, accent);
        switch (kind)
        {
            case CarKind.Sedan: p.SedanShape(); break;
            case CarKind.Beetle: p.Beetle(); break;
            case CarKind.Sports: p.SportsShape(); break;
            case CarKind.Race: p.RaceCar(); break;
            case CarKind.Taxi: p.Taxi(); break;
            case CarKind.Police: p.Police(); break;
            case CarKind.Ambulance: p.Ambulance(); break;
            case CarKind.Bus: p.Bus(); break;
            case CarKind.SchoolBus: p.SchoolBus(); break;
            case CarKind.FireTruck: p.FireTruck(); break;
            case CarKind.Tractor: p.Tractor(); break;
            case CarKind.Excavator: p.Excavator(); break;
            case CarKind.Bulldozer: p.Bulldozer(); break;
            case CarKind.DumpTruck: p.DumpTruck(); break;
            case CarKind.Mixer: p.Mixer(); break;
            case CarKind.GarbageTruck: p.GarbageTruck(); break;
            case CarKind.IceCream: p.IceCream(); break;
            case CarKind.Limo: p.Limo(); break;
            case CarKind.MonsterTruck: p.MonsterTruck(); break;
            case CarKind.Van: p.VanShape(); break;
        }
        return p.Pixels;
    }

    public static Texture2D MakeTexture(Color32[] pixels, int width, int height, string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private sealed class Painter
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Pixels;
        private readonly float _scale;
        private readonly Color32 _body;
        private readonly Color32 _accent;

        public Painter(int w, int h, float scale, Color body, Color accent)
        {
            W = w;
            H = h;
            _scale = scale;
            _body = body;
            _accent = accent;
            Pixels = new Color32[w * h];
        }

        // All authored points are normalised 0..1 with y pointing up. Primitive
        // methods below work in pixel space, so convert here. Forgetting the
        // W/H multiplication drew every part around the texture's top-left
        // corner and made the cars look like scattered fragments.
        private Vector2 P(float x, float y)
        {
            return new Vector2(
                (0.5f + (x - 0.5f) * _scale) * W,
                (0.5f + (y - 0.5f) * _scale) * H);
        }

        private void Plot(int x, int y, Color32 source, float alpha)
        {
            alpha = Mathf.Clamp01(alpha) * source.a / 255f;
            if (alpha <= 0.002f) return;
            int i = y * W + x;
            Color32 d = Pixels[i];
            float da = d.a / 255f;
            float outA = alpha + da * (1f - alpha);
            float inv = da * (1f - alpha);
            Pixels[i] = new Color32(
                (byte)((source.r * alpha + d.r * inv) / outA),
                (byte)((source.g * alpha + d.g * inv) / outA),
                (byte)((source.b * alpha + d.b * inv) / outA),
                (byte)(outA * 255f));
        }

        private void RoundRect(float cx, float cy, float w, float h, float r, Color32 color)
        {
            Vector2 c = P(cx, cy);
            float pw = w * _scale * W;
            float ph = h * _scale * H;
            float pr = Mathf.Max(0.5f, r * _scale * H);
            float rx = Mathf.Max(0.4f, pw * 0.5f - pr);
            float ry = Mathf.Max(0.4f, ph * 0.5f - pr);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - pw * 0.5f - pr));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(c.x + pw * 0.5f + pr));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - ph * 0.5f - pr));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(c.y + ph * 0.5f + pr));

            for (int y = y0; y <= y1; y++)
            {
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - c.y) - ry, 0f);
                for (int x = x0; x <= x1; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - c.x) - rx, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - pr;
                    float coverage = Mathf.Clamp01(0.5f - dist);
                    if (coverage > 0f) Plot(x, y, color, coverage);
                }
            }
        }

        private void Rect(float cx, float cy, float w, float h, Color32 color)
        {
            RoundRect(cx, cy, w, h, 0.004f, color);
        }

        private void Circle(float cx, float cy, float r, Color32 color)
        {
            Vector2 c = P(cx, cy);
            float pr = Mathf.Max(0.6f, r * _scale * H);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - pr));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(c.x + pr));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - pr));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(c.y + pr));
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - c.y;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - pr;
                    float coverage = Mathf.Clamp01(0.5f - d);
                    if (coverage > 0f) Plot(x, y, color, coverage);
                }
            }
        }

        private void Poly(Vector2[] points, Color32 color)
        {
            var mapped = new Vector2[points.Length];
            float minX = W, maxX = 0, minY = H, maxY = 0;
            for (int i = 0; i < points.Length; i++)
            {
                mapped[i] = P(points[i].x, points[i].y);
                minX = Mathf.Min(minX, mapped[i].x);
                maxX = Mathf.Max(maxX, mapped[i].x);
                minY = Mathf.Min(minY, mapped[i].y);
                maxY = Mathf.Max(maxY, mapped[i].y);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(maxY));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    if (Inside(x + 0.25f, y + 0.25f, mapped)) hits++;
                    if (Inside(x + 0.75f, y + 0.25f, mapped)) hits++;
                    if (Inside(x + 0.25f, y + 0.75f, mapped)) hits++;
                    if (Inside(x + 0.75f, y + 0.75f, mapped)) hits++;
                    if (hits > 0) Plot(x, y, color, hits / 4f);
                }
            }
        }

        private static bool Inside(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > y) != (poly[j].y > y) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        private static Vector2 Pt(float x, float y) { return new Vector2(x, y); }

        private void Wheel(float x, float y, float r)
        {
            Circle(x, y, r, Tire);
            Circle(x, y, r * 0.56f, Hub);
            Circle(x, y, r * 0.22f, Dark);
        }

        private void Wheels(float x1, float x2, float y, float r)
        {
            Wheel(x1, y, r);
            Wheel(x2, y, r);
        }

        private void Wheels3(float x1, float x2, float x3, float y, float r)
        {
            Wheel(x1, y, r);
            Wheel(x2, y, r);
            Wheel(x3, y, r);
        }

        private void Wheels4(float x1, float x2, float x3, float x4, float y, float r)
        {
            Wheel(x1, y, r);
            Wheel(x2, y, r);
            Wheel(x3, y, r);
            Wheel(x4, y, r);
        }

        private void LightBar(float x, float y)
        {
            RoundRect(x, y, 0.15f, 0.045f, 0.012f, Dark);
            Circle(x - 0.04f, y, 0.025f, Red);
            Circle(x + 0.04f, y, 0.025f, Blue);
        }

        private void Cross(float x, float y, float size, Color32 color)
        {
            Rect(x, y, size * 0.32f, size, color);
            Rect(x, y, size, size * 0.32f, color);
        }

        private void CheckerBand(float y)
        {
            for (int i = 0; i < 12; i++)
                Rect(0.155f + i * 0.063f, y, 0.063f, 0.038f, i % 2 == 0 ? White : Dark);
        }

        public void SedanShape()
        {
            Wheels(0.28f, 0.72f, 0.27f, 0.095f);
            RoundRect(0.5f, 0.47f, 0.78f, 0.32f, 0.10f, _body);
            RoundRect(0.5f, 0.37f, 0.73f, 0.11f, 0.04f, _accent);
            Poly(new[] { Pt(0.29f, 0.60f), Pt(0.40f, 0.77f), Pt(0.63f, 0.77f), Pt(0.75f, 0.60f) }, _body);
            Poly(new[] { Pt(0.35f, 0.62f), Pt(0.42f, 0.74f), Pt(0.50f, 0.74f), Pt(0.50f, 0.62f) }, Glass);
            Poly(new[] { Pt(0.52f, 0.62f), Pt(0.52f, 0.74f), Pt(0.61f, 0.74f), Pt(0.68f, 0.62f) }, Glass);
            Circle(0.88f, 0.49f, 0.035f, Light);
            Circle(0.12f, 0.49f, 0.030f, Red);
            Rect(0.50f, 0.47f, 0.007f, 0.20f, Dark);
            RoundRect(0.77f, 0.63f, 0.06f, 0.035f, 0.012f, _body);
        }

        public void Beetle()
        {
            Wheels(0.31f, 0.69f, 0.26f, 0.09f);
            RoundRect(0.5f, 0.47f, 0.72f, 0.40f, 0.19f, _body);
            RoundRect(0.5f, 0.70f, 0.44f, 0.22f, 0.10f, _body);
            RoundRect(0.5f, 0.71f, 0.35f, 0.15f, 0.07f, Glass);
            Circle(0.83f, 0.56f, 0.040f, Light);
            Circle(0.83f, 0.39f, 0.035f, Light);
            RoundRect(0.5f, 0.34f, 0.70f, 0.05f, 0.018f, _accent);
            Circle(0.16f, 0.47f, 0.028f, Red);
        }

        public void SportsShape()
        {
            Wheels(0.26f, 0.74f, 0.25f, 0.085f);
            Poly(new[]
            {
                Pt(0.08f, 0.34f), Pt(0.18f, 0.53f), Pt(0.38f, 0.57f), Pt(0.60f, 0.59f),
                Pt(0.88f, 0.45f), Pt(0.94f, 0.36f), Pt(0.80f, 0.30f), Pt(0.20f, 0.30f)
            }, _body);
            Poly(new[] { Pt(0.38f, 0.57f), Pt(0.47f, 0.75f), Pt(0.63f, 0.75f), Pt(0.74f, 0.55f) }, _body);
            Poly(new[] { Pt(0.43f, 0.59f), Pt(0.50f, 0.71f), Pt(0.61f, 0.71f), Pt(0.69f, 0.57f) }, Glass);
            Rect(0.13f, 0.65f, 0.18f, 0.025f, _accent);
            Poly(new[] { Pt(0.10f, 0.405f), Pt(0.90f, 0.435f), Pt(0.90f, 0.455f), Pt(0.10f, 0.425f) }, _accent);
            Circle(0.90f, 0.40f, 0.025f, Light);
        }

        public void RaceCar()
        {
            SportsShape();
            Rect(0.12f, 0.75f, 0.28f, 0.030f, _accent);
            Rect(0.19f, 0.69f, 0.012f, 0.10f, _accent);
            Rect(0.30f, 0.69f, 0.012f, 0.10f, _accent);
            Circle(0.52f, 0.44f, 0.062f, White);
            Circle(0.52f, 0.44f, 0.036f, _accent);
            for (int i = 0; i < 6; i++)
                Rect(0.20f + i * 0.055f, 0.325f, 0.055f, 0.022f, i % 2 == 0 ? White : Dark);
        }

        public void Taxi()
        {
            SedanShape();
            CheckerBand(0.40f);
            RoundRect(0.50f, 0.84f, 0.14f, 0.07f, 0.02f, _accent);
            Rect(0.50f, 0.86f, 0.012f, 0.045f, Dark);
            Rect(0.50f, 0.82f, 0.065f, 0.012f, Dark);
        }

        public void Police()
        {
            SedanShape();
            RoundRect(0.43f, 0.49f, 0.14f, 0.17f, 0.02f, White);
            RoundRect(0.60f, 0.49f, 0.14f, 0.17f, 0.02f, White);
            LightBar(0.50f, 0.82f);
            Rect(0.50f, 0.40f, 0.70f, 0.025f, Blue);
        }

        public void Ambulance()
        {
            VanShape();
            RoundRect(0.40f, 0.51f, 0.15f, 0.15f, 0.02f, White);
            Cross(0.40f, 0.51f, 0.11f, Red);
            LightBar(0.52f, 0.76f);
        }

        public void VanShape()
        {
            Wheels(0.28f, 0.74f, 0.26f, 0.085f);
            RoundRect(0.5f, 0.50f, 0.82f, 0.42f, 0.10f, _body);
            RoundRect(0.5f, 0.38f, 0.80f, 0.10f, 0.03f, _accent);
            Poly(new[] { Pt(0.68f, 0.65f), Pt(0.82f, 0.65f), Pt(0.82f, 0.54f), Pt(0.68f, 0.54f) }, Glass);
            RoundRect(0.40f, 0.63f, 0.28f, 0.13f, 0.03f, Glass);
            Circle(0.89f, 0.46f, 0.030f, Light);
            Rect(0.52f, 0.50f, 0.007f, 0.22f, Dark);
        }

        public void Bus()
        {
            Wheels3(0.20f, 0.50f, 0.80f, 0.26f, 0.075f);
            RoundRect(0.5f, 0.50f, 0.92f, 0.46f, 0.12f, _body);
            RoundRect(0.5f, 0.39f, 0.90f, 0.10f, 0.03f, _accent);
            for (int i = 0; i < 6; i++)
                RoundRect(0.17f + i * 0.132f, 0.63f, 0.10f, 0.13f, 0.02f, Glass);
            Rect(0.11f, 0.48f, 0.008f, 0.30f, Dark);
            Circle(0.93f, 0.45f, 0.030f, Light);
            Circle(0.07f, 0.45f, 0.026f, Red);
        }

        public void FireTruck()
        {
            Wheels3(0.22f, 0.50f, 0.79f, 0.26f, 0.085f);
            RoundRect(0.35f, 0.51f, 0.52f, 0.38f, 0.06f, _body);
            RoundRect(0.76f, 0.51f, 0.24f, 0.34f, 0.07f, _body);
            Rect(0.35f, 0.38f, 0.50f, 0.055f, White);
            Poly(new[] { Pt(0.69f, 0.62f), Pt(0.84f, 0.62f), Pt(0.84f, 0.52f), Pt(0.69f, 0.52f) }, Glass);
            Rect(0.30f, 0.73f, 0.42f, 0.018f, Metal);
            Rect(0.30f, 0.77f, 0.42f, 0.018f, Metal);
            for (int i = 0; i < 8; i++) Rect(0.12f + i * 0.051f, 0.75f, 0.009f, 0.048f, Metal);
            LightBar(0.80f, 0.72f);
            Circle(0.91f, 0.44f, 0.028f, Light);
        }

        public void Tractor()
        {
            Wheel(0.30f, 0.28f, 0.15f);
            Wheel(0.77f, 0.21f, 0.085f);
            RoundRect(0.62f, 0.45f, 0.36f, 0.22f, 0.05f, _body);
            RoundRect(0.36f, 0.63f, 0.34f, 0.34f, 0.07f, _body);
            RoundRect(0.36f, 0.69f, 0.26f, 0.18f, 0.05f, Glass);
            Rect(0.53f, 0.62f, 0.022f, 0.24f, Dark);
            Rect(0.79f, 0.45f, 0.030f, 0.18f, _accent);
            Circle(0.90f, 0.41f, 0.025f, Light);
        }

        public void DumpTruck()
        {
            Wheels3(0.24f, 0.52f, 0.80f, 0.26f, 0.09f);
            RoundRect(0.74f, 0.48f, 0.26f, 0.28f, 0.05f, _body);
            Poly(new[] { Pt(0.11f, 0.42f), Pt(0.16f, 0.67f), Pt(0.52f, 0.73f), Pt(0.56f, 0.44f) }, _accent);
            Poly(new[] { Pt(0.18f, 0.60f), Pt(0.49f, 0.66f), Pt(0.51f, 0.58f), Pt(0.19f, 0.53f) }, Mud);
            Poly(new[] { Pt(0.67f, 0.57f), Pt(0.79f, 0.68f), Pt(0.84f, 0.68f), Pt(0.84f, 0.57f) }, Glass);
            Circle(0.90f, 0.43f, 0.026f, Light);
        }

        public void Mixer()
        {
            Wheels3(0.24f, 0.52f, 0.80f, 0.26f, 0.09f);
            RoundRect(0.74f, 0.48f, 0.26f, 0.28f, 0.05f, _body);
            Circle(0.38f, 0.58f, 0.21f, _accent);
            Rect(0.38f, 0.70f, 0.38f, 0.022f, White);
            Rect(0.38f, 0.58f, 0.40f, 0.020f, White);
            Rect(0.38f, 0.46f, 0.35f, 0.020f, White);
            Poly(new[] { Pt(0.67f, 0.57f), Pt(0.79f, 0.67f), Pt(0.84f, 0.67f), Pt(0.84f, 0.57f) }, Glass);
            Circle(0.90f, 0.43f, 0.026f, Light);
        }

        public void GarbageTruck()
        {
            Wheels3(0.23f, 0.52f, 0.80f, 0.26f, 0.09f);
            RoundRect(0.36f, 0.51f, 0.54f, 0.38f, 0.06f, _body);
            RoundRect(0.78f, 0.48f, 0.22f, 0.30f, 0.05f, _accent);
            for (int i = 0; i < 5; i++) Rect(0.14f + i * 0.105f, 0.51f, 0.014f, 0.32f, Dark);
            Poly(new[] { Pt(0.72f, 0.57f), Pt(0.83f, 0.65f), Pt(0.88f, 0.65f), Pt(0.88f, 0.57f) }, Glass);
            Circle(0.90f, 0.43f, 0.025f, Light);
        }

        public void IceCream()
        {
            Wheels(0.28f, 0.72f, 0.26f, 0.085f);
            RoundRect(0.5f, 0.49f, 0.82f, 0.40f, 0.10f, _body);
            var rainbow = new[] { Red, new Color32(255, 170, 70, 255), new Color32(255, 235, 90, 255),
                new Color32(120, 205, 110, 255), Blue, new Color32(190, 120, 220, 255) };
            for (int i = 0; i < rainbow.Length; i++)
                Poly(new[]
                {
                    Pt(0.16f + i * 0.11f, 0.36f), Pt(0.22f + i * 0.11f, 0.36f),
                    Pt(0.26f + i * 0.11f, 0.61f), Pt(0.20f + i * 0.11f, 0.61f)
                }, rainbow[i]);
            Poly(new[] { Pt(0.42f, 0.69f), Pt(0.58f, 0.69f), Pt(0.50f, 0.89f) }, new Color32(226, 168, 96, 255));
            Circle(0.50f, 0.87f, 0.065f, new Color32(255, 170, 190, 255));
            RoundRect(0.69f, 0.62f, 0.20f, 0.12f, 0.03f, Glass);
            Circle(0.89f, 0.45f, 0.026f, Light);
        }

        public void Limo()
        {
            Wheels4(0.20f, 0.39f, 0.63f, 0.82f, 0.25f, 0.070f);
            RoundRect(0.5f, 0.46f, 0.94f, 0.28f, 0.09f, _body);
            RoundRect(0.5f, 0.64f, 0.60f, 0.18f, 0.07f, _body);
            RoundRect(0.5f, 0.66f, 0.54f, 0.13f, 0.05f, Glass);
            Rect(0.62f, 0.46f, 0.008f, 0.30f, Dark);
            Rect(0.5f, 0.37f, 0.90f, 0.022f, _accent);
            Circle(0.94f, 0.44f, 0.028f, Light);
        }

        public void MonsterTruck()
        {
            Wheels(0.25f, 0.75f, 0.29f, 0.15f);
            Rect(0.5f, 0.42f, 0.68f, 0.045f, Dark);
            RoundRect(0.5f, 0.54f, 0.70f, 0.28f, 0.06f, _body);
            RoundRect(0.5f, 0.74f, 0.42f, 0.14f, 0.04f, _body);
            RoundRect(0.5f, 0.75f, 0.34f, 0.10f, 0.03f, Glass);
            Rect(0.5f, 0.45f, 0.68f, 0.028f, _accent);
            Circle(0.88f, 0.54f, 0.035f, Light);
        }

        public void SchoolBus()
        {
            Bus();
            RoundRect(0.50f, 0.79f, 0.20f, 0.055f, 0.014f, White);
            Rect(0.50f, 0.79f, 0.11f, 0.022f, Red);
            RoundRect(0.09f, 0.57f, 0.055f, 0.16f, 0.012f, Red);
            Rect(0.09f, 0.57f, 0.030f, 0.10f, White);
            Rect(0.50f, 0.39f, 0.86f, 0.022f, Dark);
        }

        public void Excavator()
        {
            RoundRect(0.31f, 0.24f, 0.36f, 0.16f, 0.07f, Dark);
            RoundRect(0.72f, 0.24f, 0.36f, 0.16f, 0.07f, Dark);
            for (int i = 0; i < 4; i++)
            {
                Circle(0.22f + i * 0.055f, 0.24f, 0.017f, Metal);
                Circle(0.63f + i * 0.055f, 0.24f, 0.017f, Metal);
            }
            RoundRect(0.52f, 0.43f, 0.68f, 0.20f, 0.04f, _body);
            RoundRect(0.70f, 0.63f, 0.24f, 0.28f, 0.05f, _body);
            Poly(new[] { Pt(0.62f, 0.64f), Pt(0.75f, 0.78f), Pt(0.79f, 0.78f), Pt(0.79f, 0.61f) }, Glass);
            Poly(new[] { Pt(0.26f, 0.50f), Pt(0.31f, 0.86f), Pt(0.38f, 0.86f), Pt(0.34f, 0.50f) }, _accent);
            Poly(new[] { Pt(0.34f, 0.84f), Pt(0.63f, 0.72f), Pt(0.65f, 0.66f), Pt(0.36f, 0.78f) }, _accent);
            Poly(new[] { Pt(0.63f, 0.71f), Pt(0.58f, 0.51f), Pt(0.65f, 0.49f), Pt(0.70f, 0.68f) }, Metal);
            Poly(new[] { Pt(0.58f, 0.51f), Pt(0.67f, 0.45f), Pt(0.77f, 0.49f), Pt(0.70f, 0.57f), Pt(0.63f, 0.53f) }, Mud);
            Circle(0.88f, 0.42f, 0.026f, Light);
        }

        public void Bulldozer()
        {
            RoundRect(0.34f, 0.23f, 0.40f, 0.17f, 0.07f, Dark);
            RoundRect(0.73f, 0.23f, 0.40f, 0.17f, 0.07f, Dark);
            for (int i = 0; i < 5; i++)
            {
                Circle(0.23f + i * 0.055f, 0.23f, 0.016f, Metal);
                Circle(0.62f + i * 0.055f, 0.23f, 0.016f, Metal);
            }
            RoundRect(0.60f, 0.44f, 0.58f, 0.22f, 0.04f, _body);
            RoundRect(0.63f, 0.64f, 0.30f, 0.30f, 0.05f, _body);
            Poly(new[] { Pt(0.55f, 0.66f), Pt(0.68f, 0.82f), Pt(0.75f, 0.82f), Pt(0.75f, 0.63f) }, Glass);
            Rect(0.48f, 0.61f, 0.022f, 0.30f, Dark);
            Poly(new[] { Pt(0.08f, 0.20f), Pt(0.24f, 0.72f), Pt(0.32f, 0.72f), Pt(0.18f, 0.20f) }, Metal);
            Rect(0.12f, 0.22f, 0.20f, 0.055f, _accent);
            Circle(0.88f, 0.42f, 0.026f, Light);
        }
    }
}
