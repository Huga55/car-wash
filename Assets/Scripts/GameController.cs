using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Screen orchestrator. Game rules live in ProgressManager/WashSurface,
/// car motion in CarViewController, drivers in AnimalActor and input in
/// WashingController; this class only wires them together.</summary>
public sealed class GameController : MonoBehaviour
{
    private enum ScreenId { Home, Levels, Play }

    [SerializeField] private Vector2 uiReferenceResolution = new Vector2(1200f, 800f);
    [SerializeField] private float stageTransitionSeconds = 0.9f;
    [SerializeField] private float rewardSparkleSeconds = 0.45f;
    [SerializeField] private float driveAwaySeconds = 0.55f;

    private static readonly Color Ink = new Color32(38, 58, 84, 255);
    private static readonly Color Green = new Color32(76, 202, 122, 255);
    private static readonly Color Blue = new Color32(86, 166, 246, 255);
    private static readonly Color Yellow = new Color32(255, 208, 74, 255);
    private static readonly Color Pink = new Color32(246, 128, 168, 255);
    private static readonly Color Pale = new Color32(255, 255, 255, 232);

    private ScreenId _screen;
    private RectTransform _canvasRoot;
    private RectTransform _safeRoot;
    private Image _background;
    private readonly Sprite[] _backgroundSprites = new Sprite[5];
    private int _screenWidth;
    private int _screenHeight;

    private GameObject _homePanel;
    private GameObject _levelsPanel;
    private GameObject _playPanel;
    private GameObject _pausePanel;
    private GameObject _exitPanel;
    private GameObject _completeCard;

    private Image _preview;
    private Image _sun;
    private readonly Image[] _clouds = new Image[3];
    private readonly List<Text> _soundLabels = new List<Text>();

    private Image _cardPanel;
    private Image _cardCar;
    private Image _cardLock;
    private readonly Image[] _cardStars = new Image[3];
    private Text _cardName;
    private Text _progressLabel;
    private Text _chooseLabel;
    private Button _chooseButton;
    private Texture2D _cardTexture;
    private Sprite _cardSprite;
    private int _selected;

    private Text _levelLabel;
    private Text _instruction;
    private Text _toolLabel;
    private Text _completeTitle;
    private Text _rewardLabel;
    private Text _nextLabel;
    private Image _toolIcon;
    private Image _progressFill;
    private Image _flash;
    private readonly Image[] _stageDots = new Image[5];
    private readonly Image[] _zoneDots = new Image[WashSurface.ZoneCount];
    private readonly Image[] _effects = new Image[8];
    private readonly float[] _effectLife = new float[8];
    private int _effectCursor;
    private float _displayProgress;
    private readonly Image[] _sparkles = new Image[6];
    private readonly Image[] _completeStars = new Image[3];
    private readonly Image[] _confetti = new Image[10];
    private RectTransform _washRect;
    private RectTransform _hintRect;
    private GameObject _hint;

    private CarViewController _car;
    private AnimalActor _animal;
    private WashingController _washing;
    private WashSurface _surface;
    private Texture2D _overlayTexture;

    private LevelDef _levelDef;
    private WashStage[] _stages;
    private WashStage _stage;
    private int _level;
    private int _stageIndex;
    private bool _transitioning;
    private bool _rewarding;
    private bool _paused;
    private bool _exitOpen;
    private float _flashAlpha;

    private void Awake()
    {
        BuildUi();
        ApplySafeArea();
        ShowHome();
    }

