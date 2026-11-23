using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Tiny UI helper library. Everything is generated at runtime so the
/// project has no font, sprite or prefab dependencies.</summary>
public static class UiFactory
{
    private static Font _font;
    private static Sprite _round;
    private static Sprite _star;

    public static Font Font
    {
        get
        {
            if (_font == null)
            {
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
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
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
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        Label(image.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        return button;
    }
}
