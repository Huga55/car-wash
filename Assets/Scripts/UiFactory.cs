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

    /// <summary>Simple tool icons, baked once and shared by every level.</summary>
    public static Sprite ToolSprite(WashingTool tool)
    {
        int index = (int)tool;
        if (_tools[index] != null) return _tools[index];

        const int s = 64;
        var pixels = new Color32[s * s];
        Color32 hose = new Color32(64, 154, 236, 255);
        Color32 dark = new Color32(48, 72, 104, 255);
        Color32 foam = new Color32(255, 255, 255, 255);
        Color32 sponge = new Color32(255, 206, 72, 255);
        Color32 dryer = new Color32(246, 132, 156, 255);

        if (tool == WashingTool.Hose)
        {
            Line(pixels, s, 10, 48, 30, 24, hose, 7f);
            Line(pixels, s, 30, 24, 50, 44, hose, 7f);
            Box(pixels, s, 44, 38, 15, 12, dark);
            Disc(pixels, s, 56, 52, 4f, new Color32(126, 214, 255, 255));
            Disc(pixels, s, 50, 58, 3f, new Color32(126, 214, 255, 255));
            Disc(pixels, s, 60, 42, 2.5f, new Color32(126, 214, 255, 255));
        }
        else if (tool == WashingTool.Foam)
        {
            Box(pixels, s, 26, 12, 20, 22, new Color32(96, 196, 156, 255));
            Box(pixels, s, 33, 6, 7, 8, dark);
            Disc(pixels, s, 22, 43, 7f, foam);
            Disc(pixels, s, 39, 48, 9f, foam);
            Disc(pixels, s, 50, 39, 5f, foam);
            Disc(pixels, s, 31, 32, 4f, foam);
            Disc(pixels, s, 25, 46, 2f, new Color32(170, 222, 255, 255));
            Disc(pixels, s, 42, 51, 2f, new Color32(170, 222, 255, 255));
        }
        else if (tool == WashingTool.Sponge)
        {
            Box(pixels, s, 12, 18, 40, 28, sponge);
            Box(pixels, s, 12, 18, 40, 5, new Color32(255, 228, 130, 255));
            Disc(pixels, s, 21, 30, 4f, new Color32(222, 166, 48, 255));
            Disc(pixels, s, 39, 26, 3f, new Color32(222, 166, 48, 255));
            Disc(pixels, s, 45, 38, 4f, new Color32(222, 166, 48, 255));
            Disc(pixels, s, 28, 40, 3f, new Color32(222, 166, 48, 255));
        }
        else
        {
            Box(pixels, s, 16, 20, 26, 24, dryer);
            Box(pixels, s, 40, 24, 11, 16, new Color32(236, 96, 124, 255));
            Line(pixels, s, 52, 24, 61, 18, new Color32(255, 236, 150, 255), 3f);
            Line(pixels, s, 52, 32, 62, 32, new Color32(255, 236, 150, 255), 3f);
            Line(pixels, s, 52, 40, 61, 46, new Color32(255, 236, 150, 255), 3f);
            Disc(pixels, s, 22, 45, 4f, new Color32(255, 236, 150, 255));
        }

        var texture = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        _tools[index] = Sprite.Create(texture, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _tools[index];
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
