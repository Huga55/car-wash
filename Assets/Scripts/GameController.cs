using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>All screens and game rules. Works with touch on Android and mouse on Windows.</summary>
public sealed class GameController : MonoBehaviour
{
    private enum View { Home, Levels, Play }

    private const int CarW = 384;
    private const int CarH = 256;
    private const float FinishAt = 0.62f; // forgiving: only 62% needs swiping

    private static readonly Color Ink = new Color32(38, 58, 84, 255);
    private static readonly Color Green = new Color32(76, 202, 122, 255);
    private static readonly Color Blue = new Color32(86, 166, 246, 255);
    private static readonly Color Yellow = new Color32(255, 208, 74, 255);
    private static readonly Color Pink = new Color32(246, 128, 168, 255);
    private static readonly Color Pale = new Color32(255, 255, 255, 225);

    private View _view;
    private Transform _root;
    private Image _background;
    private readonly Sprite[] _backgroundSprites = new Sprite[5];

    private GameObject _homePanel;
    private GameObject _levelsPanel;
    private GameObject _playPanel;
    private GameObject _completeCard;
    private RectTransform _levelGrid;

    private Image _preview;
    private RectTransform _washRect;
    private Image _carImage;
    private Image _overlayImage;
    private Image _progressFill;
    private Image _flash;
    private Image[] _stageDots;
    private Image[] _completeStars;
    private Image[] _sparkles;
    private GameObject _hint;
    private Text _instruction;
    private Text _levelLabel;
    private Text _completeTitle;
    private Text _completeSubtitle;
    private Button _nextButton;
    private readonly List<Text> _soundLabels = new List<Text>();

    private Texture2D _carTexture;
    private Texture2D _overlayTexture;
    private Sprite _carSprite;
    private Sprite _overlaySprite;
    private WashSurface _surface;

    private int _level;
    private WashStage _stage;
    private bool _transitioning;
    private float _elapsed;
    private int _strokes;
    private float _flashAlpha;
    private readonly Dictionary<int, Vector2> _lastPoints = new Dictionary<int, Vector2>();

    private void Awake()
    {
        BuildUi();
        ShowHome();
    }

