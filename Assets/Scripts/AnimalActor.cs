using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns the Reward driver: portrait, speech, bobbing, waving and the
/// short walk into the clean car. Kept out of GameController on purpose.</summary>
public sealed class AnimalActor
{
    private readonly Image _portrait;
    private readonly Text _speech;
    private Texture2D _texture;
    private Sprite _sprite;
    private Vector2 _home;
    private float _appearTime;
    private bool _visible;
    private AnimalKind _kind;

    public AnimalActor(Image portrait, Text speech, Vector2 home)
    {
        _portrait = portrait;
        _speech = speech;
        _home = home;
        _portrait.gameObject.SetActive(false);
        if (_speech != null) _speech.gameObject.SetActive(false);
    }

    public bool Visible { get { return _visible; } }
    public Vector2 Position { get { return _portrait.rectTransform.anchoredPosition; } }
    public Vector2 Home { get { return _home; } }
    public AnimalKind Kind { get { return _kind; } }

    public void SetSpeech(string text, bool visible)
    {
        if (_speech == null) return;
        _speech.text = text;
        _speech.gameObject.SetActive(visible);
    }

    public void Show(int level, AnimalKind kind)
    {
        Dispose();
        _kind = kind;
        _texture = AnimalArt.Texture(kind);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 160, 160), new Vector2(0.5f, 0.5f), 100f);
        _portrait.sprite = _sprite;
        _appearTime = Time.unscaledTime;
        _portrait.rectTransform.anchoredPosition = _home + new Vector2(-260f, 0f);
        _portrait.rectTransform.localScale = new Vector3(0.85f, 0.85f, 1f);
        _portrait.gameObject.SetActive(true);
        if (_speech != null)
        {
            _speech.text = Loc.DriverThanks(level);
            _speech.gameObject.SetActive(true);
        }
        _visible = true;
    }

    public void Animate(float time)
    {
        if (!_visible) return;
        float bob = Mathf.Sin(time * 6f) * 9f;
        float wave = Mathf.Sin(time * 8f) * 9f;
        float enter = Mathf.Clamp01((time - _appearTime) / 0.5f);
        _portrait.rectTransform.anchoredPosition = _home +
            new Vector2(-260f * (1f - enter), bob * enter);
        _portrait.rectTransform.localRotation = Quaternion.Euler(0f, 0f, wave);
        if (_speech != null)
            _speech.rectTransform.anchoredPosition = new Vector2(_home.x, _home.y + 118f + bob * 0.4f);
    }

    public void SetPosition(Vector2 position)
    {
        _portrait.rectTransform.anchoredPosition = position;
        if (_speech != null)
            _speech.rectTransform.anchoredPosition = position + new Vector2(0f, 118f);
    }

    public void Hide()
    {
        _visible = false;
        _portrait.gameObject.SetActive(false);
        if (_speech != null) _speech.gameObject.SetActive(false);
    }

    public void Dispose()
    {
        Hide();
        if (_sprite != null) Object.Destroy(_sprite);
        if (_texture != null) Object.Destroy(_texture);
        _sprite = null;
        _texture = null;
    }
}
