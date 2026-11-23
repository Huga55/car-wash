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
    [SerializeField] private float brushRadiusMultiplier = 1.25f;
    [SerializeField] private float stageTransitionSeconds = 0.9f;
    [SerializeField] private float rewardSparkleSeconds = 1.0f;
    [SerializeField] private float driveAwaySeconds = 1.5f;
    /// <summary>Total length of the celebration, excluding nothing - the whole
    /// 10-second scene is deliberate and is never auto-skipped.</summary>
    public const float RewardSceneSeconds = 10f;

    // Restrained wash-bay palette: graphite, steel blue, white and a single
    // cyan/amber accent. No candy pink or acid green anywhere in the UI.
    private static readonly Color Ink = new Color32(34, 46, 62, 255);
    private static readonly Color Teal = new Color32(36, 146, 158, 255);
    private static readonly Color Steel = new Color32(58, 104, 152, 255);
    private static readonly Color Amber = new Color32(238, 178, 70, 255);
    private static readonly Color Graphite = new Color32(74, 90, 110, 255);
    private static readonly Color Cyan = new Color32(86, 190, 226, 255);
    private static readonly Color Pale = new Color32(242, 247, 251, 238);

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
    private readonly List<Text> _soundLabels = new List<Text>();

    // Audio settings are independent: muting one bus never changes the other.
    private GameObject _audioPanel;
    private Button _musicToggleButton;
    private Button _sfxToggleButton;
    private Button _muteToggleButton;
    private Text _musicToggleLabel;
    private Text _sfxToggleLabel;
    private Text _muteToggleLabel;
    private readonly Text[] _volumeLabels = new Text[3];

    // Auto update: a silent check on launch and an explicit Russian dialog.
    private UpdateChecker _updates;
    private Button _updateButton;
    private Text _updateButtonLabel;
    private GameObject _updatePanel;
    private Text _updateTitle;
    private Text _updateVersion;
    private Text _updateNotes;
    private Text _updateStatus;
    private Image _updateProgressFill;
    private Button _updateInstallButton;
    private UpdateInfo _pendingUpdate;
    private string _updateMessage;
    private bool _autoChecked;
    private bool _manualCheck;

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
    private readonly Text[] _stageLabels = new Text[5];
    // Zone markers are deliberately NOT created in the play view. The zone
    // system still drives completion, but its debug overlay exists only in the
    // Editor (see DumpZoneDebug) and can never appear in a player build.
    private const int EffectPool = 24;
    private readonly Image[] _effects = new Image[EffectPool];
    private readonly float[] _effectLife = new float[EffectPool];
    private readonly float[] _effectDrift = new float[EffectPool];
    private int _effectCursor;
    private Image _toolCursor;
    private Image _stream;
    private Vector2 _lastToolUv;
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
    private bool _skipReward;
    private Button _skipButton;
    private bool _paused;
    private bool _exitOpen;
    private float _flashAlpha;

    private void Awake()
    {
        BuildUi();
        ApplySafeArea();
        ShowHome();

        _updates = FindObjectOfType<UpdateChecker>();
        if (_updates != null)
        {
            _updates.Checked += OnUpdateChecked;
            _updates.Progress += OnUpdateProgress;
            _updates.Installed += OnUpdateInstalled;
        }
    }

    private void Update()
    {
        if (_screenWidth != Screen.width || _screenHeight != Screen.height) ApplySafeArea();
        AudioManager.Tick(Time.unscaledDeltaTime);

        float time = Time.unscaledTime;
        AnimateMenu(time);
        AnimateFlash();
        HandleSystemKey();
        AnimateConfetti(time);

        // Silent launch check: only tells the child something when a newer
        // release actually exists. Manual checks always show a result dialog.
        if (!_autoChecked && !Application.isEditor && _screen == ScreenId.Home && time > 2f &&
            Application.internetReachability != NetworkReachability.NotReachable)
        {
            _autoChecked = true;
            if (_updates != null && !_updates.Busy) _updates.Check();
        }

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
        BuildUpdateDialog();
        BuildAudioSettings();
        AudioManager.SetMusicMode("menu");
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
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 100f), new Vector2(0f, -68f));
        UiFactory.Label(_homePanel.transform, "Tagline", "Весёлая мойка для машинок", 30, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(800f, 50f), new Vector2(0f, -132f));

        Color32[] previewPixels = CarArt.Render(CarKind.Sedan,
            new Color32(72, 156, 255, 255), new Color32(255, 208, 74, 255), 0.94f, 384, 256);
        var previewTexture = CarArt.MakeTexture(previewPixels, 384, 256, "MenuCar");
        var previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, 384, 256),
            new Vector2(0.5f, 0.5f), 100f);
        _preview = UiFactory.CenteredImage(_homePanel.transform, "MenuCar", Color.white,
            new Vector2(620f, 414f), new Vector2(0f, 35f), previewSprite);

        UiFactory.Button(_homePanel.transform, "PlayButton", Loc.T(Loc.Play), Teal, 46,
            new Vector2(390f, 94f), new Vector2(0f, -198f),
            delegate { StartLevel(ProgressManager.UnlockedLevel); });
        UiFactory.Button(_homePanel.transform, "CarsButton", Loc.T(Loc.Cars), Steel, 38,
            new Vector2(360f, 82f), new Vector2(0f, -300f), ShowLevels);

        // Top-left: clear of the logo, tagline, car preview and the big buttons.
        _updateButton = UiFactory.Button(_homePanel.transform, "UpdateButton",
            Loc.T(Loc.UpdateCheck), Graphite, 20, new Vector2(250f, 66f),
            new Vector2(135f, -210f), OnUpdateButtonClicked);
        var updateRect = _updateButton.GetComponent<RectTransform>();
        updateRect.anchorMin = new Vector2(0f, 1f);
        updateRect.anchorMax = new Vector2(0f, 1f);
        updateRect.anchoredPosition = new Vector2(135f, -210f);
        _updateButtonLabel = _updateButton.GetComponentInChildren<Text>();

        AddSoundButton(_homePanel.transform, new Vector2(1f, 1f), new Vector2(-110f, -62f),
            new Vector2(170f, 62f));
        AddAudioSettingsButton(_homePanel.transform, new Vector2(1f, 1f), new Vector2(-110f, -136f));
        UiFactory.Button(_homePanel.transform, "ExitButton", Loc.T(Loc.Exit), Graphite, 30,
            new Vector2(190f, 66f), new Vector2(0f, -364f), ShowExitDialog);

        UpdateAudioLabels();
    }

    private void BuildLevels()
    {
        UiFactory.Label(_levelsPanel.transform, "Title", Loc.T(Loc.PickCar), 64, Ink,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(760f, 90f), new Vector2(0f, -62f));
        _progressLabel = UiFactory.Label(_levelsPanel.transform, "Progress", "", 28, Ink,
            TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(360f, 54f), new Vector2(-190f, -66f));
        AddAudioSettingsButton(_levelsPanel.transform, new Vector2(1f, 1f), new Vector2(-110f, -138f));

        var card = UiFactory.CenteredImage(_levelsPanel.transform, "Card", Pale,
            new Vector2(760f, 430f), new Vector2(0f, 10f), UiFactory.RoundSprite, true);
        _cardPanel = card;

        _cardCar = UiFactory.CenteredImage(card.transform, "Car", Color.white,
            new Vector2(640f, 330f), new Vector2(0f, 34f));
        _cardName = UiFactory.CenteredLabel(card.transform, "Name", "", 40, Ink,
            new Vector2(660f, 64f), new Vector2(0f, -130f));
        for (int i = 0; i < _cardStars.Length; i++)
            _cardStars[i] = UiFactory.CenteredImage(card.transform, "Star" + i, Amber,
                new Vector2(58f, 58f), new Vector2(-70f + i * 70f, -182f), UiFactory.StarSprite);
        _cardLock = UiFactory.CenteredImage(card.transform, "Lock", Color.white,
            new Vector2(130f, 130f), new Vector2(0f, 20f), UiFactory.LockSprite);
        _cardLock.gameObject.SetActive(false);

        UiFactory.Button(_levelsPanel.transform, "Prev", "<", Steel, 46,
            new Vector2(96f, 96f), new Vector2(-470f, 10f), delegate { MoveSelection(-1); });
        UiFactory.Button(_levelsPanel.transform, "Next", ">", Steel, 46,
            new Vector2(96f, 96f), new Vector2(470f, 10f), delegate { MoveSelection(1); });

        _chooseButton = UiFactory.Button(_levelsPanel.transform, "Choose", Loc.T(Loc.Choose), Teal, 36,
            new Vector2(330f, 82f), new Vector2(0f, -258f), StartSelectedLevel);
        _chooseLabel = _chooseButton.GetComponentInChildren<Text>();
        UiFactory.Button(_levelsPanel.transform, "Back", Loc.T(Loc.Back), Steel, 32,
            new Vector2(240f, 72f), new Vector2(0f, -344f), ShowHome);
        UpdateAudioLabels();
    }

    private void BuildPlay()
    {
        UiFactory.Image(_playPanel.transform, "TopBar", new Color(1f, 1f, 1f, 0.68f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 108f), new Vector2(0f, -54f),
            UiFactory.RoundSprite, true);

        _levelLabel = UiFactory.Label(_playPanel.transform, "LevelLabel", "", 28, Ink,
            TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(380f, 60f), new Vector2(200f, -54f));

        // Restrained vertical stage sequence. The car stays the biggest thing
        // on screen; these compact pills only mark where the child is.
        for (int i = 0; i < _stageDots.Length; i++)
        {
            _stageDots[i] = UiFactory.CenteredImage(_playPanel.transform, "StagePill" + i,
                new Color32(255, 255, 255, 185), new Vector2(126f, 50f),
                new Vector2(-518f, 190f - i * 62f), UiFactory.RoundSprite, true);
            UiFactory.CenteredLabel(_stageDots[i].transform, "Number", (i + 1).ToString(), 20, Ink,
                new Vector2(34f, 34f), new Vector2(-42f, 0f), false);
            _stageLabels[i] = UiFactory.CenteredLabel(_stageDots[i].transform, "Name", "", 20, Ink,
                new Vector2(76f, 40f), new Vector2(14f, 0f), false);
        }
        var pauseButton = UiFactory.Button(_playPanel.transform, "Pause", Loc.T(Loc.Pause), Graphite, 26,
            new Vector2(150f, 64f), new Vector2(-110f, -54f), ShowPause);
        var pauseRect = pauseButton.GetComponent<RectTransform>();
        pauseRect.anchorMin = new Vector2(1f, 1f);
        pauseRect.anchorMax = new Vector2(1f, 1f);

        UiFactory.Image(_playPanel.transform, "ProgressBack", new Color32(58, 78, 104, 210),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(370f, 18f), new Vector2(0f, -122f),
            UiFactory.RoundSprite, true);
        _progressFill = UiFactory.Image(_playPanel.transform, "ProgressFill", Teal,
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

        // A tiny pool of transient images gives every stroke visible water,
        // foam, sponge or polish feedback without a particle system.
        for (int i = 0; i < _effects.Length; i++)
        {
            _effects[i] = UiFactory.CenteredImage(_washRect, "Effect" + i,
                new Color(1f, 1f, 1f, 0f), new Vector2(36f, 36f), Vector2.zero, UiFactory.SoftSprite);
            _effects[i].gameObject.SetActive(false);
        }

        // Continuous jet drawn from the tool towards the contact point. The
        // soft sprite stretched into a tapered strip reads as pressurised water
        // without a particle system or a custom shader.
        _stream = UiFactory.CenteredImage(_washRect, "WaterStream", new Color(0.62f, 0.86f, 1f, 0.72f),
            new Vector2(120f, 9f), Vector2.zero, UiFactory.SoftSprite, true);
        _stream.gameObject.SetActive(false);
        _toolCursor = UiFactory.CenteredImage(_washRect, "ToolCursor", Color.white,
            new Vector2(112f, 112f), Vector2.zero, WashingTools.Icon(WashStage.Wet));
        _toolCursor.gameObject.SetActive(false);

        for (int i = 0; i < _sparkles.Length; i++)
        {
            _sparkles[i] = UiFactory.CenteredImage(_washRect, "Sparkle" + i, Amber,
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
        UiFactory.CenteredImage(_playPanel.transform, "ToolBack", Cyan,
            new Vector2(104f, 104f), new Vector2(-448f, 56f), UiFactory.RoundSprite, true);
        _toolIcon = UiFactory.CenteredImage(_playPanel.transform, "ToolIcon", Color.white,
            new Vector2(82f, 82f), new Vector2(-448f, 56f), WashingTools.Icon(WashStage.Wet));
        _toolLabel = UiFactory.Label(_playPanel.transform, "ToolLabel", Loc.StageTool(WashStage.Wet), 28, Ink,
            TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(160f, 70f), new Vector2(-310f, 56f));
        _instruction = UiFactory.CenteredLabel(_playPanel.transform, "Instruction",
            Loc.StageInstruction(WashStage.Wet), 30, Ink, new Vector2(420f, 76f), new Vector2(0f, 56f));
        AddSoundButton(_playPanel.transform, new Vector2(0.5f, 0f), new Vector2(300f, 56f),
            new Vector2(130f, 62f));
        UiFactory.Button(_playPanel.transform, "ToMenu", Loc.T(Loc.ToMenu), Steel, 26,
            new Vector2(160f, 62f), new Vector2(462f, 56f), ShowPause);

        _skipButton = UiFactory.Button(_playPanel.transform, "SkipReward", Loc.T(Loc.Skip), Graphite, 26,
            new Vector2(190f, 60f), new Vector2(470f, 152f), SkipReward);
        var skipRect = _skipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0f);
        skipRect.anchorMax = new Vector2(0.5f, 0f);
        _skipButton.gameObject.SetActive(false);

        _flash = UiFactory.StretchImage(_playPanel.transform, "Flash", new Color(1f, 1f, 1f, 0f));

        _car = new CarViewController(carImage, overlayImage, _washRect.sizeDelta);
        _washing = new WashingController(_washRect);
        _washing.StrokeStarted = OnStrokeStarted;
        _washing.StrokeMoved = OnStrokeMoved;
        _washing.StrokeEnded = OnStrokeEnded;
        _animal = new AnimalActor(animalPortrait, animalSpeech, new Vector2(330f, -70f));
        UpdateAudioLabels();
    }

    private void BuildPause()
    {
        _pausePanel = Panel("PauseDialog");
        var dim = UiFactory.StretchImage(_pausePanel.transform, "Dim", new Color(0f, 0f, 0f, 0.42f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_pausePanel.transform, "Card", Pale, new Vector2(620f, 590f),
            Vector2.zero, UiFactory.RoundSprite, true);
        UiFactory.CenteredLabel(_pausePanel.transform, "Title", Loc.T(Loc.Pause), 68, Ink,
            new Vector2(560f, 90f), new Vector2(0f, 208f));
        UiFactory.Button(_pausePanel.transform, "Resume", Loc.T(Loc.Resume), Teal, 34,
            new Vector2(380f, 82f), new Vector2(0f, 110f), Resume);
        UiFactory.Button(_pausePanel.transform, "Restart", Loc.T(Loc.Restart), Steel, 32,
            new Vector2(380f, 76f), new Vector2(0f, 20f), RestartLevel);
        UiFactory.Button(_pausePanel.transform, "Menu", Loc.T(Loc.ToMenu), Graphite, 32,
            new Vector2(380f, 76f), new Vector2(0f, -70f), ShowHome);
        AddAudioSettingsButton(_pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -158f));
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
        UiFactory.Button(_exitPanel.transform, "Stay", Loc.T(Loc.Stay), Teal, 34,
            new Vector2(250f, 78f), new Vector2(-150f, -70f), CloseExitDialog);
        UiFactory.Button(_exitPanel.transform, "Quit", Loc.T(Loc.Quit), Graphite, 34,
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
                i % 2 == 0 ? Amber : Graphite, new Vector2(34f, 34f), Vector2.zero, UiFactory.StarSprite);

        _completeTitle = UiFactory.CenteredLabel(_completeCard.transform, "Title", Loc.T(Loc.LevelComplete),
            58, Ink, new Vector2(720f, 82f), new Vector2(0f, 202f));
        UiFactory.CenteredImage(_completeCard.transform, "CleanCarBack", new Color(1f, 1f, 1f, 0.7f),
            new Vector2(600f, 220f), new Vector2(0f, 92f), UiFactory.RoundSprite, true);
        _cleanCar = UiFactory.CenteredImage(_completeCard.transform, "CleanCar", Color.white,
            new Vector2(560f, 210f), new Vector2(0f, 92f));

        for (int i = 0; i < _completeStars.Length; i++)
            _completeStars[i] = UiFactory.CenteredImage(_completeCard.transform, "Star" + i, Amber,
                new Vector2(78f, 78f), new Vector2(-110f + i * 110f, -28f), UiFactory.StarSprite);

        _rewardLabel = UiFactory.CenteredLabel(_completeCard.transform, "Reward", "", 26, Ink,
            new Vector2(700f, 60f), new Vector2(0f, -102f));
        _nextButton = UiFactory.Button(_completeCard.transform, "Next", Loc.T(Loc.NextCar), Teal, 34,
            new Vector2(320f, 80f), new Vector2(168f, -188f), GoToNext);
        _nextLabel = _nextButton.GetComponentInChildren<Text>();
        UiFactory.Button(_completeCard.transform, "Menu", Loc.T(Loc.ToMenu), Steel, 32,
            new Vector2(270f, 76f), new Vector2(-180f, -188f), ShowHome);
        _completeCard.SetActive(false);
    }

    private Image _cleanCar;
    private Button _nextButton;

    private void BuildUpdateDialog()
    {
        _updatePanel = Panel("UpdateDialog");
        var dim = UiFactory.StretchImage(_updatePanel.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_updatePanel.transform, "Card", Pale, new Vector2(760f, 470f),
            Vector2.zero, UiFactory.RoundSprite, true);

        _updateTitle = UiFactory.CenteredLabel(_updatePanel.transform, "Title",
            Loc.T(Loc.UpdateTitle), 42, Ink, new Vector2(660f, 66f), new Vector2(0f, 188f));
        _updateVersion = UiFactory.CenteredLabel(_updatePanel.transform, "Version", "", 26, Steel,
            new Vector2(660f, 46f), new Vector2(0f, 134f));
        _updateNotes = UiFactory.CenteredLabel(_updatePanel.transform, "Notes", "", 24, Ink,
            new Vector2(660f, 150f), new Vector2(0f, 28f));
        _updateStatus = UiFactory.CenteredLabel(_updatePanel.transform, "Status", "", 22, Ink,
            new Vector2(660f, 40f), new Vector2(0f, -70f));

        UiFactory.CenteredImage(_updatePanel.transform, "ProgressBack", new Color32(58, 78, 104, 210),
            new Vector2(580f, 16f), new Vector2(0f, -102f), UiFactory.SoftSprite, true);
        _updateProgressFill = UiFactory.Image(_updatePanel.transform, "ProgressFill", Teal,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(580f, 14f),
            new Vector2(-290f, -102f), UiFactory.SoftSprite, true);
        _updateProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        _updateProgressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);

        _updateInstallButton = UiFactory.Button(_updatePanel.transform, "Install",
            Loc.T(Loc.UpdateInstall), Teal, 30, new Vector2(270f, 66f),
            new Vector2(-155f, -148f), OnInstallConfirmed);
        UiFactory.Button(_updatePanel.transform, "Later", Loc.T(Loc.UpdateLater), Graphite, 30,
            new Vector2(270f, 66f), new Vector2(155f, -148f), CloseUpdateDialog);
        UiFactory.Button(_updatePanel.transform, "OpenReleases", Loc.T(Loc.UpdateOpenPage), Steel, 22,
            new Vector2(300f, 44f), new Vector2(0f, -205f), OpenReleasesPage);

        _updatePanel.SetActive(false);
    }

    private void AddSoundButton(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var button = UiFactory.Button(parent, "SoundButton", "", Graphite, 24, size, position, ToggleSound);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        _soundLabels.Add(button.GetComponentInChildren<Text>());
    }

    private void AddAudioSettingsButton(Transform parent, Vector2 anchor, Vector2 position)
    {
        UiFactory.Button(parent, "AudioSettingsButton", Loc.T(Loc.AudioSettingsShort), Steel, 24,
            new Vector2(170f, 62f), position, ShowAudioSettings);
        // UiFactory centres on (0.5, 0.5); move the button to the requested
        // anchor without changing its authored design size.
        Transform button = parent.Find("AudioSettingsButton");
        if (button != null)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }
    }

    private void BuildAudioSettings()
    {
        _audioPanel = Panel("AudioSettingsDialog");
        var dim = UiFactory.StretchImage(_audioPanel.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        dim.raycastTarget = true;
        UiFactory.CenteredImage(_audioPanel.transform, "Card", Pale, new Vector2(760f, 620f),
            Vector2.zero, UiFactory.RoundSprite, true);
        UiFactory.CenteredLabel(_audioPanel.transform, "Title", Loc.T(Loc.AudioSettings), 44, Ink,
            new Vector2(660f, 70f), new Vector2(0f, 255f));

        _musicToggleButton = UiFactory.Button(_audioPanel.transform, "MusicToggle", "", Teal, 28,
            new Vector2(430f, 66f), new Vector2(0f, 180f), ToggleMusic);
        _musicToggleLabel = _musicToggleButton.GetComponentInChildren<Text>();
        _sfxToggleButton = UiFactory.Button(_audioPanel.transform, "SfxToggle", "", Steel, 28,
            new Vector2(430f, 66f), new Vector2(0f, 105f), ToggleSfx);
        _sfxToggleLabel = _sfxToggleButton.GetComponentInChildren<Text>();
        _muteToggleButton = UiFactory.Button(_audioPanel.transform, "MuteToggle", "", Graphite, 28,
            new Vector2(430f, 66f), new Vector2(0f, 30f), ToggleMute);
        _muteToggleLabel = _muteToggleButton.GetComponentInChildren<Text>();

        BuildVolumeRow(0, Loc.MasterVolume, -48f, delegate { ChangeVolume(0, -0.05f); },
            delegate { ChangeVolume(0, 0.05f); });
        BuildVolumeRow(1, Loc.MusicVolume, -123f, delegate { ChangeVolume(1, -0.05f); },
            delegate { ChangeVolume(1, 0.05f); });
        BuildVolumeRow(2, Loc.SfxVolume, -198f, delegate { ChangeVolume(2, -0.05f); },
            delegate { ChangeVolume(2, 0.05f); });

        UiFactory.Button(_audioPanel.transform, "Close", Loc.T(Loc.Close), Teal, 30,
            new Vector2(250f, 66f), new Vector2(0f, -265f), CloseAudioSettings);
        _audioPanel.SetActive(false);
        UpdateAudioLabels();
    }

    private void BuildVolumeRow(int index, string titleKey, float y, UnityEngine.Events.UnityAction minus,
        UnityEngine.Events.UnityAction plus)
    {
        UiFactory.CenteredLabel(_audioPanel.transform, "VolumeTitle" + index, Loc.T(titleKey), 25, Ink,
            new Vector2(280f, 56f), new Vector2(-210f, y));
        UiFactory.Button(_audioPanel.transform, "VolumeMinus" + index, "-", Steel, 28,
            new Vector2(68f, 58f), new Vector2(80f, y), minus);
        _volumeLabels[index] = UiFactory.CenteredLabel(_audioPanel.transform, "VolumeValue" + index,
            "100%", 25, Ink, new Vector2(120f, 56f), new Vector2(175f, y));
        UiFactory.Button(_audioPanel.transform, "VolumePlus" + index, "+", Steel, 28,
            new Vector2(68f, 58f), new Vector2(270f, y), plus);
    }

    private void ShowAudioSettings()
    {
        AudioManager.PlayUi("click");
        UpdateAudioLabels();
        if (_updatePanel != null) _updatePanel.SetActive(false);
        _audioPanel.SetActive(true);
    }

    private void CloseAudioSettings()
    {
        AudioManager.PlayUi("click");
        _audioPanel.SetActive(false);
    }

    private void ToggleMusic()
    {
        AudioManager.MusicEnabled = !AudioManager.MusicEnabled;
        UpdateAudioLabels();
        AudioManager.PlayUi("click");
    }

    private void ToggleSfx()
    {
        AudioManager.SfxEnabled = !AudioManager.SfxEnabled;
        UpdateAudioLabels();
        AudioManager.PlayUi("click");
    }

    private void ToggleMute()
    {
        AudioManager.Muted = !AudioManager.Muted;
        UpdateAudioLabels();
        AudioManager.PlayUi("click");
    }

    private void ChangeVolume(int index, float delta)
    {
        if (index == 0) AudioManager.MasterVolume = Mathf.Round((AudioManager.MasterVolume + delta) * 100f) / 100f;
        else if (index == 1) AudioManager.MusicVolume = Mathf.Round((AudioManager.MusicVolume + delta) * 100f) / 100f;
        else AudioManager.SfxVolume = Mathf.Round((AudioManager.SfxVolume + delta) * 100f) / 100f;
        UpdateAudioLabels();
        AudioManager.PlayUi("click");
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    private void UpdateAudioLabels()
    {
        if (_musicToggleLabel != null)
            _musicToggleLabel.text = Loc.T(AudioManager.MusicEnabled ? Loc.MusicOn : Loc.MusicOff);
        if (_sfxToggleLabel != null)
            _sfxToggleLabel.text = Loc.T(AudioManager.SfxEnabled ? Loc.SfxOn : Loc.SfxOff);
        if (_muteToggleLabel != null)
            _muteToggleLabel.text = Loc.T(AudioManager.Muted ? Loc.MuteOff : Loc.MuteOn);
        if (_musicToggleButton != null)
            _musicToggleButton.GetComponent<Image>().color = AudioManager.MusicEnabled ? Teal : Graphite;
        if (_sfxToggleButton != null)
            _sfxToggleButton.GetComponent<Image>().color = AudioManager.SfxEnabled ? Steel : Graphite;
        if (_muteToggleButton != null)
            _muteToggleButton.GetComponent<Image>().color = AudioManager.Muted ? Graphite : Steel;
        if (_volumeLabels[0] != null) _volumeLabels[0].text = Percent(AudioManager.MasterVolume);
        if (_volumeLabels[1] != null) _volumeLabels[1].text = Percent(AudioManager.MusicVolume);
        if (_volumeLabels[2] != null) _volumeLabels[2].text = Percent(AudioManager.SfxVolume);
        string quickLabel = Loc.T(AudioManager.Muted ? Loc.SoundOff : Loc.SoundOn);
        for (int i = 0; i < _soundLabels.Count; i++)
            if (_soundLabels[i] != null) _soundLabels[i].text = quickLabel;
    }

    // ---------------------------------------------------------- auto update

    private void SetUpdateLabel(string text, bool enabled)
    {
        if (_updateButtonLabel != null) _updateButtonLabel.text = text;
        if (_updateButton != null) _updateButton.interactable = enabled;
    }

    private void OnUpdateButtonClicked()
    {
        AudioManager.Play("click");
        if (_pendingUpdate != null && UpdateService.IsNewer(_pendingUpdate))
        {
            _updateMessage = null;
            ShowUpdateDialog();
            return;
        }
        if (_updates == null || _updates.Busy) return;
        _manualCheck = true;
        SetUpdateLabel(Loc.T(Loc.UpdateChecking), false);
        _updates.Check();
    }

    private void OnUpdateChecked(UpdateInfo info, string error)
    {
        bool manual = _manualCheck;
        _manualCheck = false;

        if (!string.IsNullOrEmpty(error) || info == null)
        {
            _pendingUpdate = null;
            _updateMessage = Loc.T(Loc.UpdateCheckError);
            if (manual) SetUpdateLabel(Loc.T(Loc.UpdateError), true);
        }
        else if (UpdateService.IsNewer(info))
        {
            _pendingUpdate = info;
            _updateMessage = null;
            SetUpdateLabel(Loc.T(Loc.UpdateAvailable), true);
        }
        else
        {
            _pendingUpdate = null;
            _updateMessage = Loc.T(Loc.UpdateNoNeed);
            if (manual) SetUpdateLabel(Loc.T(Loc.UpdateLatest), false);
        }

        if (manual) ShowUpdateDialog();
        else if (_updatePanel.activeSelf) UpdateUpdateDialog();
    }

    private void ShowUpdateDialog()
    {
        UpdateUpdateDialog();
        _updatePanel.SetActive(true);
    }

    private void UpdateUpdateDialog()
    {
        if (_updateTitle != null) _updateTitle.text = Loc.T(Loc.UpdateTitle);
        if (_updateVersion != null)
        {
            _updateVersion.text = _pendingUpdate != null && UpdateService.IsNewer(_pendingUpdate)
                ? string.Format(Loc.T(Loc.UpdateVersion), _pendingUpdate.version)
                : "Little Car Wash " + AppInfo.Version;
        }
        if (_updateNotes != null)
        {
            _updateNotes.text = _pendingUpdate != null && !string.IsNullOrEmpty(_pendingUpdate.notes)
                ? _pendingUpdate.notes
                : Loc.T(Loc.UpdateNoNeed);
        }
        if (_updateStatus != null) _updateStatus.text = _updateMessage ?? string.Empty;
        if (_updateInstallButton != null)
            _updateInstallButton.interactable = _pendingUpdate != null &&
                                                UpdateService.IsNewer(_pendingUpdate) &&
                                                (_updates == null || !_updates.Busy);
        if (_updateProgressFill != null) _updateProgressFill.gameObject.SetActive(_pendingUpdate != null);
    }

    private void CloseUpdateDialog()
    {
        _updatePanel.SetActive(false);
    }

    private void OpenReleasesPage()
    {
        Application.OpenURL(AppInfo.ReleasesPageUrl);
    }

    private void OnInstallConfirmed()
    {
        if (_pendingUpdate == null || _updates == null || _updates.Busy) return;
        _updateMessage = Loc.T(Loc.UpdateDownloading);
        if (_updateStatus != null) _updateStatus.text = _updateMessage;
        if (_updateInstallButton != null) _updateInstallButton.interactable = false;
        if (_updateProgressFill != null) _updateProgressFill.rectTransform.localScale = new Vector3(0.001f, 1f, 1f);
        _updates.Install(_pendingUpdate);
    }

    private void OnUpdateProgress(float value)
    {
        if (_updateProgressFill != null)
            _updateProgressFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(value), 1f, 1f);
    }

    private void OnUpdateInstalled(bool openedBrowser, string error)
    {
        if (openedBrowser)
        {
            _updateMessage = Loc.T(Loc.UpdateBrowserHint);
        }
        else if (string.IsNullOrEmpty(error))
        {
            // Windows: the batch started by UpdateService replaces the files
            // after this process exits and launches the new build itself.
            Application.Quit();
            return;
        }
        else
        {
            _updateMessage = error == "PLATFORM_UNAVAILABLE"
                ? Loc.T(Loc.UpdatePlatformError)
                : Loc.T(Loc.UpdateInstallError);
        }

        if (_updateStatus != null) _updateStatus.text = _updateMessage;
        if (_updateInstallButton != null) _updateInstallButton.interactable = true;
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
        AudioManager.StopTool();
        AudioManager.SetMusicMode("menu");
        ReleaseLevel();
        _screen = ScreenId.Home;
        _homePanel.SetActive(true);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _exitPanel.SetActive(false);
        if (_updatePanel != null) _updatePanel.SetActive(false);
        if (_audioPanel != null) _audioPanel.SetActive(false);
        _paused = false;
        _exitOpen = false;
        _rewarding = false;
        _skipReward = false;
        if (_skipButton != null) _skipButton.gameObject.SetActive(false);
        Time.timeScale = 1f;
        SetBackground(0);
    }

    private void ShowLevels()
    {
        StopAllCoroutines();
        AudioManager.StopTool();
        AudioManager.SetMusicMode("menu");
        ReleaseLevel();
        _screen = ScreenId.Levels;
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(true);
        _playPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _exitPanel.SetActive(false);
        if (_updatePanel != null) _updatePanel.SetActive(false);
        if (_audioPanel != null) _audioPanel.SetActive(false);
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
        AudioManager.StopTool();
        AudioManager.SetMusicMode("wash");
        ReleaseLevel();
        ReleaseCard();
        if (_audioPanel != null) _audioPanel.SetActive(false);

        _level = index;
        _levelDef = GameData.Levels[index];
        _screen = ScreenId.Play;
        _homePanel.SetActive(false);
        _levelsPanel.SetActive(false);
        _playPanel.SetActive(true);
        _pausePanel.SetActive(false);
        if (_updatePanel != null) _updatePanel.SetActive(false);
        _exitPanel.SetActive(false);
        _paused = false;
        _exitOpen = false;
        Time.timeScale = 1f;

        byte[] panelMask;
        Color32[] pixels = CarArt.Render(_levelDef.Kind, _levelDef.Body, _levelDef.Accent,
            _levelDef.Size, 384, 256, out panelMask);
        _surface = new WashSurface(pixels, 384, 256, _levelDef.Dirt, index + 1,
            _levelDef.DirtType, panelMask);
        _overlayTexture = _surface.Texture;
        _car.Load(pixels, _levelDef, _surface);
        _washing.Begin(_surface, _levelDef.Size, _car.CarRect,
            Mathf.Clamp(brushRadiusMultiplier, 0.8f, 1.6f));

        _stages = GameData.StagesFor(index);
        _stageIndex = 0;
        _stage = _stages[0];
        _transitioning = false;
        _rewarding = false;
        _skipReward = false;
        if (_skipButton != null) _skipButton.gameObject.SetActive(false);
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
            _cardStars[i].color = i < stars ? Amber : new Color32(255, 255, 255, 110);

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

        for (int i = 0; i < _stageDots.Length; i++)
        {
            bool used = i < _stages.Length;
            _stageDots[i].gameObject.SetActive(used);
            if (!used) continue;
            bool active = i == _stageIndex;
            bool passed = i < _stageIndex;
            _stageDots[i].color = active ? Teal : (passed ? new Color32(214, 232, 236, 235) :
                new Color32(255, 255, 255, 185));
            _stageLabels[i].text = (passed ? "✓ " : "") + Loc.StageShort(_stages[i]);
            _stageLabels[i].color = active || passed ? Color.white : Ink;
            float s = active ? 1.06f : 1f;
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
            float t = Mathf.Clamp01(_effectLife[i] / 0.62f);
            Color c = fx.color;
            c.a = t * 0.9f;
            fx.color = c;
            Vector2 pos = fx.rectTransform.anchoredPosition;
            pos.y += dt * _effectDrift[i];
            fx.rectTransform.anchoredPosition = pos;
            float s = 1.15f - t * 0.35f;
            fx.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only diagnostics: zone coverage stays available to
    /// developers without ever creating a single marker in a player build.</summary>
    public void DumpZoneDebug()
    {
        if (_washing == null) return;
        string text = "Zone coverage for " + _stage + ":";
        for (int i = 0; i < WashSurface.ZoneCount; i++)
            text += "\n  " + i + " = " + _washing.ZoneProgress(_stage, i).ToString("F2");
        Debug.Log(text);
    }
#endif

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
        // One reusable looping source per tool family. Nothing is created on
        // a stroke and no identical water loops can pile up.
        AudioManager.StartTool(WashingTools.Sound(stage));
        _toolIcon.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
        _toolCursor.sprite = WashingTools.Icon(stage);
        _toolCursor.gameObject.SetActive(true);
    }

    private void OnStrokeEnded()
    {
        // Fade out over a few frames in AudioManager.Tick; the loop keeps
        // playing quietly until the envelope reaches zero, then stops itself.
        AudioManager.StopTool();
        if (_toolCursor != null) _toolCursor.gameObject.SetActive(false);
        if (_stream != null) _stream.gameObject.SetActive(false);
    }

    private void OnStrokeMoved(Vector2 uv, WashStage stage)
    {
        if (_car != null) _car.WashPulse(uv);
        _toolIcon.rectTransform.localScale = Vector3.Lerp(_toolIcon.rectTransform.localScale,
            Vector3.one, 0.35f);

        // Louder while the child actually moves the tool, quiet when the finger
        // holds still. AudioManager smooths the change, so there are no steps.
        float travel = Vector2.Distance(uv, _lastToolUv);
        AudioManager.SetToolActivity(Mathf.Clamp01(travel / 0.035f));

        float w = _washRect.sizeDelta.x;
        float h = _washRect.sizeDelta.y;
        Vector2 point = new Vector2((uv.x - 0.5f) * w, (uv.y - 0.5f) * h);

        // Sprayers sit above/left of the contact point so a jet has somewhere
        // to come from. The sponge touches the body itself: a visible gap here
        // would look like the tool passing in front of the car.
        bool scrubbing = stage == WashStage.Scrub;
        Vector2 nozzle = point + (scrubbing ? new Vector2(-16f, 18f) : new Vector2(-92f, 78f));
        if (_toolCursor.gameObject.activeSelf)
        {
            _toolCursor.rectTransform.anchoredPosition = nozzle;
            Vector2 delta = point - _lastToolUv;
            float tilt = Mathf.Clamp(-delta.x * 0.8f, -14f, 14f);
            _toolCursor.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (scrubbing)
            {
                float press = Mathf.Sin(Time.unscaledTime * 19f) * 0.05f;
                _toolCursor.rectTransform.localScale = new Vector3(1.04f + press, 0.96f - press, 1f);
            }
            else
            {
                _toolCursor.rectTransform.localScale = Vector3.one;
            }
        }
        _lastToolUv = uv;

        bool jet = stage == WashStage.Wet || stage == WashStage.Rinse ||
                   stage == WashStage.Foam || stage == WashStage.Dry;
        if (jet)
        {
            Vector2 from = nozzle + new Vector2(44f, -44f);
            Vector2 direction = point - from;
            float length = direction.magnitude;
            if (length > 1f)
            {
                _stream.gameObject.SetActive(true);
                _stream.rectTransform.anchoredPosition = (from + point) * 0.5f;
                _stream.rectTransform.sizeDelta = new Vector2(length, stage == WashStage.Foam ? 13f : 8f);
                _stream.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                _stream.color = stage == WashStage.Foam
                    ? new Color(0.96f, 0.98f, 1f, 0.72f)
                    : new Color(0.58f, 0.84f, 1f, 0.66f);
            }
        }
        else if (_stream.gameObject.activeSelf)
        {
            _stream.gameObject.SetActive(false);
        }

        // Impact particles: thin droplets along the jet plus a splash fan at
        // the surface. Everything comes from the fixed pool, nothing allocates.
        if (jet)
        {
            Vector2 splashDir = (point - nozzle).normalized;
            for (int i = 0; i < (GraphicsProfile.Low ? 2 : 4); i++)
            {
                float spread = (i - 1.5f) * 16f;
                Vector2 offset = new Vector2(splashDir.x * (i * 7f) - splashDir.y * spread,
                    splashDir.y * (i * 7f) + splashDir.x * spread);
                // Positive Y is up in UI space: the droplets fall after the
                // initial sideways splash, which reads as water running off.
                SpawnEffect(point + offset, 12f + i * 2f,
                    new Color(0.62f, 0.86f, 1f, 0.78f), 0.42f, -22f - i * 8f);
            }
        }

        if (stage == WashStage.Foam)
        {
            for (int i = 0; i < (GraphicsProfile.Low ? 2 : 4); i++)
                SpawnEffect(point + new Vector2(Mathf.Sin(i * 2.1f) * 22f, Mathf.Cos(i * 1.7f) * 16f),
                    28f + i * 5f, new Color(1f, 1f, 1f, 0.86f), 0.62f, -12f);
        }
        else if (stage == WashStage.Scrub)
        {
            SpawnEffect(point, 44f, new Color(0.94f, 0.97f, 0.88f, 0.62f), 0.5f, 8f);
            SpawnEffect(point + new Vector2(-18f, -8f), 16f,
                new Color(0.82f, 0.72f, 0.42f, 0.6f), 0.42f, -18f);
        }
        else if (stage == WashStage.Dry)
        {
            SpawnEffect(point + new Vector2(20f, 10f), 30f,
                new Color(1f, 0.97f, 0.78f, 0.72f), 0.5f, 56f);
        }
    }

    private void SpawnEffect(Vector2 position, float size, Color color, float life, float drift)
    {
        int i = _effectCursor;
        _effectCursor = (_effectCursor + 1) % _effects.Length;
        Image fx = _effects[i];
        fx.gameObject.SetActive(true);
        fx.rectTransform.anchoredPosition = position;
        fx.rectTransform.sizeDelta = new Vector2(size, size);
        fx.rectTransform.localScale = Vector3.one;
        fx.color = color;
        _effectLife[i] = life;
        // Reuse a parallel drift factor by encoding it in the alpha channel's
        // lower bits would be fragile; a tiny dedicated array keeps it clear.
        _effectDrift[i] = drift;
    }

    private IEnumerator AdvanceStage()
    {
        _transitioning = true;
        AudioManager.StopTool();
        // Gameplay threshold (ZoneFinish) already released the stage. This is
        // only the visible finishing pass: the last few texels are filled over
        // 0.5-1.2 seconds from the SAME masks that are uploaded to the texture.
        _washing.BeginCompletion(_stage);
        _instruction.text = Loc.T(Loc.Great);
        AudioManager.PlayUi("star");

        float duration = Mathf.Clamp(stageTransitionSeconds, 0.5f, 1.2f);
        float start = Time.unscaledTime;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
            _washing.AdvanceCompletion(t);
            _displayProgress = _washing.Progress(_stage);
            _progressFill.rectTransform.localScale = new Vector3(
                Mathf.Max(0.001f, _displayProgress), 1f, 1f);
            if (t >= 1f) break;
            yield return null;
        }
        _washing.AdvanceCompletion(1f);
        _progressFill.rectTransform.localScale = new Vector3(1f, 1f, 1f);
        _flashAlpha = 0.18f;
        yield return new WaitForSecondsRealtime(0.12f);

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

    /// <summary>A single timed reward phase. Skipping only fast-forwards the
    /// remaining phases; it never happens by itself.</summary>
    private IEnumerator RewardPhase(float seconds, System.Action<float> update)
    {
        float start = Time.unscaledTime;
        while (!_skipReward)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / Mathf.Max(0.01f, seconds));
            if (update != null) update(t);
            if (t >= 1f) break;
            yield return null;
        }
    }

    private IEnumerator RewardSequence()
    {
        _rewarding = true;
        _skipReward = false;
        AudioManager.StopTool();
        AudioManager.SetMusicMode("finale");
        _skipButton.gameObject.SetActive(true);
        _hint.SetActive(false);
        for (int i = 0; i < _sparkles.Length; i++) _sparkles[i].gameObject.SetActive(false);

        // Phase 0 - one diagonal polishing sweep, then the clean car sparkles.
        // The sweep replaces the old instant overlay hide, so drying ends with
        // an understandable final action instead of a texture popping away.
        _washing.FinishAll();
        _washing.BeginFinalShine();
        _instruction.text = Loc.T(Loc.Sparkle);
        AudioManager.PlayUi("star");
        yield return StartCoroutine(RewardPhase(rewardSparkleSeconds, t =>
        {
            if (_surface != null) _surface.AdvanceFinalShine(t);
        }));
        if (_surface != null) _surface.EndFinalShine();
        _car.HideOverlay();
        _flashAlpha = 0.34f;

        // Phase 1 - the driver enters from the edge of the screen (1.5 s).
        Vector2 home = Vector2.zero;
        _animal.Show(_level, _levelDef.Driver);
        home = _animal.Home;
        AudioManager.PlayAnimal(_level);
        AudioManager.Play("win");
        yield return StartCoroutine(RewardPhase(1.5f, t =>
        {
            _animal.Animate(Time.unscaledTime);
            _animal.SetPosition(Vector2.Lerp(home + new Vector2(-260f, 0f), home, t));
        }));

        // Phase 2 - joy: jumping and waving, long enough to notice (2 s).
        yield return StartCoroutine(RewardPhase(2f, t =>
        {
            _animal.Animate(Time.unscaledTime);
            float jump = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 26f;
            _animal.SetPosition(home + new Vector2(0f, jump));
        }));
        AudioManager.Play("click");

        // Phase 3 - gratitude line with time to read it (2 s).
        _animal.SetSpeech(Loc.DriverThanks(_level), true);
        yield return StartCoroutine(RewardPhase(2f, t =>
        {
            _animal.Animate(Time.unscaledTime);
            _animal.SetPosition(home + new Vector2(0f, Mathf.Sin(t * Mathf.PI * 2f) * 6f));
        }));

        // Phase 4 - the driver walks to the car and gets in (2 s).
        AudioManager.PlayUi("jump");
        _animal.SetSpeech("", false);
        yield return StartCoroutine(RewardPhase(2f, t =>
        {
            _animal.Animate(Time.unscaledTime);
            _animal.SetPosition(Vector2.Lerp(home, new Vector2(60f, -40f), t));
        }));
        _animal.Hide();
        AudioManager.Play("horn");

        // Phase 5 - the car drives away before the completion card appears.
        AudioManager.PlayAmbient("depart");
        yield return StartCoroutine(RewardPhase(driveAwaySeconds, t => _car.DriveAway(t)));
        // A skip fast-forwards the phases, but it must still leave the world
        // in the same final pose as a fully watched celebration.
        if (_skipReward)
        {
            _animal.Hide();
            _car.DriveAway(1f);
        }
        _skipButton.gameObject.SetActive(false);
        CompleteLevel();
    }

    public void SkipReward()
    {
        if (!_rewarding) return;
        _skipReward = true;
        AudioManager.Play("click");
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
        for (int i = 0; i < _completeStars.Length; i++) _completeStars[i].color = Amber;
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
        AudioManager.StopTool();
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
        // Quick one-tap silence. This changes only the global mute flag; the
        // separate music/SFX switches in the settings dialog stay untouched.
        AudioManager.Muted = !AudioManager.Muted;
        UpdateAudioLabels();
        AudioManager.PlayUi("click");
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
        AudioManager.StopTool();
        if (_updates != null)
        {
            _updates.Checked -= OnUpdateChecked;
            _updates.Progress -= OnUpdateProgress;
            _updates.Installed -= OnUpdateInstalled;
        }
        Time.timeScale = 1f;
        ReleaseLevel();
        ReleaseCard();
    }
}