    private void Update()
    {
        float time = Time.unscaledTime;

        if (_view == View.Home && _preview != null)
        {
            _preview.rectTransform.anchoredPosition = new Vector2(0f, 10f + Mathf.Sin(time * 1.6f) * 8f);
            _preview.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.1f) * 2.5f);
        }

        if (_flashAlpha > 0f)
        {
            _flashAlpha = Mathf.Max(0f, _flashAlpha - Time.unscaledDeltaTime * 1.4f);
            _flash.color = new Color(1f, 1f, 1f, _flashAlpha);
        }

        if (_view != View.Play) return;

        if (_hint != null && _hint.activeSelf)
        {
            Vector3 p = _hint.transform.localPosition;
            p.y = -30f + Mathf.Sin(time * 4f) * 16f;
            p.x = -170f + Mathf.Cos(time * 2.2f) * 45f;
            _hint.transform.localPosition = p;
        }

        for (int i = 0; i < _sparkles.Length; i++)
        {
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3.4f + i * 1.3f));
            bool visible = _stage == WashStage.Rinse || _stage == WashStage.Done;
            _sparkles[i].gameObject.SetActive(visible && (i % 3 != 2 || pulse > 0.7f));
            _sparkles[i].color = new Color(1f, 0.96f, 0.55f, pulse);
            float scale = 0.7f + pulse * 0.5f;
            _sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (_stage == WashStage.Done && _completeCard.activeSelf)
        {
            for (int i = 0; i < _completeStars.Length; i++)
            {
                float s = 1f + Mathf.Sin(time * 4f + i) * 0.09f;
                _completeStars[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        if (_transitioning || _surface == null) return;

        _elapsed += Time.unscaledDeltaTime;
        HandlePointers();

        float progress = _surface.Progress(_stage);
        _progressFill.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, progress), 1f, 1f);
        if (progress >= FinishAt) StartCoroutine(TransitionStage());
    }

    // ---------------------------------------------------------------- UI build

    private void BuildUi()
    {
        var canvasObject = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1200f, 800f);
        scaler.matchWidthOrHeight = 0.5f;
        _root = canvasObject.GetComponent<RectTransform>();

        _background = UiFactory.StretchImage(_root, "Background", Color.white);
        SetBackground(0);

        _homePanel = Panel("HomeScreen");
        _levelsPanel = Panel("LevelScreen");
        _playPanel = Panel("PlayScreen");
        BuildHome();
        BuildLevels();
        BuildPlay();
    }

    private GameObject Panel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private void BuildHome()
    {
        UiFactory.Label(_homePanel.transform, "Title", "Little Car Wash", 82, Ink, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 100f), new Vector2(0f, -85f));
        UiFactory.Label(_homePanel.transform, "Subtitle", "Wash a dirty car and make it shine!", 32, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(900f, 50f), new Vector2(0f, -155f));

        Color32[] previewPixels = CarArt.Render(CarKind.Sedan,
            new Color32(72, 156, 255, 255), new Color32(255, 208, 74, 255), 0.94f, CarW, CarH);
        var previewTexture = CarArt.MakeTexture(previewPixels, CarW, CarH, "PreviewCar");
        var previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, CarW, CarH), new Vector2(0.5f, 0.5f), 100f);
        _preview = UiFactory.CenteredImage(_homePanel.transform, "PreviewCar", Color.white,
            new Vector2(560f, 374f), new Vector2(0f, 10f), previewSprite);

        UiFactory.Button(_homePanel.transform, "PlayButton", "PLAY!", Green, 46,
            new Vector2(360f, 92f), new Vector2(0f, -215f), delegate { StartLevel(0); });
        UiFactory.Button(_homePanel.transform, "LevelsButton", "All cars", Blue, 36,
            new Vector2(360f, 82f), new Vector2(0f, -310f), ShowLevels);

        var sound = UiFactory.Button(_homePanel.transform, "SoundButton", "", Pink, 24,
            new Vector2(150f, 62f), new Vector2(-95f, -62f), ToggleSound);
        ((RectTransform)sound.transform).anchorMin = new Vector2(1f, 1f);
        ((RectTransform)sound.transform).anchorMax = new Vector2(1f, 1f);
        _soundLabels.Add(sound.GetComponentInChildren<Text>());

        UiFactory.Label(_homePanel.transform, "Hint", "Swipe with your finger or mouse - no rush!", 22, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(1000f, 32f), new Vector2(0f, 30f));
        UpdateSoundLabels();
    }

    private void BuildLevels()
    {
        UiFactory.Label(_levelsPanel.transform, "Title", "Pick a car!", 64, Ink, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 90f), new Vector2(0f, -62f));

        var gridObject = new GameObject("Grid", typeof(RectTransform));
        gridObject.transform.SetParent(_levelsPanel.transform, false);
        _levelGrid = gridObject.GetComponent<RectTransform>();
        _levelGrid.anchorMin = new Vector2(0.5f, 0.5f);
        _levelGrid.anchorMax = new Vector2(0.5f, 0.5f);
        _levelGrid.sizeDelta = new Vector2(1100f, 620f);
        _levelGrid.anchoredPosition = new Vector2(0f, -8f);

        UiFactory.Button(_levelsPanel.transform, "BackButton", "Back", Blue, 34,
            new Vector2(250f, 68f), new Vector2(0f, -362f), ShowHome);
    }

    private void RebuildLevelGrid()
    {
        for (int i = _levelGrid.childCount - 1; i >= 0; i--)
            Destroy(_levelGrid.GetChild(i).gameObject);

        for (int i = 0; i < GameData.LevelCount; i++)
        {
            int levelIndex = i;
            int column = i % 5;
            int row = i / 5;
            var position = new Vector2(-428f + column * 214f, 240f - row * 102f);
            var tint = Color.Lerp(Blue, Pink, (i % 6) / 5f);
            var button = UiFactory.Button(_levelGrid, "Level" + (i + 1), "", tint, 18,
                new Vector2(196f, 90f), position, delegate { StartLevel(levelIndex); });

            var pixels = CarArt.Render(GameData.Levels[i].Kind, GameData.Levels[i].Body,
                GameData.Levels[i].Accent, GameData.Levels[i].Size, 128, 86);
            var texture = CarArt.MakeTexture(pixels, 128, 86, "Thumb" + i);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 128, 86), new Vector2(0.5f, 0.5f), 100f);
            UiFactory.CenteredImage(button.transform, "Car", Color.white, new Vector2(132f, 76f),
                new Vector2(0f, 8f), sprite);
            UiFactory.Label(button.transform, "Name", GameData.Levels[i].Name, 16, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(190f, 26f), new Vector2(0f, 6f), false);

            int earned = PlayerPrefs.GetInt(GameData.StarsKey(i), 0);
            for (int s = 0; s < 3; s++)
            {
                var star = UiFactory.Image(button.transform, "Star" + s,
                    s < earned ? new Color32(255, 224, 92, 255) : new Color32(255, 255, 255, 85),
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(18f, 18f),
                    new Vector2(-14f - s * 20f, -12f), UiFactory.StarSprite);
                star.rectTransform.anchorMin = new Vector2(1f, 1f);
                star.rectTransform.anchorMax = new Vector2(1f, 1f);
            }
        }
    }

    private void BuildPlay()
    {
        UiFactory.Image(_playPanel.transform, "TopBar", new Color(1f, 1f, 1f, 0.58f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 112f), new Vector2(0f, -56f),
            UiFactory.RoundSprite, true);

        _levelLabel = UiFactory.Label(_playPanel.transform, "LevelLabel", "Level 1", 30, Ink,
            TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(280f, 60f), new Vector2(168f, -56f));
        _instruction = UiFactory.Label(_playPanel.transform, "Instruction", "1. Make it wet!", 40, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(550f, 60f), new Vector2(0f, -56f));

        UiFactory.Image(_playPanel.transform, "ProgressBack", new Color32(58, 78, 104, 210),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(256f, 30f), new Vector2(-165f, -56f),
            UiFactory.RoundSprite, true);
        _progressFill = UiFactory.Image(_playPanel.transform, "ProgressFill", Green,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(248f, 22f), new Vector2(-293f, -56f),
            UiFactory.RoundSprite, true);
        _progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        _progressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);

        _stageDots = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            _stageDots[i] = UiFactory.CenteredImage(_playPanel.transform, "Dot" + i,
                new Color32(255, 255, 255, 150), new Vector2(20f, 20f),
                new Vector2(-48f + i * 48f, 268f), UiFactory.RoundSprite);
        }

        var wash = new GameObject("WashArea", typeof(RectTransform));
        wash.transform.SetParent(_playPanel.transform, false);
        _washRect = wash.GetComponent<RectTransform>();
        _washRect.anchorMin = new Vector2(0.5f, 0.5f);
        _washRect.anchorMax = new Vector2(0.5f, 0.5f);
        _washRect.sizeDelta = new Vector2(840f, 560f);
        _washRect.anchoredPosition = new Vector2(0f, -28f);

        _carImage = UiFactory.StretchImage(_washRect, "Car", Color.white);
        _overlayImage = UiFactory.StretchImage(_washRect, "WashLayer", Color.white);

        _sparkles = new Image[6];
        for (int i = 0; i < _sparkles.Length; i++)
        {
            _sparkles[i] = UiFactory.CenteredImage(_washRect, "Sparkle" + i, Yellow,
                new Vector2(38f, 38f), new Vector2(-300f + (i % 3) * 290f, -140f + (i / 3) * 210f),
                UiFactory.StarSprite);
            _sparkles[i].gameObject.SetActive(false);
        }

        _hint = new GameObject("FingerHint", typeof(RectTransform));
        _hint.transform.SetParent(_washRect, false);
        var hintRect = _hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(70f, 90f);
        UiFactory.CenteredImage(_hint.transform, "Finger", new Color32(255, 255, 255, 235),
            new Vector2(26f, 62f), new Vector2(0f, 8f), UiFactory.RoundSprite);
        UiFactory.CenteredImage(_hint.transform, "Tip", new Color32(255, 255, 255, 235),
            new Vector2(18f, 34f), new Vector2(0f, 46f), UiFactory.RoundSprite);

        UiFactory.Image(_playPanel.transform, "BottomBar", new Color(1f, 1f, 1f, 0.58f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 98f), new Vector2(0f, 49f),
            UiFactory.RoundSprite, true);
        UiFactory.Button(_playPanel.transform, "HomeButton", "Home", Blue, 30,
            new Vector2(180f, 64f), new Vector2(-470f, 49f), ShowHome);
        UiFactory.Button(_playPanel.transform, "ReplayButton", "Again", Pink, 30,
            new Vector2(180f, 64f), new Vector2(0f, 49f), delegate { StartLevel(_level); });
        var sound = UiFactory.Button(_playPanel.transform, "SoundButton", "", Pink, 22,
            new Vector2(180f, 64f), new Vector2(470f, 49f), ToggleSound);
        _soundLabels.Add(sound.GetComponentInChildren<Text>());

        _flash = UiFactory.StretchImage(_playPanel.transform, "Flash", new Color(1f, 1f, 1f, 0f));

        _completeCard = new GameObject("CompleteCard", typeof(RectTransform));
        _completeCard.transform.SetParent(_playPanel.transform, false);
        var cardRect = _completeCard.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.sizeDelta = Vector2.zero;
        cardRect.anchoredPosition = Vector2.zero;
        var dim = UiFactory.StretchImage(_completeCard.transform, "Dim", new Color(0f, 0f, 0f, 0.35f));
        dim.raycastTarget = true; // blocks taps from reaching the buttons behind the card
        UiFactory.CenteredImage(_completeCard.transform, "Card", Pale, new Vector2(640f, 430f),
            Vector2.zero, UiFactory.RoundSprite, true);
        _completeTitle = UiFactory.CenteredLabel(_completeCard.transform, "Title", "Sparkling clean!",
            58, Ink, new Vector2(700f, 80f), new Vector2(0f, 130f));
        _completeSubtitle = UiFactory.CenteredLabel(_completeCard.transform, "Subtitle", "",
            28, Ink, new Vector2(600f, 50f), new Vector2(0f, 72f));
        _completeStars = new Image[3];
        for (int i = 0; i < 3; i++)
            _completeStars[i] = UiFactory.CenteredImage(_completeCard.transform, "Star" + i, Yellow,
                new Vector2(80f, 80f), new Vector2(-105f + i * 105f, 0f), UiFactory.StarSprite);
        _nextButton = UiFactory.Button(_completeCard.transform, "Next", "Next car", Green, 36,
            new Vector2(230f, 76f), new Vector2(128f, -105f), delegate { StartLevel(_level + 1); });
        UiFactory.Button(_completeCard.transform, "Again", "Again", Blue, 32,
            new Vector2(180f, 70f), new Vector2(-120f, -105f), delegate { StartLevel(_level); });
        UiFactory.Button(_completeCard.transform, "Levels", "All cars", Pink, 28,
            new Vector2(180f, 62f), new Vector2(0f, -180f), ShowLevels);
        _completeCard.SetActive(false);
        UpdateSoundLabels();
    }

    // ---------------------------------------------------------------- screens

    private void ShowHome()
    {
        _view = View.Home;
        _homePanel.SetActive(true);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(false);
        SetBackground(0);
    }

    private void ShowLevels()
    {
        _view = View.Levels;
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(true);
        _playPanel.SetActive(false);
        RebuildLevelGrid();
        SetBackground(2);
    }

    private void StartLevel(int index)
    {
        if (index < 0 || index >= GameData.LevelCount) { ShowLevels(); return; }
        StopAllCoroutines();
        LevelDef level = GameData.Levels[index];
        _level = index;

        if (_carSprite != null) Destroy(_carSprite);
        if (_carTexture != null) Destroy(_carTexture);
        if (_overlaySprite != null) Destroy(_overlaySprite);
        if (_overlayTexture != null) Destroy(_overlayTexture);

        Color32[] pixels = CarArt.Render(level.Kind, level.Body, level.Accent, level.Size, CarW, CarH);
        _carTexture = CarArt.MakeTexture(pixels, CarW, CarH, "Car" + index);
        _carSprite = Sprite.Create(_carTexture, new Rect(0, 0, CarW, CarH), new Vector2(0.5f, 0.5f), 100f);
        _carImage.sprite = _carSprite;

        _surface = new WashSurface(pixels, CarW, CarH, level.Dirt, index + 1);
        _overlayTexture = _surface.Texture;
        _overlaySprite = Sprite.Create(_overlayTexture, new Rect(0, 0, WashSurface.W, WashSurface.H),
            new Vector2(0.5f, 0.5f), 100f);
        _overlayImage.sprite = _overlaySprite;

        SetBackground(level.Theme);
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(true);
        _completeCard.SetActive(false);
        _view = View.Play;

        _stage = WashStage.Wet;
        _transitioning = false;
        _elapsed = 0f;
        _strokes = 0;
        _flashAlpha = 0f;
        _lastPoints.Clear();
        _instruction.text = "1. Make it wet!";
        _levelLabel.text = "Level " + (index + 1) + " - " + level.Name;
        _progressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);
        ShowHint();
        UpdateStageDots();
        Sfx.Play("pop");
    }

    private void SetBackground(int theme)
    {
        int t = Mathf.Abs(theme) % _backgroundSprites.Length;
        if (_backgroundSprites[t] == null)
        {
            var texture = GameArt.Background(t);
            _backgroundSprites[t] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        _background.sprite = _backgroundSprites[t];
    }

    private void ShowHint()
    {
        _hint.transform.localPosition = new Vector3(-170f, -30f, 0f);
        _hint.SetActive(true);
    }

    private void UpdateStageDots()
    {
        for (int i = 0; i < _stageDots.Length; i++)
        {
            bool active = i == (int)_stage;
            _stageDots[i].color = active ? Green : new Color32(255, 255, 255, 150);
            float s = active ? 1.25f : 0.9f;
            _stageDots[i].rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void ToggleSound()
    {
        Sfx.Enabled = !Sfx.Enabled;
        UpdateSoundLabels();
        Sfx.Play("click");
    }

    private void UpdateSoundLabels()
    {
        string label = Sfx.Enabled ? "Sound: On" : "Sound: Off";
        for (int i = 0; i < _soundLabels.Count; i++)
            if (_soundLabels[i] != null) _soundLabels[i].text = label;
    }

    // ---------------------------------------------------------------- washing

    private void HandlePointers()
    {
        var seen = new HashSet<int>();
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                AddPointer(touch.fingerId, touch.position, seen);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            AddPointer(-1, Input.mousePosition, seen);
        }

        var stale = new List<int>();
        foreach (var pair in _lastPoints)
            if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
        for (int i = 0; i < stale.Count; i++) _lastPoints.Remove(stale[i]);
    }

    private void AddPointer(int id, Vector2 screenPosition, HashSet<int> seen)
    {
        seen.Add(id);
        Vector2 uv;
        if (!TryScreenToUv(screenPosition, out uv))
        {
            _lastPoints.Remove(id);
            return;
        }

        float radius = GameData.Levels[_level].Size < 0.8f ? 0.098f : 0.084f;
        Vector2 previous;
        if (_lastPoints.TryGetValue(id, out previous))
        {
            _surface.PaintLine(previous.x, previous.y, uv.x, uv.y, radius, _stage);
        }
        else
        {
            _surface.Paint(uv.x, uv.y, radius, _stage);
            _surface.Upload();
            _strokes++;
            _hint.SetActive(false);
            Sfx.Play(_stage == WashStage.Wet ? "splash" : (_stage == WashStage.Foam ? "foam" : "rinse"));
        }
        _lastPoints[id] = uv;
    }

    private bool TryScreenToUv(Vector2 screenPosition, out Vector2 uv)
    {
        uv = Vector2.zero;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_washRect, screenPosition, null, out local))
            return false;
        Rect r = _washRect.rect;
        float u = local.x / r.width + 0.5f;
        float v = local.y / r.height + 0.5f;
        if (u < -0.08f || u > 1.08f || v < -0.08f || v > 1.08f) return false;
        uv = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return true;
    }

    private IEnumerator TransitionStage()
    {
        _transitioning = true;
        _surface.FillCurrent(_stage);
        _progressFill.rectTransform.localScale = new Vector3(1f, 1f, 1f);
        _flashAlpha = 0.5f;
        Sfx.Play("star");
        _instruction.text = "Great job!";
        yield return new WaitForSecondsRealtime(1.05f);

        if (_stage == WashStage.Wet)
        {
            _stage = WashStage.Foam;
            _instruction.text = "2. Add foam!";
            Sfx.Play("foam");
        }
        else if (_stage == WashStage.Foam)
        {
            _stage = WashStage.Rinse;
            _instruction.text = "3. Rinse it off!";
            Sfx.Play("rinse");
        }
        else
        {
            CompleteLevel();
            yield break;
        }

        _lastPoints.Clear();
        _progressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);
        ShowHint();
        UpdateStageDots();
        _transitioning = false;
    }

    private void CompleteLevel()
    {
        _stage = WashStage.Done;
        _instruction.text = "So shiny!";
        _transitioning = true;
        UpdateStageDots();
        Sfx.Play("win");

        int stars = 1;
        if (_elapsed < 95f && _strokes < 45) stars = 2;
        if (_elapsed < 65f && _strokes < 26) stars = 3;
        int previous = PlayerPrefs.GetInt(GameData.StarsKey(_level), 0);
        if (stars > previous)
        {
            PlayerPrefs.SetInt(GameData.StarsKey(_level), stars);
            PlayerPrefs.Save();
        }

        _completeTitle.text = stars == 3 ? "Wow! Sparkling!" : "Sparkling clean!";
        _completeSubtitle.text = "Level " + (_level + 1) + " - " + GameData.Levels[_level].Name;
        for (int i = 0; i < _completeStars.Length; i++)
            _completeStars[i].color = i < stars ? Yellow : new Color32(255, 255, 255, 70);
        _nextButton.gameObject.SetActive(_level < GameData.LevelCount - 1);
        _completeCard.SetActive(true);
    }
}