    private void Update()
    {
        if (_screenWidth != Screen.width || _screenHeight != Screen.height) ApplySafeArea();

        float time = Time.unscaledTime;
        AnimateMenu(time);
        AnimateFlash();
        HandleSystemKey();
        AnimateConfetti(time);

        if (_screen != ScreenId.Play || _paused || _exitOpen || _transitioning ||
            _rewarding || _completeCard.activeSelf)
            return;

        AnimateHint(time);
        AnimateSparkles(time);
        AnimateEffects();
        if (_car != null) _car.UpdateIdle(time);

        _washing.Update(_stage, true);
        float progress = _washing.Progress(_stage);
        _displayProgress = Mathf.Lerp(_displayProgress, progress, Mathf.Clamp01(Time.unscaledDeltaTime * 7f));
        _progressFill.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, _displayProgress), 1f, 1f);
        UpdateZoneDots();
        if (_washing.MinZoneProgress(_stage) >= WashSurface.ZoneFinish) StartCoroutine(AdvanceStage());
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        var canvasObject = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        _canvasRoot = canvasObject.GetComponent<RectTransform>();

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = uiReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        _background = UiFactory.StretchImage(_canvasRoot, "Background", Color.white);
        SetBackground(0);

        // Fixed design area, scaled to fit inside the device safe area. Every
        // screen is authored in these coordinates, so nothing can be clipped.
        var safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(_canvasRoot, false);
        _safeRoot = safe.GetComponent<RectTransform>();
        _safeRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _safeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _safeRoot.pivot = new Vector2(0.5f, 0.5f);
        _safeRoot.sizeDelta = uiReferenceResolution;
        _safeRoot.anchoredPosition = Vector2.zero;

        _homePanel = Panel("HomeScreen");
        _levelsPanel = Panel("LevelScreen");
        _playPanel = Panel("PlayScreen");
        BuildHome();
        BuildLevels();
        BuildPlay();
        BuildPause();
        BuildExitDialog();
        BuildCompleteCard();
    }

    private GameObject Panel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_safeRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private void BuildHome()
    {
        UiFactory.Label(_homePanel.transform, "Logo", "Little Car Wash", 78, Ink, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 100f), new Vector2(0f, -68f));
        UiFactory.Label(_homePanel.transform, "Tagline", "Весёлая мойка для машинок", 30, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(800f, 50f), new Vector2(0f, -132f));

        _sun = UiFactory.CenteredImage(_homePanel.transform, "Sun", Color.white,
            new Vector2(150f, 150f), new Vector2(430f, 245f), GameArt.SunSprite);
        _clouds[0] = UiFactory.CenteredImage(_homePanel.transform, "Cloud1", Color.white,
            new Vector2(210f, 105f), new Vector2(-420f, 210f), GameArt.CloudSprite);
        _clouds[1] = UiFactory.CenteredImage(_homePanel.transform, "Cloud2", Color.white,
            new Vector2(160f, 80f), new Vector2(310f, 150f), GameArt.CloudSprite);
        _clouds[2] = UiFactory.CenteredImage(_homePanel.transform, "Cloud3", Color.white,
            new Vector2(130f, 66f), new Vector2(-100f, 260f), GameArt.CloudSprite);

        Color32[] previewPixels = CarArt.Render(CarKind.Sedan,
            new Color32(72, 156, 255, 255), new Color32(255, 208, 74, 255), 0.94f, 384, 256);
        var previewTexture = CarArt.MakeTexture(previewPixels, 384, 256, "MenuCar");
        var previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, 384, 256),
            new Vector2(0.5f, 0.5f), 100f);
        _preview = UiFactory.CenteredImage(_homePanel.transform, "MenuCar", Color.white,
            new Vector2(620f, 414f), new Vector2(0f, 35f), previewSprite);

        UiFactory.Button(_homePanel.transform, "PlayButton", Loc.T(Loc.Play), Green, 46,
            new Vector2(390f, 94f), new Vector2(0f, -198f),
            delegate { StartLevel(ProgressManager.UnlockedLevel); });
        UiFactory.Button(_homePanel.transform, "CarsButton", Loc.T(Loc.Cars), Blue, 38,
            new Vector2(360f, 82f), new Vector2(0f, -300f), ShowLevels);

        AddSoundButton(_homePanel.transform, new Vector2(1f, 1f), new Vector2(-135f, -62f),
            new Vector2(190f, 66f));
        UiFactory.Button(_homePanel.transform, "ExitButton", Loc.T(Loc.Exit), Pink, 30,
            new Vector2(190f, 66f), new Vector2(0f, -364f), ShowExitDialog);

        UpdateSoundLabels();
    }

    private void BuildLevels()
    {
        UiFactory.Label(_levelsPanel.transform, "Title", Loc.T(Loc.PickCar), 64, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(760f, 90f), new Vector2(0f, -62f));
        _progressLabel = UiFactory.Label(_levelsPanel.transform, "Progress", "", 28, Ink,
            TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(360f, 54f), new Vector2(-190f, -66f));

        var card = UiFactory.CenteredImage(_levelsPanel.transform, "Card", Pale,
            new Vector2(760f, 430f), new Vector2(0f, 10f), UiFactory.RoundSprite, true);
        _cardPanel = card;

        _cardCar = UiFactory.CenteredImage(card.transform, "Car", Color.white,
            new Vector2(640f, 330f), new Vector2(0f, 34f));
        _cardName = UiFactory.CenteredLabel(card.transform, "Name", "", 40, Ink,
            new Vector2(660f, 64f), new Vector2(0f, -130f));
        for (int i = 0; i < _cardStars.Length; i++)
            _cardStars[i] = UiFactory.CenteredImage(card.transform, "Star" + i, Yellow,
                new Vector2(58f, 58f), new Vector2(-70f + i * 70f, -182f), UiFactory.StarSprite);
        _cardLock = UiFactory.CenteredImage(card.transform, "Lock", Color.white,
            new Vector2(130f, 130f), new Vector2(0f, 20f), UiFactory.LockSprite);
        _cardLock.gameObject.SetActive(false);

        UiFactory.Button(_levelsPanel.transform, "Prev", "<", Blue, 46,
            new Vector2(96f, 96f), new Vector2(-470f, 10f), delegate { MoveSelection(-1); });
        UiFactory.Button(_levelsPanel.transform, "Next", ">", Blue, 46,
            new Vector2(96f, 96f), new Vector2(470f, 10f), delegate { MoveSelection(1); });

        _chooseButton = UiFactory.Button(_levelsPanel.transform, "Choose", Loc.T(Loc.Choose), Green, 36,
            new Vector2(330f, 82f), new Vector2(0f, -258f), StartSelectedLevel);
        _chooseLabel = _chooseButton.GetComponentInChildren<Text>();
        UiFactory.Button(_levelsPanel.transform, "Back", Loc.T(Loc.Back), Blue, 32,
            new Vector2(240f, 72f), new Vector2(0f, -344f), ShowHome);
        UpdateSoundLabels();
    }

    private void BuildPlay()
    {
        UiFactory.Image(_playPanel.transform, "TopBar", new Color(1f, 1f, 1f, 0.68f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 108f), new Vector2(0f, -54f),
            UiFactory.RoundSprite, true);

        _levelLabel = UiFactory.Label(_playPanel.transform, "LevelLabel", "", 28, Ink,
            TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(430f, 60f), new Vector2(230f, -54f));
        for (int i = 0; i < _stageDots.Length; i++)
            _stageDots[i] = UiFactory.CenteredImage(_playPanel.transform, "Stage" + i,
                new Color32(255, 255, 255, 160), new Vector2(24f, 24f),
                new Vector2(80f + i * 52f, -54f), UiFactory.SoftSprite);
        var pauseButton = UiFactory.Button(_playPanel.transform, "Pause", Loc.T(Loc.Pause), Pink, 26,
            new Vector2(150f, 64f), new Vector2(-110f, -54f), ShowPause);
        var pauseRect = pauseButton.GetComponent<RectTransform>();
        pauseRect.anchorMin = new Vector2(1f, 1f);
        pauseRect.anchorMax = new Vector2(1f, 1f);

        UiFactory.Image(_playPanel.transform, "ProgressBack", new Color32(58, 78, 104, 210),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(370f, 18f), new Vector2(0f, -122f),
            UiFactory.RoundSprite, true);
        _progressFill = UiFactory.Image(_playPanel.transform, "ProgressFill", Green,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(360f, 16f), new Vector2(-180f, -122f),
            UiFactory.SoftSprite, true);
        _progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        _progressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);

        var wash = new GameObject("WashArea", typeof(RectTransform));
        wash.transform.SetParent(_playPanel.transform, false);
        _washRect = wash.GetComponent<RectTransform>();
        _washRect.anchorMin = new Vector2(0.5f, 0.5f);
        _washRect.anchorMax = new Vector2(0.5f, 0.5f);
        _washRect.sizeDelta = new Vector2(860f, 520f);
        _washRect.anchoredPosition = new Vector2(0f, -6f);

        var carImage = UiFactory.StretchImage(_washRect, "Car", Color.white);
        var overlayImage = UiFactory.StretchImage(_washRect, "WashLayer", Color.white);

        // Small translucent markers show which parts of the car still need
        // attention. They are hidden automatically for zones with no car pixels.
        for (int i = 0; i < _zoneDots.Length; i++)
        {
            int column = i % 3;
            int row = i / 3;
            _zoneDots[i] = UiFactory.CenteredImage(_washRect, "Zone" + i,
                new Color(1f, 1f, 1f, 0.35f), new Vector2(20f, 20f),
                new Vector2(-260f + column * 260f, -110f + row * 220f), UiFactory.SoftSprite);
            _zoneDots[i].gameObject.SetActive(false);
        }

        // A tiny pool of transient images gives every stroke visible water,
        // foam, sponge or polish feedback without a particle system.
        for (int i = 0; i < _effects.Length; i++)
        {
            _effects[i] = UiFactory.CenteredImage(_washRect, "Effect" + i,
                new Color(1f, 1f, 1f, 0f), new Vector2(36f, 36f), Vector2.zero, UiFactory.SoftSprite);
            _effects[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < _sparkles.Length; i++)
        {
            _sparkles[i] = UiFactory.CenteredImage(_washRect, "Sparkle" + i, Yellow,
                new Vector2(44f, 44f), new Vector2(-320f + (i % 3) * 310f, -150f + (i / 3) * 210f),
                UiFactory.StarSprite);
            _sparkles[i].gameObject.SetActive(false);
        }

        _hint = new GameObject("FingerHint", typeof(RectTransform));
        _hint.transform.SetParent(_washRect, false);
        _hintRect = _hint.GetComponent<RectTransform>();
        _hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        _hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        _hintRect.sizeDelta = new Vector2(74f, 96f);
        UiFactory.CenteredImage(_hint.transform, "Palm", new Color32(255, 255, 255, 240),
            new Vector2(30f, 64f), new Vector2(0f, 6f), UiFactory.RoundSprite);
        UiFactory.CenteredImage(_hint.transform, "Tip", new Color32(255, 255, 255, 240),
            new Vector2(20f, 36f), new Vector2(0f, 48f), UiFactory.RoundSprite);

        Image animalPortrait = UiFactory.CenteredImage(_washRect, "Driver", Color.white,
            new Vector2(170f, 170f), new Vector2(330f, -70f));
        Text animalSpeech = UiFactory.CenteredLabel(_washRect, "DriverSpeech", "", 26, Ink,
            new Vector2(330f, 88f), new Vector2(330f, 52f));

        UiFactory.Image(_playPanel.transform, "BottomBar", new Color(1f, 1f, 1f, 0.68f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 112f), new Vector2(0f, 56f),
            UiFactory.RoundSprite, true);
        UiFactory.CenteredImage(_playPanel.transform, "ToolBack", Blue,
            new Vector2(104f, 104f), new Vector2(-448f, 56f), UiFactory.RoundSprite, true);
        _toolIcon = UiFactory.CenteredImage(_playPanel.transform, "ToolIcon", Color.white,
            new Vector2(82f, 82f), new Vector2(-448f, 56f), WashingTools.Icon(WashStage.Wet));
        _toolLabel = UiFactory.Label(_playPanel.transform, "ToolLabel", Loc.StageTool(WashStage.Wet), 28, Ink,
            TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(180f, 70f), new Vector2(-300f, 56f));
        _instruction = UiFactory.CenteredLabel(_playPanel.transform, "Instruction",
            Loc.StageInstruction(WashStage.Wet), 30, Ink, new Vector2(440f, 76f), new Vector2(-10f, 56f));
        AddSoundButton(_playPanel.transform, new Vector2(0.5f, 0f), new Vector2(310f, 56f),
            new Vector2(140f, 62f));
        UiFactory.Button(_playPanel.transform, "ToMenu", Loc.T(Loc.ToMenu), Blue, 26,
            new Vector2(160f, 62f), new Vector2(462f, 56f), ShowPause);

        _flash = UiFactory.StretchImage(_playPanel.transform, "Flash", new Color(1f, 1f, 1f, 0f));

        _car = new CarViewController(carImage, overlayImage);
        _washing = new WashingController(_washRect);
        _washing.StrokeStarted = OnStrokeStarted;
        _washing.StrokeMoved = OnStrokeMoved;
        _animal = new AnimalActor(animalPortrait, animalSpeech, new Vector2(330f, -70f));
        UpdateSoundLabels();
    }

    private void BuildPause()
    {
        _pausePanel = Panel("PauseDialog");
        var dim = UiFactory.StretchImage(_pausePanel.transform, "Dim", new Color(0f, 0f, 0f, 0.42f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_pausePanel.transform, "Card", Pale, new Vector2(620f, 500f),
            Vector2.zero, UiFactory.RoundSprite, true);
        UiFactory.CenteredLabel(_pausePanel.transform, "Title", Loc.T(Loc.Pause), 68, Ink,
            new Vector2(560f, 90f), new Vector2(0f, 165f));
        UiFactory.Button(_pausePanel.transform, "Resume", Loc.T(Loc.Resume), Green, 34,
            new Vector2(380f, 82f), new Vector2(0f, 62f), Resume);
        UiFactory.Button(_pausePanel.transform, "Restart", Loc.T(Loc.Restart), Blue, 32,
            new Vector2(380f, 76f), new Vector2(0f, -32f), RestartLevel);
        UiFactory.Button(_pausePanel.transform, "Menu", Loc.T(Loc.ToMenu), Pink, 32,
            new Vector2(380f, 76f), new Vector2(0f, -126f), ShowHome);
        _pausePanel.SetActive(false);
    }

    private void BuildExitDialog()
    {
        _exitPanel = Panel("ExitDialog");
        var dim = UiFactory.StretchImage(_exitPanel.transform, "Dim", new Color(0f, 0f, 0f, 0.42f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_exitPanel.transform, "Card", Pale, new Vector2(660f, 380f),
            Vector2.zero, UiFactory.RoundSprite, true);
        UiFactory.CenteredLabel(_exitPanel.transform, "Title", Loc.T(Loc.ExitTitle), 46, Ink,
            new Vector2(600f, 100f), new Vector2(0f, 92f));
        UiFactory.Button(_exitPanel.transform, "Stay", Loc.T(Loc.Stay), Green, 34,
            new Vector2(250f, 78f), new Vector2(-150f, -70f), CloseExitDialog);
        UiFactory.Button(_exitPanel.transform, "Quit", Loc.T(Loc.Quit), Pink, 34,
            new Vector2(250f, 78f), new Vector2(150f, -70f), QuitGame);
        _exitPanel.SetActive(false);
    }

    private void BuildCompleteCard()
    {
        _completeCard = new GameObject("CompleteCard", typeof(RectTransform));
        _completeCard.transform.SetParent(_playPanel.transform, false);
        var rect = _completeCard.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var dim = UiFactory.StretchImage(_completeCard.transform, "Dim", new Color(0f, 0f, 0f, 0.42f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_completeCard.transform, "Card", Pale, new Vector2(780f, 540f),
            Vector2.zero, UiFactory.RoundSprite, true);

        for (int i = 0; i < _confetti.Length; i++)
            _confetti[i] = UiFactory.CenteredImage(_completeCard.transform, "Confetti" + i,
                i % 2 == 0 ? Yellow : Pink, new Vector2(34f, 34f), Vector2.zero, UiFactory.StarSprite);

        _completeTitle = UiFactory.CenteredLabel(_completeCard.transform, "Title", Loc.T(Loc.LevelComplete),
            58, Ink, new Vector2(720f, 82f), new Vector2(0f, 202f));
        UiFactory.CenteredImage(_completeCard.transform, "CleanCarBack", new Color(1f, 1f, 1f, 0.7f),
            new Vector2(600f, 220f), new Vector2(0f, 92f), UiFactory.RoundSprite, true);
        _cleanCar = UiFactory.CenteredImage(_completeCard.transform, "CleanCar", Color.white,
            new Vector2(560f, 210f), new Vector2(0f, 92f));

        for (int i = 0; i < _completeStars.Length; i++)
            _completeStars[i] = UiFactory.CenteredImage(_completeCard.transform, "Star" + i, Yellow,
                new Vector2(78f, 78f), new Vector2(-110f + i * 110f, -28f), UiFactory.StarSprite);

        _rewardLabel = UiFactory.CenteredLabel(_completeCard.transform, "Reward", "", 26, Ink,
            new Vector2(700f, 60f), new Vector2(0f, -102f));
        _nextButton = UiFactory.Button(_completeCard.transform, "Next", Loc.T(Loc.NextCar), Green, 34,
            new Vector2(320f, 80f), new Vector2(168f, -188f), GoToNext);
        _nextLabel = _nextButton.GetComponentInChildren<Text>();
        UiFactory.Button(_completeCard.transform, "Menu", Loc.T(Loc.ToMenu), Blue, 32,
            new Vector2(270f, 76f), new Vector2(-180f, -188f), ShowHome);
        _completeCard.SetActive(false);
    }

    private Image _cleanCar;
    private Button _nextButton;

    private void AddSoundButton(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var button = UiFactory.Button(parent, "SoundButton", "", Pink, 24, size, position, ToggleSound);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        _soundLabels.Add(button.GetComponentInChildren<Text>());
    }

    private void ApplySafeArea()
    {
        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        UiFit fit = UiLayout.Compute(uiReferenceResolution, _screenWidth, _screenHeight, Screen.safeArea);
        _safeRoot.localScale = new Vector3(fit.Scale, fit.Scale, 1f);
        _safeRoot.anchoredPosition = fit.Offset;
    }

    // -------------------------------------------------------------- screens

    private void ShowHome()
    {
        StopAllCoroutines();
        ReleaseLevel();
        _screen = ScreenId.Home;
        _homePanel.SetActive(true);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _exitPanel.SetActive(false);
        _paused = false;
        _exitOpen = false;
        Time.timeScale = 1f;
        SetBackground(0);
    }

    private void ShowLevels()
    {
        StopAllCoroutines();
        ReleaseLevel();
        _screen = ScreenId.Levels;
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(true);
        _playPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _exitPanel.SetActive(false);
        _paused = false;
        _exitOpen = false;
        Time.timeScale = 1f;
        _selected = ProgressManager.UnlockedLevel;
        UpdateLevelCard();
        SetBackground(2);
    }

    private void StartLevel(int index)
    {
        if (!ProgressManager.IsUnlocked(index))
        {
            AudioManager.Play("click");
            return;
        }

        StopAllCoroutines();
        ReleaseLevel();
        ReleaseCard();

        _level = index;
        _levelDef = GameData.Levels[index];
        _screen = ScreenId.Play;
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(true);
        _pausePanel.SetActive(false);
        _exitPanel.SetActive(false);
        _paused = false;
        _exitOpen = false;
        Time.timeScale = 1f;

        Color32[] pixels = CarArt.Render(_levelDef.Kind, _levelDef.Body, _levelDef.Accent,
            _levelDef.Size, 384, 256);
        _surface = new WashSurface(pixels, 384, 256, _levelDef.Dirt, index + 1, _levelDef.DirtType);
        _overlayTexture = _surface.Texture;
        _car.Load(pixels, _levelDef, _surface);
        _washing.Begin(_surface, _levelDef.Size);

        _stages = GameData.StagesFor(index);
        _stageIndex = 0;
        _stage = _stages[0];
        _transitioning = false;
        _rewarding = false;
        _flashAlpha = 0f;
        _completeCard.SetActive(false);
        _levelLabel.text = Loc.T(Loc.Level) + " " + (index + 1) + "  •  " + _levelDef.Name;
        UpdateStageUi();
        ShowHint();
        SetBackground(_levelDef.Theme);
        AudioManager.Play("pop");
    }

    private void StartSelectedLevel()
    {
        if (ProgressManager.IsUnlocked(_selected)) StartLevel(_selected);
        else AudioManager.Play("click");
    }

    private void MoveSelection(int delta)
    {
        _selected = Mathf.Clamp(_selected + delta, 0, GameData.LevelCount - 1);
        UpdateLevelCard();
        AudioManager.Play("click");
    }

    private void UpdateLevelCard()
    {
        ReleaseCard();
        LevelDef level = GameData.Levels[_selected];
        bool unlocked = ProgressManager.IsUnlocked(_selected);
        bool done = ProgressManager.IsCompleted(_selected);

        Color32[] pixels = CarArt.Render(level.Kind, level.Body, level.Accent, level.Size, 384, 256);
        _cardTexture = CarArt.MakeTexture(pixels, 384, 256, "CardCar" + _selected);
        _cardSprite = Sprite.Create(_cardTexture, new Rect(0, 0, 384, 256), new Vector2(0.5f, 0.5f), 100f);
        _cardCar.sprite = _cardSprite;
        _cardName.text = level.Name;
        _cardLock.gameObject.SetActive(!unlocked);
        _cardPanel.color = unlocked ? (done ? new Color(0.82f, 0.95f, 0.84f, 1f) : Pale) :
            new Color(0.78f, 0.80f, 0.84f, 1f);

        int stars = ProgressManager.Stars(_selected);
        for (int i = 0; i < _cardStars.Length; i++)
            _cardStars[i].color = i < stars ? Yellow : new Color32(255, 255, 255, 110);

        _progressLabel.text = Loc.T(Loc.Progress) + " " + ProgressManager.CompletedCount() +
                              " / " + GameData.LevelCount;
        _chooseButton.interactable = unlocked;
        _chooseLabel.text = unlocked ? Loc.T(Loc.Choose) : Loc.T(Loc.Locked);
    }

    private void UpdateStageUi()
    {
        _instruction.text = Loc.StageInstruction(_stage);
        _toolLabel.text = Loc.StageTool(_stage);
        _toolIcon.sprite = WashingTools.Icon(_stage);
        _toolIcon.rectTransform.localScale = Vector3.one;
        _displayProgress = 0f;
        _progressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);
        for (int i = 0; i < _zoneDots.Length; i++)
            _zoneDots[i].gameObject.SetActive(_washing != null && _washing.HasZone(i));

        for (int i = 0; i < _stageDots.Length; i++)
        {
            bool used = i < _stages.Length;
            _stageDots[i].gameObject.SetActive(used);
            if (!used) continue;
            bool active = i == _stageIndex;
            bool passed = i < _stageIndex;
            _stageDots[i].color = active ? Green : (passed ? Yellow : new Color32(255, 255, 255, 160));
            float s = active ? 1.3f : 0.9f;
            _stageDots[i].rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void ShowHint()
    {
        _hint.SetActive(true);
        _hintRect.anchoredPosition = new Vector2(-210f, -60f);
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

    // --------------------------------------------------------------- animation

    private void AnimateMenu(float time)
    {
        if (_screen == ScreenId.Home && _preview != null)
        {
            _preview.rectTransform.anchoredPosition = new Vector2(0f, 35f + Mathf.Sin(time * 1.6f) * 9f);
            _preview.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.1f) * 2.5f);
            _sun.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.5f) * 8f);
            for (int i = 0; i < _clouds.Length; i++)
            {
                Vector2 p = _clouds[i].rectTransform.anchoredPosition;
                p.x += Mathf.Sin(time * (0.25f + i * 0.06f) + i) * 0.35f;
                if (p.x > 620f) p.x = -620f;
                if (p.x < -620f) p.x = 620f;
                _clouds[i].rectTransform.anchoredPosition = p;
            }
        }
    }

    private void AnimateHint(float time)
    {
        if (!_hint.activeSelf) return;
        _hintRect.anchoredPosition = new Vector2(-210f + Mathf.Cos(time * 2.2f) * 60f,
            -60f + Mathf.Sin(time * 4f) * 18f);
    }

    private void AnimateSparkles(float time)
    {
        bool visible = _stage == WashStage.Rinse || _stage == WashStage.Scrub || _stage == WashStage.Dry;
        for (int i = 0; i < _sparkles.Length; i++)
        {
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3.4f + i * 1.3f));
            _sparkles[i].gameObject.SetActive(visible && (i % 3 != 2 || pulse > 0.7f));
            _sparkles[i].color = new Color(1f, 0.96f, 0.55f, pulse);
            float scale = 0.7f + pulse * 0.5f;
            _sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void AnimateEffects()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _effects.Length; i++)
        {
            if (_effectLife[i] <= 0f) continue;
            _effectLife[i] -= dt;
            Image fx = _effects[i];
            if (_effectLife[i] <= 0f)
            {
                fx.gameObject.SetActive(false);
                continue;
            }
            float t = _effectLife[i] / 0.42f;
            Color c = fx.color;
            c.a = t * 0.85f;
            fx.color = c;
            Vector2 pos = fx.rectTransform.anchoredPosition;
            pos.y += dt * 26f;
            fx.rectTransform.anchoredPosition = pos;
            float s = 1.15f - t * 0.45f;
            fx.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void UpdateZoneDots()
    {
        for (int i = 0; i < _zoneDots.Length; i++)
        {
            Image dot = _zoneDots[i];
            if (!dot.gameObject.activeSelf) continue;
            float p = _washing.ZoneProgress(_stage, i);
            dot.color = p >= WashSurface.ZoneFinish ? new Color(0.35f, 0.85f, 0.5f, 0.85f) :
                p >= 0.4f ? new Color(1f, 0.85f, 0.35f, 0.75f) :
                new Color(1f, 0.55f, 0.45f, 0.7f);
            float s = p >= WashSurface.ZoneFinish ? 0.7f : 1f;
            dot.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void HideZoneDots()
    {
        for (int i = 0; i < _zoneDots.Length; i++) _zoneDots[i].gameObject.SetActive(false);
    }

    private void AnimateFlash()
    {
        if (_flashAlpha <= 0f) return;
        _flashAlpha = Mathf.Max(0f, _flashAlpha - Time.unscaledDeltaTime * 1.5f);
        _flash.color = new Color(1f, 1f, 1f, _flashAlpha);
    }

    private void AnimateConfetti(float time)
    {
        if (!_completeCard.activeSelf) return;
        for (int i = 0; i < _confetti.Length; i++)
        {
            float phase = time * 0.8f + i * 0.73f;
            float x = -330f + ((i * 97f + time * (35f + i * 3f)) % 660f);
            float y = 190f - ((phase * 110f) % 420f);
            _confetti[i].rectTransform.anchoredPosition = new Vector2(x, y);
            _confetti[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, phase * 180f);
        }
    }

    // -------------------------------------------------------------- input/flow

    private void HandleSystemKey()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (_exitOpen) { CloseExitDialog(); return; }
        if (_paused) { Resume(); return; }
        if (_screen == ScreenId.Play)
        {
            if (_completeCard.activeSelf) ShowHome();
            else ShowPause();
        }
        else if (_screen == ScreenId.Levels) ShowHome();
        else ShowExitDialog();
    }

    private void OnStrokeStarted(WashStage stage)
    {
        _hint.SetActive(false);
        AudioManager.Play(WashingTools.Sound(stage));
        _toolIcon.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
    }

    private void OnStrokeMoved(Vector2 uv, WashStage stage)
    {
        if (_car != null) _car.WashPulse(uv);
        _toolIcon.rectTransform.localScale = Vector3.Lerp(_toolIcon.rectTransform.localScale,
            Vector3.one, 0.35f);

        int i = _effectCursor;
        _effectCursor = (_effectCursor + 1) % _effects.Length;
        Image fx = _effects[i];
        fx.gameObject.SetActive(true);
        fx.rectTransform.anchoredPosition = new Vector2(
            (uv.x - 0.5f) * _washRect.sizeDelta.x, (uv.y - 0.5f) * _washRect.sizeDelta.y);
        float size = 34f;
        Color color;
        switch (stage)
        {
            case WashStage.Foam:
                size = 44f;
                color = new Color(1f, 1f, 1f, 0.85f);
                break;
            case WashStage.Scrub:
                size = 30f;
                color = new Color(1f, 0.95f, 0.7f, 0.7f);
                break;
            case WashStage.Rinse:
                size = 28f;
                color = new Color(0.55f, 0.82f, 1f, 0.8f);
                break;
            case WashStage.Dry:
                size = 40f;
                color = new Color(1f, 0.98f, 0.85f, 0.7f);
                break;
            default:
                size = 30f;
                color = new Color(0.6f, 0.82f, 1f, 0.8f);
                break;
        }
        fx.rectTransform.sizeDelta = new Vector2(size, size);
        fx.color = color;
        fx.rectTransform.localScale = Vector3.one;
        _effectLife[i] = 0.42f;
    }

    private IEnumerator AdvanceStage()
    {
        _transitioning = true;
        HideZoneDots();
        _washing.FillCurrent(_stage);
        _progressFill.rectTransform.localScale = new Vector3(1f, 1f, 1f);
        _flashAlpha = 0.42f;
        _instruction.text = Loc.T(Loc.Great);
        AudioManager.Play("star");
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, stageTransitionSeconds));

        _stageIndex++;
        if (_stageIndex >= _stages.Length)
        {
            StartCoroutine(RewardSequence());
            yield break;
        }

        _stage = _stages[_stageIndex];
        UpdateStageUi();
        ShowHint();
        _transitioning = false;
    }

    private IEnumerator RewardSequence()
    {
        _rewarding = true;
        _hint.SetActive(false);
        HideZoneDots();
        for (int i = 0; i < _sparkles.Length; i++) _sparkles[i].gameObject.SetActive(false);

        _washing.FinishAll();
        _car.HideOverlay();
        _flashAlpha = 0.6f;
        _instruction.text = Loc.T(Loc.Sparkle);
        AudioManager.Play("star");
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, rewardSparkleSeconds));

        _animal.Show(_level, _levelDef.Driver);
        AudioManager.Play("win");
        for (int i = 0; i < 22; i++)
        {
            _animal.Animate(Time.unscaledTime);
            yield return null;
        }

        Vector2 start = _animal.Position;
        Vector2 end = new Vector2(30f, -30f);
        for (int i = 0; i <= 20; i++)
        {
            _animal.SetPosition(Vector2.Lerp(start, end, i / 20f));
            yield return null;
        }
        _animal.Hide();
        AudioManager.Play("horn");

        int driveSteps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.1f, driveAwaySeconds) * 60f));
        for (int i = 0; i <= driveSteps; i++)
        {
            _car.DriveAway(i / (float)driveSteps);
            yield return null;
        }

        CompleteLevel();
    }

    private void CompleteLevel()
    {
        bool unlockedNew = ProgressManager.RecordWin(_level, 3);
        _rewarding = false;
        _transitioning = false;
        _instruction.text = Loc.T(Loc.Complete);

        _completeTitle.text = Loc.T(Loc.LevelComplete);
        _rewardLabel.text = _level + 1 < GameData.LevelCount
            ? (unlockedNew ? Loc.T(Loc.Unlocked) : Loc.T(Loc.Complete))
            : Loc.T(Loc.AllDone);
        for (int i = 0; i < _completeStars.Length; i++) _completeStars[i].color = Yellow;
        if (_cleanCar != null) _cleanCar.sprite = _car.CleanSprite;
        bool hasNext = _level + 1 < GameData.LevelCount;
        _nextLabel.text = hasNext ? Loc.T(Loc.NextCar) : Loc.T(Loc.ToMenu);
        _completeCard.SetActive(true);
        AudioManager.Play("unlock");
    }

    private void GoToNext()
    {
        if (_level + 1 < GameData.LevelCount) StartLevel(_level + 1);
        else ShowHome();
    }

    private void ShowPause()
    {
        if (_screen != ScreenId.Play || _transitioning || _rewarding || _completeCard.activeSelf) return;
        _paused = true;
        Time.timeScale = 0f;
        _pausePanel.SetActive(true);
        AudioManager.Play("click");
    }

    private void Resume()
    {
        _paused = false;
        Time.timeScale = 1f;
        _pausePanel.SetActive(false);
        AudioManager.Play("click");
    }

    private void RestartLevel()
    {
        Resume();
        StartLevel(_level);
    }

    private void ShowExitDialog()
    {
        _exitOpen = true;
        _exitPanel.SetActive(true);
        AudioManager.Play("click");
    }

    private void CloseExitDialog()
    {
        _exitOpen = false;
        _exitPanel.SetActive(false);
        AudioManager.Play("click");
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ToggleSound()
    {
        AudioManager.Enabled = !AudioManager.Enabled;
        UpdateSoundLabels();
        AudioManager.Play("click");
    }

    private void UpdateSoundLabels()
    {
        string label = AudioManager.Enabled ? Loc.T(Loc.SoundOn) : Loc.T(Loc.SoundOff);
        for (int i = 0; i < _soundLabels.Count; i++)
            if (_soundLabels[i] != null) _soundLabels[i].text = label;
    }

    // ------------------------------------------------------------ resource life

    private void ReleaseCard()
    {
        if (_cardSprite != null) Object.Destroy(_cardSprite);
        if (_cardTexture != null) Object.Destroy(_cardTexture);
        _cardSprite = null;
        _cardTexture = null;
        if (_cardCar != null) _cardCar.sprite = null;
    }

    private void ReleaseLevel()
    {
        if (_animal != null) _animal.Dispose();
        if (_car != null) _car.Dispose();
        if (_overlayTexture != null) Object.Destroy(_overlayTexture);
        _overlayTexture = null;
        _surface = null;
        if (_cleanCar != null) _cleanCar.sprite = null;
        _hint.SetActive(false);
        _flashAlpha = 0f;
        _flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        ReleaseLevel();
        ReleaseCard();
    }
}
