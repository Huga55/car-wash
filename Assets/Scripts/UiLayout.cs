using UnityEngine;

/// <summary>Result of fitting the fixed design canvas into the device safe area.</summary>
public struct UiFit
{
    public readonly float Scale;
    public readonly Vector2 Offset;

    public UiFit(float scale, Vector2 offset)
    {
        Scale = scale;
        Offset = offset;
    }
}

/// <summary>
/// CanvasScaler uses a geometric mean between width and height scaling. This
/// helper repeats that calculation so the design canvas can be scaled and
/// centred inside Screen.safeArea. All screens are authored in one fixed
/// coordinate space, which makes clipping and overlapping impossible.
/// </summary>
public static class UiLayout
{
    public static float CanvasScaleFactor(Vector2 reference, int screenWidth, int screenHeight)
    {
        float refW = Mathf.Max(1f, reference.x);
        float refH = Mathf.Max(1f, reference.y);
        float widthScale = Mathf.Max(1f, screenWidth) / refW;
        float heightScale = Mathf.Max(1f, screenHeight) / refH;
        return Mathf.Sqrt(widthScale * heightScale);
    }

    public static UiFit Compute(Vector2 reference, int screenWidth, int screenHeight, Rect safeArea)
    {
        float canvasScale = CanvasScaleFactor(reference, screenWidth, screenHeight);
        float safeWidth = Mathf.Max(1f, safeArea.width) / canvasScale;
        float safeHeight = Mathf.Max(1f, safeArea.height) / canvasScale;

        float scale = Mathf.Min(safeWidth / Mathf.Max(1f, reference.x),
            safeHeight / Mathf.Max(1f, reference.y));
        if (scale <= 0f || float.IsNaN(scale)) scale = 0.001f;

        float centerX = (safeArea.x + safeArea.width * 0.5f - screenWidth * 0.5f) / canvasScale;
        float centerY = (safeArea.y + safeArea.height * 0.5f - screenHeight * 0.5f) / canvasScale;
        return new UiFit(scale, new Vector2(centerX, centerY));
    }
}
