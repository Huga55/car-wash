using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Tiny UI helper library. Everything is generated at runtime so the
/// project has no font, sprite or prefab dependencies.</summary>
public static class UiFactory
{
    // Unity's built-in legacy font does not reliably contain Cyrillic glyphs.
    // Prefer an OS font that is guaranteed to render Russian text on both
    // Windows and Android, then fall back to the built-in font.
    private static readonly string[] OsFontNames =
    {
        "Arial", "Segoe UI", "Roboto", "Noto Sans", "DejaVu Sans", "Liberation Sans", "Tahoma"
    };

    private static Font _font;
    private static Sprite _round;
    private static Sprite _soft;
    private static Sprite _star;
    private static Sprite _lock;
    private static readonly Sprite[] _tools = new Sprite[4];

    public static Font Font
    {
        get
        {
            if (_font == null)
            {
                for (int i = 0; i < OsFontNames.Length; i++)
                {
                    Font candidate = null;
                    try { candidate = Font.CreateDynamicFontFromOSFont(OsFontNames[i], 32); } catch { }
                    if (candidate == null) continue;
                    if (candidate.HasCharacter('Ж'))
                    {
                        _font = candidate;
                        return _font;
                    }
                    Object.Destroy(candidate);
                }

                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                }
            }
            return _font;
        }
    }

    public static Sprite RoundSprite
    {
        get
        {
            if (_round == null)
            {
                const int s = 64;
                var pixels = new Color32[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 32f) - 14f, 0f);
                        float dy = Mathf.Max(Mathf.Abs(y + 0.5f - 32f) - 14f, 0f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - 18f;
                        byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                        pixels[y * s + x] = new Color32(255, 255, 255, a);
                    }
                }
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                _round = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            }
            return _round;
        }
    }

    // Thin bars, dots and small badges need a much smaller 9-slice border; a
    // 20px border on a 13px-high bar degenerates into a flat stretched strip.
    public static Sprite SoftSprite
    {
        get
        {
            if (_soft == null)
            {
                const int s = 32;
                var pixels = new Color32[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 16f) - 9f, 0f);
                        float dy = Mathf.Max(Mathf.Abs(y + 0.5f - 16f) - 9f, 0f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - 7f;
                        byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                        pixels[y * s + x] = new Color32(255, 255, 255, a);
                    }
                }
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                _soft = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            }
            return _soft;
        }
    }

    public static Sprite StarSprite
    {
        get
        {
            if (_star == null)
            {
                const int s = 64;
                var points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float radius = i % 2 == 0 ? 29f : 12.5f;
                    points[i] = new Vector2(32f + Mathf.Cos(angle) * radius, 32f + Mathf.Sin(angle) * radius);
                }
                var pixels = new Color32[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        int hits = 0;
                        if (Inside(x + 0.25f, y + 0.25f, points)) hits++;
                        if (Inside(x + 0.75f, y + 0.25f, points)) hits++;
                        if (Inside(x + 0.25f, y + 0.75f, points)) hits++;
                        if (Inside(x + 0.75f, y + 0.75f, points)) hits++;
                        pixels[y * s + x] = new Color32(255, 255, 255, (byte)(hits * 64));
                    }
                }
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                _star = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            }
            return _star;
        }
    }

    /// <summary>Detailed equipment renders, baked once and shared by every
    /// level. The same sprite is used for the HUD badge and the cursor that
    /// follows the child's finger, so the two always match.</summary>
    public static Sprite ToolSprite(WashingTool tool)
    {
        int index = (int)tool;
        if (_tools[index] != null) return _tools[index];

        const int s = 128;
        var px = new Color32[s * s];
        var dark = new Color32(42, 54, 70, 255);
        var mid = new Color32(84, 102, 124, 255);
        var metal = new Color32(190, 202, 214, 255);
        var rubber = new Color32(30, 36, 44, 255);
        var water = new Color32(152, 220, 246, 235);
        var foam = new Color32(246, 250, 252, 255);
        var amber = new Color32(226, 172, 72, 255);

        if (tool == WashingTool.Hose)
        {
            // Flexible rubber hose with a lighter top edge.
            Line(px, s, 8, 116, 26, 92, rubber, 13f);
            Line(px, s, 26, 92, 52, 74, rubber, 13f);
            Line(px, s, 8, 119, 26, 95, mid, 4f);
            Line(px, s, 26, 95, 52, 77, mid, 4f);
            // Hose-to-gun coupling.
            Box(px, s, 46, 62, 16, 16, metal);
            Box(px, s, 48, 64, 11, 4, dark);
            // Spray gun body, grip and trigger.
            GradientBox(px, s, 58, 60, 40, 28, mid, dark);
            Polygon(px, s, new[] { new Vector2(65, 60), new Vector2(84, 60), new Vector2(78, 24), new Vector2(64, 24) }, dark);
            Box(px, s, 72, 30, 8, 22, mid);
            Box(px, s, 86, 46, 8, 16, rubber);
            Box(px, s, 96, 62, 18, 14, metal);
            Box(px, s, 112, 65, 8, 8, new Color32(216, 228, 238, 255));
            Disc(px, s, 70, 80, 4f, water);
            // Pressurised spray cone.
            for (int i = 0; i < 14; i++)
            {
                float x = 120f + (i % 6) * 2f;
                float y = 68f + Mathf.Sin(i * 1.7f) * (3f + i * 0.8f);
                Disc(px, s, x, y, i % 3 == 0 ? 2.4f : 1.5f, water);
            }
        }
        else if (tool == WashingTool.Foam)
        {
            // Detergent bottle with a visible fill level and label.
            GradientBox(px, s, 20, 14, 42, 64, new Color32(112, 176, 206, 255), new Color32(52, 96, 130, 255));
            GradientBox(px, s, 23, 20, 36, 38, foam, new Color32(214, 236, 244, 255));
            Box(px, s, 25, 44, 32, 11, new Color32(64, 146, 158, 255));
            Box(px, s, 35, 78, 13, 14, metal);
            // Sprayer head and trigger.
            GradientBox(px, s, 42, 88, 52, 24, mid, dark);
            Box(px, s, 52, 74, 10, 16, rubber);
            Box(px, s, 92, 92, 20, 12, metal);
            Disc(px, s, 63, 100, 4f, water);
            // Thick foam spray.
            for (int i = 0; i < 18; i++)
            {
                float x = 108f + (i % 5) * 3f;
                float y = 92f + Mathf.Sin(i * 1.3f) * (5f + i * 0.7f);
                Disc(px, s, x, y, 2.6f + (i % 3) * 1.2f, foam);
            }
        }
        else if (tool == WashingTool.Sponge)
        {
            // Front, top and side faces give the sponge genuine volume.
            Polygon(px, s, new[] { new Vector2(18, 30), new Vector2(96, 30), new Vector2(108, 46), new Vector2(30, 46) },
                new Color32(198, 158, 52, 255));
            GradientBox(px, s, 18, 44, 78, 34, new Color32(252, 214, 92, 255), new Color32(212, 166, 54, 255));
            Polygon(px, s, new[] { new Vector2(18, 78), new Vector2(96, 78), new Vector2(108, 94), new Vector2(30, 94) },
                new Color32(255, 234, 148, 255));
            // Porous surface and a little soap on top.
            for (int i = 0; i < 26; i++)
            {
                float x = 24f + (i * 37 % 68);
                float y = 50f + (i * 19 % 26);
                Disc(px, s, x, y, 1.8f + (i % 3) * 0.7f, new Color32(186, 142, 42, 210));
            }
            Disc(px, s, 54, 88, 7f, foam);
            Disc(px, s, 76, 86, 5f, foam);
            Disc(px, s, 38, 90, 4f, foam);
        }
        else
        {
            // Dryer body, handle, vent grille and tapered nozzle.
            GradientBox(px, s, 18, 46, 62, 40, mid, dark);
            Box(px, s, 28, 18, 22, 30, rubber);
            Box(px, s, 32, 22, 14, 22, dark);
            for (int i = 0; i < 4; i++) Box(px, s, 26 + i * 13, 74, 5, 8, metal);
            Polygon(px, s, new[] { new Vector2(78, 52), new Vector2(112, 34), new Vector2(112, 94), new Vector2(78, 78) }, metal);
            Box(px, s, 108, 40, 7, 48, new Color32(222, 232, 240, 255));
            Disc(px, s, 40, 84, 5f, amber);
            // Air flow streaks.
            Line(px, s, 116, 60, 127, 52, water, 2.5f);
            Line(px, s, 116, 68, 127, 68, water, 2.5f);
            Line(px, s, 116, 76, 127, 84, water, 2.5f);
        }

        var texture = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(px);
        texture.Apply(false, false);
        _tools[index] = Sprite.Create(texture, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _tools[index];
    }

    private static void GradientBox(Color32[] px, int size, float x, float y, float w, float h,
        Color32 top, Color32 bottom)
    {
        for (int py = Mathf.FloorToInt(y); py < Mathf.CeilToInt(y + h); py++)
        {
            float t = (py - y) / Mathf.Max(1f, h);
            var row = Color.Lerp((Color)top, (Color)bottom, t);
            for (int pxi = Mathf.FloorToInt(x); pxi < Mathf.CeilToInt(x + w); pxi++)
                Dot(px, size, pxi, py, row);
        }
    }

    private static void ShadedDisc(Color32[] px, int size, float cx, float cy, float radius, Color32 color)
    {
        Disc(px, size, cx, cy, radius, color);
        Disc(px, size, cx - radius * 0.3f, cy + radius * 0.3f, radius * 0.35f,
            new Color32(255, 255, 255, 120));
    }

    private static void Polygon(Color32[] px, int size, Vector2[] points, Color32 color)
    {
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (Inside(x + 0.5f, y + 0.5f, points)) Dot(px, size, x, y, color);
    }

    public static Sprite LockSprite
    {
        get
        {
            if (_lock == null)
            {
                const int s = 64;
                var pixels = new Color32[s * s];
                var metal = new Color32(116, 132, 152, 255);
                Disc(pixels, s, 32, 40, 15f, metal);
                Disc(pixels, s, 32, 40, 9f, new Color32(0, 0, 0, 0));
                Box(pixels, s, 19, 15, 26, 28, new Color32(255, 214, 92, 255));
                Disc(pixels, s, 32, 31, 4f, metal);
                Box(pixels, s, 30, 19, 4, 12, metal);
                var texture = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                _lock = Sprite.Create(texture, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            }
            return _lock;
        }
    }

    private static void Dot(Color32[] pixels, int size, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        pixels[y * size + x] = color;
    }

    private static void Disc(Color32[] pixels, int size, float cx, float cy, float radius, Color32 color)
    {
        for (int y = Mathf.FloorToInt(cy - radius); y <= Mathf.CeilToInt(cy + radius); y++)
        {
            for (int x = Mathf.FloorToInt(cx - radius); x <= Mathf.CeilToInt(cx + radius); x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= radius * radius) Dot(pixels, size, x, y, color);
            }
        }
    }

    private static void Box(Color32[] pixels, int size, float x, float y, float w, float h, Color32 color)
    {
        for (int py = Mathf.FloorToInt(y); py < Mathf.CeilToInt(y + h); py++)
            for (int px = Mathf.FloorToInt(x); px < Mathf.CeilToInt(x + w); px++)
                Dot(pixels, size, px, py, color);
    }

    private static void Line(Color32[] pixels, int size, float x0, float y0, float x1, float y1,
        Color32 color, float width)
    {
        float distance = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Disc(pixels, size, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), width * 0.5f, color);
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

    public static Image Image(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position,
        Sprite sprite = null, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null) image.sprite = sprite;
        if (sliced) image.type = UnityEngine.UI.Image.Type.Sliced;
        return image;
    }

    public static Image CenteredImage(Transform parent, string name, Color color, Vector2 size, Vector2 position,
        Sprite sprite = null, bool sliced = false)
    {
        return Image(parent, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position, sprite, sliced);
    }

    public static Image StretchImage(Transform parent, string name, Color color, Sprite sprite = null)
    {
        return Image(parent, name, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, sprite);
    }

    public static Text Label(Transform parent, string name, string content, int fontSize, Color color,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position, bool shadow = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var text = go.GetComponent<Text>();
        text.font = Font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, fontSize / 2);
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        if (shadow)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.18f);
            s.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    public static Text CenteredLabel(Transform parent, string name, string content, int fontSize,
        Color color, Vector2 size, Vector2 position, bool shadow = true)
    {
        return Label(parent, name, content, fontSize, color, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position, shadow);
    }

    public static Button Button(Transform parent, string name, string label, Color background,
        int fontSize, Vector2 size, Vector2 position, UnityAction onClick)
    {
        var image = CenteredImage(parent, name, background, size, position, RoundSprite, true);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
        colors.selectedColor = background;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        colors.fadeDuration = 0.10f;
        button.colors = colors;
        button.interactable = true;
        button.onClick.AddListener(onClick);
        Label(image.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        return button;
    }
}
