using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns the car image, its wash overlay and its movement. Replaying a
/// level reuses these Images; only the small textures are replaced.</summary>
public sealed class CarViewController
{
    private readonly RectTransform _carRect;
    private readonly RectTransform _overlayRect;
    private Texture2D _carTexture;
    private Texture2D _overlayTexture;
    private Sprite _carSprite;
    private Sprite _overlaySprite;
    private Vector2 _home;
    private float _wobble;
    private bool _driving;

    public CarViewController(Image car, Image overlay)
    {
        _carRect = car.rectTransform;
        _overlayRect = overlay.rectTransform;
        _home = _carRect.anchoredPosition;
    }

    public Sprite CleanSprite { get { return _carSprite; } }

    public void Load(Color32[] pixels, LevelDef level, WashSurface surface)
    {
        Dispose();
        _home = new Vector2(0f, 0f);
        _wobble = 0f;
        _driving = false;

        _carTexture = CarArt.MakeTexture(pixels, 384, 256, "Car" + level.Index);
        _carSprite = Sprite.Create(_carTexture, new Rect(0, 0, 384, 256), new Vector2(0.5f, 0.5f), 100f);

        _overlayTexture = surface.Texture;
        _overlaySprite = Sprite.Create(_overlayTexture, new Rect(0, 0, WashSurface.W, WashSurface.H),
            new Vector2(0.5f, 0.5f), 100f);

        _carRect.anchorMin = new Vector2(0.5f, 0.5f);
        _carRect.anchorMax = new Vector2(0.5f, 0.5f);
        _carRect.sizeDelta = new Vector2(780f, 520f);
        _overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        _overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        _overlayRect.sizeDelta = new Vector2(780f, 520f);

        var carImage = _carRect.GetComponent<Image>();
        var overlayImage = _overlayRect.GetComponent<Image>();
        if (carImage != null) carImage.sprite = _carSprite;
        if (overlayImage != null) overlayImage.sprite = _overlaySprite;

        ResetPose();
        if (overlayImage != null) overlayImage.gameObject.SetActive(true);
    }

    public void ResetPose()
    {
        SetPose(_home, 0f, 1f);
    }

    /// <summary>Gentle idle suspension and a short damped reaction to washing.</summary>
    public void UpdateIdle(float time)
    {
        if (_driving || _carRect == null) return;
        float dt = Time.unscaledDeltaTime;
        _wobble = Mathf.Max(0f, _wobble - dt * 2.2f);
        _home.x = Mathf.Lerp(_home.x, 0f, Mathf.Clamp01(dt * 2f));
        float bob = Mathf.Sin(time * 1.7f) * 2.2f;
        float kick = Mathf.Sin(time * 16f) * _wobble * 3.4f;
        float angle = Mathf.Sin(time * 1.1f) * 0.45f + kick * 0.35f;
        SetPose(_home + new Vector2(0f, bob + kick), angle, 1f);
    }

    /// <summary>A small body reaction wherever water or a sponge lands.</summary>
    public void WashPulse(Vector2 uv)
    {
        if (_driving) return;
        float side = (uv.x - 0.5f) * 2f;
        _wobble = Mathf.Min(1f, _wobble + 0.32f);
        _home.x = Mathf.Clamp(_home.x - side * 0.35f, -3f, 3f);
    }

    public void DriveAway(float t)
    {
        float eased = t * t * (3f - 2f * t);
        float x = _home.x + eased * 1450f;
        float y = _home.y + Mathf.Sin(t * Mathf.PI) * 36f;
        SetPose(new Vector2(x, y), -4f * t, 1f);
    }

    public void HideOverlay()
    {
        var overlayImage = _overlayRect.GetComponent<Image>();
        if (overlayImage != null) overlayImage.gameObject.SetActive(false);
    }

    private void SetPose(Vector2 position, float angle, float scale)
    {
        _carRect.anchoredPosition = position;
        _carRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        _carRect.localScale = new Vector3(scale, scale, 1f);
        _overlayRect.anchoredPosition = position;
        _overlayRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        _overlayRect.localScale = new Vector3(scale, scale, 1f);
    }

    public void Dispose()
    {
        if (_carSprite != null) Object.Destroy(_carSprite);
        if (_overlaySprite != null) Object.Destroy(_overlaySprite);
        if (_carTexture != null) Object.Destroy(_carTexture);
        // _overlayTexture belongs to WashSurface and is disposed by GameController.
        _carSprite = null;
        _overlaySprite = null;
        _carTexture = null;
        _overlayTexture = null;
        ResetPose();
    }
}
