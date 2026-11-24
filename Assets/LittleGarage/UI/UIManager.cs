using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittleGarage
{
    /// <summary>
    /// Runtime UGUI interface. Builds all screens once and toggles visibility/state.
    /// No prefabs/assets required.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private const float RefW = 1080f;
        private const float RefH = 1920f;

        private readonly Color _bg = new Color(0.10f, 0.11f, 0.13f, 0.96f);
        private readonly Color _panel = new Color(0.16f, 0.18f, 0.20f, 0.94f);
        private readonly Color _panelSoft = new Color(0.20f, 0.22f, 0.25f, 0.92f);
        private readonly Color _text = new Color(0.93f, 0.94f, 0.95f, 1f);
        private readonly Color _muted = new Color(0.72f, 0.75f, 0.78f, 1f);
        private readonly Color _teal = new Color(0.20f, 0.62f, 0.62f, 1f);
        private readonly Color _amber = new Color(0.90f, 0.66f, 0.24f, 1f);
        private readonly Color _steel = new Color(0.35f, 0.42f, 0.48f, 1f);
        private readonly Color _danger = new Color(0.72f, 0.30f, 0.28f, 1f);

        private UnityEngine.Canvas _canvas;
        private RectTransform _root;
        private RectTransform _safeRoot;
        private EventSystem _eventSystem;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
        private PointerEventData _pointerEventData;

        private Font _font;
        private Sprite _roundedSprite;
        private Sprite _roundedSoftSprite;
        private Sprite _circleSprite;
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        // Screens
        private GameObject _homeScreen;
        private GameObject _garageScreen;
        private GameObject _playScreen;
        private GameObject _completeScreen;
        private GameObject _pausePanel;
        private GameObject _customizationPanel;
        private GameObject _settingsPanel;

        // Home
        private Text _homeProgressText;
        private Slider _homeProgressSlider;
        private Button _homePlayButton;
        private Button _homeSoundButton;
        private Text _homeSoundButtonText;

        // Garage
        private readonly List<JobCard> _jobCards = new List<JobCard>(10);
        private Text _garageProgressText;
        private Button _garageBackButton;

        // Play HUD
        private Text _playTitleText;
        private Text _playTaskText;
        private Text _playInstructionText;
        private Text _playHintText;
        private Slider _playProgressSlider;
        private Button _playPauseButton;
        private RectTransform _toolBar;
        private readonly List<ToolButton> _toolButtons = new List<ToolButton>(6);

        // Complete
        private Text _completeTitleText;
        private Text _completeBodyText;
        private Text _completeRewardText;
        private Button _completeNextButton;
        private Button _completeReplayButton;
        private Button _completeHomeButton;

        // Pause
        private Button _pauseResumeButton;
        private Button _pauseRestartButton;
        private Button _pauseHomeButton;
        private Button _pauseSoundButton;
        private Text _pauseSoundButtonText;

        // Customization
        private Text _customTitleText;
        private RectTransform _colorRow;
        private RectTransform _rimRow;
        private RectTransform _decalRow;
        private readonly List<Button> _colorButtons = new List<Button>(8);
        private readonly List<Button> _rimButtons = new List<Button>(4);
        private readonly List<Button> _decalButtons = new List<Button>(4);
        private Button _customCommitButton;
        private VehicleDef _customDef;

        // Settings
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Toggle _musicToggle;
        private Toggle _sfxToggle;
        private Button _settingsCloseButton;

        private ScreenId _currentScreen = ScreenId.Home;

        /// <summary>Read-only view of the screen last shown; useful for QA and tests.</summary>
        public ScreenId CurrentScreen => _currentScreen;

        private string _currentJobId;
        private ProgressManager _progress;
        private Vector2 _lastSafeMin;
        private Vector2 _lastSafeMax;

        private sealed class JobCard
        {
            public GameObject Root;
            public Button Button;
            public Text Title;
            public Text Description;
            public Text Stars;
            public Image LockOverlay;
            public Text LockText;
            public string JobId;
        }

        private sealed class ToolButton
        {
            public GameObject Root;
            public Button Button;
            public Image Background;
            public Text Label;
            public string ToolId;
        }

        public event Action<string> JobSelected;
        public event Action<string> ToolSelected;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action HomeRequested;
        public event Action NextRequested;
        public event Action<int> ColorSelected;
        public event Action<int> RimSelected;
        public event Action<int> DecalSelected;
        public event Action GarageRequested;
        public event Action CustomizationCommitted;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            ApplySafeAreaIfNeeded();
        }

        public void ShowHome(ProgressManager p)
        {
            EnsureBuilt();
            _progress = p;
            _currentScreen = ScreenId.Home;
            SetScreen(_homeScreen);
            UpdateHome(p);
        }

        public void ShowGarage(ProgressManager p)
        {
            EnsureBuilt();
            _progress = p;
            _currentScreen = ScreenId.Garage;
            SetScreen(_garageScreen);
            UpdateGarage(p);
        }

        public void ShowPlay(JobDef job, int step, int steps, string instruction, string toolLabel, JobTaskDef activeTask = null)
        {
            EnsureBuilt();
            _currentScreen = ScreenId.Play;
            _currentJobId = job != null ? job.id : _currentJobId;
            SetScreen(_playScreen);
            _pausePanel.SetActive(false);
            _customizationPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _toolBar.gameObject.SetActive(true);

            _playTitleText.text = job != null ? job.titleRu : "Задание";
            int safeSteps = Mathf.Max(1, steps);
            int safeStep = Mathf.Clamp(step, 1, safeSteps);
            _playTaskText.text = "Этап " + safeStep + " из " + safeSteps;
            _playInstructionText.text = string.IsNullOrEmpty(instruction) ? "" : instruction;
            SetHint(string.Empty);
            BuildToolButtons(job, toolLabel, activeTask, safeStep - 1);
        }

        public void HideTools()
        {
            EnsureBuilt();
            if (_toolBar != null) _toolBar.gameObject.SetActive(false);
            for (int i = 0; i < _toolButtons.Count; i++)
            {
                if (_toolButtons[i].Root != null) Destroy(_toolButtons[i].Root);
            }
            _toolButtons.Clear();
        }

        public void SetProgress(float value)
        {
            EnsureBuilt();
            float v = Mathf.Clamp01(value);
            if (_playProgressSlider != null) _playProgressSlider.SetValueWithoutNotify(v);
        }

        public void SetHint(string text)
        {
            EnsureBuilt();
            if (_playHintText != null) _playHintText.text = text ?? string.Empty;
        }

        public void ShowComplete(string title, string body, int reward)
        {
            EnsureBuilt();
            _currentScreen = ScreenId.Complete;
            SetScreen(_completeScreen);
            _pausePanel.SetActive(false);
            _customizationPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _completeTitleText.text = string.IsNullOrEmpty(title) ? "Готово!" : title;
            _completeBodyText.text = body ?? string.Empty;
            _completeRewardText.text = "Награда: " + Mathf.Max(0, reward);
        }

        public void ShowPause(bool v)
        {
            EnsureBuilt();
            if (_pausePanel != null) _pausePanel.SetActive(v);
            if (v)
            {
                _settingsPanel.SetActive(false);
                UpdateSoundButtonLabels();
            }
        }

        public void ShowCustomization(VehicleDef def)
        {
            EnsureBuilt();
            _customDef = def;
            _customizationPanel.SetActive(true);
            _customTitleText.text = "Кастомизация";
            BuildCustomizationOptions(def);
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            EnsureBuilt();
            if (_eventSystem == null) _eventSystem = EventSystem.current;
            if (_eventSystem == null) return false;
            if (_pointerEventData == null) _pointerEventData = new PointerEventData(_eventSystem);
            _pointerEventData.position = screenPosition;
            _raycastResults.Clear();
            _eventSystem.RaycastAll(_pointerEventData, _raycastResults);
            return _raycastResults.Count > 0;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;
            EnsureEventSystem();
            BuildCanvas();
            BuildScreens();
            WireStaticButtons();
            UpdateSoundButtonLabels();
            SetScreen(_homeScreen);
            ApplySafeArea(true);
        }

        private void EnsureEventSystem()
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(transform, false);
            _eventSystem = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("UI Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGo.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = MatchForCurrentScreen();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            _root = canvasGo.GetComponent<RectTransform>();
            Stretch(_root);

            var safeGo = new GameObject("SafeRoot");
            safeGo.transform.SetParent(_root, false);
            _safeRoot = safeGo.AddComponent<RectTransform>();
            Stretch(_safeRoot);

            _font = ResolveFont();
            _roundedSprite = GetRoundedSprite(24, 8);
            _roundedSoftSprite = GetRoundedSprite(16, 6);
            _circleSprite = GetCircleSprite(64);
        }

        private static float MatchForCurrentScreen()
        {
            float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
            // Portrait reference 1080x1920; landscape needs more width match.
            return aspect < 1f ? 0.5f : 0.2f;
        }

        private void ApplySafeAreaIfNeeded()
        {
            if (_safeRoot == null) return;
            Rect safe = Screen.safeArea;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            if (min == _lastSafeMin && max == _lastSafeMax) return;
            ApplySafeArea(true);
        }

        private void ApplySafeArea(bool force)
        {
            if (_safeRoot == null) return;
            Rect safe = Screen.safeArea;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            if (!force && min == _lastSafeMin && max == _lastSafeMax) return;
            _lastSafeMin = min;
            _lastSafeMax = max;

            min.x /= Mathf.Max(1f, Screen.width);
            min.y /= Mathf.Max(1f, Screen.height);
            max.x /= Mathf.Max(1f, Screen.width);
            max.y /= Mathf.Max(1f, Screen.height);
            _safeRoot.anchorMin = min;
            _safeRoot.anchorMax = max;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private void BuildScreens()
        {
            _homeScreen = CreateScreen("HomeScreen");
            _garageScreen = CreateScreen("GarageScreen");
            _playScreen = CreateScreen("PlayScreen");
            _completeScreen = CreateScreen("CompleteScreen");

            BuildHomeScreen();
            BuildGarageScreen();
            BuildPlayScreen();
            BuildCompleteScreen();
            BuildPausePanel();
            BuildCustomizationPanel();
            BuildSettingsPanel();
        }

        private GameObject CreateScreen(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_safeRoot, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Stretch(rt);
            return go;
        }

        private void SetScreen(GameObject active)
        {
            if (_homeScreen != null) _homeScreen.SetActive(active == _homeScreen);
            if (_garageScreen != null) _garageScreen.SetActive(active == _garageScreen);
            if (_playScreen != null) _playScreen.SetActive(active == _playScreen);
            if (_completeScreen != null) _completeScreen.SetActive(active == _completeScreen);
            if (_pausePanel != null && active != _playScreen) _pausePanel.SetActive(false);
            if (_customizationPanel != null && active != _playScreen) _customizationPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private void BuildHomeScreen()
        {
            Image bg = CreateImage("Bg", _homeScreen.transform, new Color(0.08f, 0.09f, 0.11f, 1f));
            Stretch(bg.rectTransform);

            VerticalLayoutGroup layout = CreateVertical(_homeScreen.transform, 28f, new RectOffset(48, 48, 64, 64));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateSpacer(layout.transform, 40f);
            Text title = CreateText("Title", layout.transform, "МАЛЕНЬКИЙ АВТОСЕРВИС", 68, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;

            Text subtitle = CreateText("Subtitle", layout.transform, "Мойка, ремонт и тюнинг машин", 34, FontStyle.Normal, _muted, TextAnchor.MiddleCenter);
            subtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;

            CreateSpacer(layout.transform, 30f);

            GameObject progressCard = CreatePanel("ProgressCard", layout.transform, _panel, 22);
            LayoutElement pcLe = progressCard.AddComponent<LayoutElement>();
            pcLe.preferredHeight = 180;
            VerticalLayoutGroup pcv = CreateVertical(progressCard.transform, 12f, new RectOffset(28, 28, 24, 24));
            _homeProgressText = CreateText("ProgressText", pcv.transform, "Прогресс: 0/10", 32, FontStyle.Normal, _text, TextAnchor.MiddleLeft);
            _homeProgressText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            _homeProgressSlider = CreateSlider(pcv.transform, _steel, _teal);
            _homeProgressSlider.interactable = false;
            _homeProgressSlider.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;

            CreateSpacer(layout.transform, 20f);

            _homePlayButton = CreateButton("PlayButton", layout.transform, "ИГРАТЬ", _teal, _text, 44, 140f);
            _homeSoundButton = CreateButton("SoundButton", layout.transform, "ЗВУК И НАСТРОЙКИ", _panelSoft, _text, 32, 110f);
            _homeSoundButtonText = _homeSoundButton.GetComponentInChildren<Text>();
        }

        private void BuildGarageScreen()
        {
            CreateImage("Bg", _garageScreen.transform, new Color(0.09f, 0.10f, 0.12f, 1f)).rectTransform.StretchFull();

            var topBar = CreatePanel("Top", _garageScreen.transform, _panel, 0).GetComponent<RectTransform>();
            AnchorTop(topBar, 0.12f);
            HorizontalLayoutGroup top = CreateHorizontal(topBar, 20f, new RectOffset(24, 24, 16, 16));
            top.childAlignment = TextAnchor.MiddleCenter;
            _garageBackButton = CreateButton("Back", top.transform, "НАЗАД", _steel, _text, 30, 96f, 220f);
            Text title = CreateText("Title", top.transform, "ВЫБОР ЗАДАНИЯ", 44, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1;
            _garageProgressText = CreateText("Progress", top.transform, "0/10", 32, FontStyle.Normal, _amber, TextAnchor.MiddleRight);
            _garageProgressText.gameObject.AddComponent<LayoutElement>().preferredWidth = 180;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(_garageScreen.transform, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0.88f);
            crt.offsetMin = new Vector2(24, 24);
            crt.offsetMax = new Vector2(-24, -12);

            ScrollRect scroll = content.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = CreatePanel("Viewport", content.transform, new Color(0, 0, 0, 0), 0).GetComponent<RectTransform>();
            Stretch(viewport);
            // viewport mask omitted in stub compile
            var mask = viewport.gameObject.GetComponent<UnityEngine.UI.RectMask2D>();
            if (mask == null) viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            scroll.viewport = viewport;

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(viewport, false);
            RectTransform lrt = list.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            VerticalLayoutGroup v = list.GetComponent<VerticalLayoutGroup>();
            v.spacing = 16f;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = list.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = lrt;

            _jobCards.Clear();
            // Cards are created on demand in UpdateGarage to match config; pre-create 10 placeholders.
            for (int i = 0; i < 10; i++)
            {
                _jobCards.Add(CreateJobCard(list.transform));
            }
        }

        private JobCard CreateJobCard(Transform parent)
        {
            GameObject root = CreatePanel("JobCard", parent, _panel, 18);
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 150;
            Button btn = root.AddComponent<Button>();
            btn.transition = Button.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            btn.colors = cb;

            HorizontalLayoutGroup h = CreateHorizontal(root.transform, 16f, new RectOffset(20, 20, 16, 16));
            h.childAlignment = TextAnchor.MiddleCenter;

            var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup));
            info.transform.SetParent(root.transform, false);
            VerticalLayoutGroup iv = info.GetComponent<VerticalLayoutGroup>();
            iv.spacing = 6f;
            iv.childControlWidth = true;
            iv.childControlHeight = true;
            iv.childForceExpandWidth = true;
            iv.childForceExpandHeight = false;
            LayoutElement infoLe = info.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1;

            Text title = CreateText("Title", info.transform, "", 34, FontStyle.Bold, _text, TextAnchor.MiddleLeft);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            Text desc = CreateText("Desc", info.transform, "", 26, FontStyle.Normal, _muted, TextAnchor.MiddleLeft);
            desc.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            Text stars = CreateText("Stars", info.transform, "", 28, FontStyle.Normal, _amber, TextAnchor.MiddleLeft);
            stars.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            GameObject lockOverlay = CreatePanel("Lock", root.transform, new Color(0f, 0f, 0f, 0.35f), 18);
            LayoutElement lockLe = lockOverlay.AddComponent<LayoutElement>();
            lockLe.ignoreLayout = true;
            Stretch(lockOverlay.GetComponent<RectTransform>());
            Text lockText = CreateText("LockText", lockOverlay.transform, "ЗАКРЫТО", 30, FontStyle.Bold, _muted, TextAnchor.MiddleCenter);
            Stretch(lockText.rectTransform);

            return new JobCard
            {
                Root = root,
                Button = btn,
                Title = title,
                Description = desc,
                Stars = stars,
                LockOverlay = lockOverlay.GetComponent<Image>(),
                LockText = lockText
            };
        }

        private void BuildPlayScreen()
        {
            // Transparent screen; car is rendered by 3D camera behind canvas.
            Image bg = CreateImage("Bg", _playScreen.transform, new Color(0, 0, 0, 0));
            Stretch(bg.rectTransform);

            // Top bar <= 15%
            GameObject top = CreatePanel("TopBar", _playScreen.transform, new Color(_panel.r, _panel.g, _panel.b, 0.82f), 0);
            RectTransform topRt = top.GetComponent<RectTransform>();
            AnchorTop(topRt, 0.14f);
            HorizontalLayoutGroup th = CreateHorizontal(topRt, 12f, new RectOffset(20, 20, 12, 12));
            th.childAlignment = TextAnchor.MiddleCenter;

            VerticalLayoutGroup info = CreateVertical(topRt, 2f, new RectOffset(0, 0, 0, 0));
            LayoutElement infoLe = info.gameObject.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1;
            _playTitleText = CreateText("Title", info.transform, "", 34, FontStyle.Bold, _text, TextAnchor.MiddleLeft);
            _playTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            _playTaskText = CreateText("Task", info.transform, "", 24, FontStyle.Normal, _muted, TextAnchor.MiddleLeft);
            _playTaskText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            _playPauseButton = CreateButton("Pause", topRt, "ПАУЗА", _steel, _text, 28, 96f, 180f);

            // Instruction strip under top bar, still within safe HUD.
            GameObject instr = CreatePanel("Instruction", _playScreen.transform, new Color(0.12f, 0.13f, 0.15f, 0.55f), 12);
            RectTransform irt = instr.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.08f, 0.80f);
            irt.anchorMax = new Vector2(0.92f, 0.86f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            _playInstructionText = CreateText("InstructionText", instr.transform, "", 28, FontStyle.Normal, _text, TextAnchor.MiddleCenter);
            Stretch(_playInstructionText.rectTransform);
            _playInstructionText.raycastTarget = false;

            // Hint near bottom of center, above toolbar.
            GameObject hint = CreatePanel("Hint", _playScreen.transform, new Color(0, 0, 0, 0), 0);
            RectTransform hrt = hint.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.08f, 0.22f);
            hrt.anchorMax = new Vector2(0.92f, 0.27f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            _playHintText = CreateText("HintText", hint.transform, "", 24, FontStyle.Normal, _amber, TextAnchor.MiddleCenter);
            Stretch(_playHintText.rectTransform);
            _playHintText.raycastTarget = false;

            // Bottom toolbar <= 20%
            GameObject bottom = CreatePanel("BottomBar", _playScreen.transform, new Color(_panel.r, _panel.g, _panel.b, 0.86f), 0);
            RectTransform brt = bottom.GetComponent<RectTransform>();
            AnchorBottom(brt, 0.20f);

            VerticalLayoutGroup bv = CreateVertical(brt, 10f, new RectOffset(16, 16, 12, 12));
            bv.childAlignment = TextAnchor.UpperCenter;
            bv.childControlHeight = true;
            bv.childControlWidth = true;
            bv.childForceExpandHeight = false;
            bv.childForceExpandWidth = true;

            _playProgressSlider = CreateSlider(bv.transform, _steel, _amber);
            _playProgressSlider.interactable = false;
            _playProgressSlider.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            GameObject tools = new GameObject("Tools", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tools.transform.SetParent(bottom.transform, false);
            _toolBar = tools.GetComponent<RectTransform>();
            HorizontalLayoutGroup hg = tools.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 10f;
            hg.padding = new RectOffset(4, 4, 0, 0);
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childControlWidth = true;
            hg.childControlHeight = true;
            hg.childForceExpandWidth = true;
            hg.childForceExpandHeight = true;
            LayoutElement tle = tools.AddComponent<LayoutElement>();
            tle.flexibleHeight = 1;
            tle.minHeight = 110;
        }

        private void BuildCompleteScreen()
        {
            CreateImage("Bg", _completeScreen.transform, new Color(0.06f, 0.07f, 0.09f, 0.88f)).rectTransform.StretchFull();
            GameObject card = CreatePanel("Card", _completeScreen.transform, _panel, 24);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.08f, 0.18f);
            crt.anchorMax = new Vector2(0.92f, 0.82f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            VerticalLayoutGroup v = CreateVertical(crt, 18f, new RectOffset(32, 32, 32, 32));
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            _completeTitleText = CreateText("Title", v.transform, "Готово!", 58, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            _completeTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
            _completeBodyText = CreateText("Body", v.transform, "", 32, FontStyle.Normal, _muted, TextAnchor.MiddleCenter);
            _completeBodyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            _completeRewardText = CreateText("Reward", v.transform, "", 36, FontStyle.Bold, _amber, TextAnchor.MiddleCenter);
            _completeRewardText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;

            CreateSpacer(v.transform, 20f);
            _completeNextButton = CreateButton("Next", v.transform, "СЛЕДУЮЩИЙ ЗАКАЗ", _teal, _text, 34, 110f);
            _completeReplayButton = CreateButton("Replay", v.transform, "ПОВТОРИТЬ", _steel, _text, 32, 100f);
            _completeHomeButton = CreateButton("Home", v.transform, "ДОМОЙ", _panelSoft, _text, 32, 100f);
        }

        private void BuildPausePanel()
        {
            _pausePanel = CreatePanel("PausePanel", _playScreen.transform, new Color(0f, 0f, 0f, 0.72f), 0);
            Stretch(_pausePanel.GetComponent<RectTransform>());
            _pausePanel.SetActive(false);

            GameObject card = CreatePanel("Card", _pausePanel.transform, _panel, 20);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.12f, 0.22f);
            crt.anchorMax = new Vector2(0.88f, 0.78f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            VerticalLayoutGroup v = CreateVertical(crt, 14f, new RectOffset(28, 28, 28, 28));
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            CreateText("PauseTitle", v.transform, "ПАУЗА", 52, FontStyle.Bold, _text, TextAnchor.MiddleCenter).gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            _pauseResumeButton = CreateButton("Resume", v.transform, "ПРОДОЛЖИТЬ", _teal, _text, 32, 100f);
            _pauseRestartButton = CreateButton("Restart", v.transform, "ЗАНОВО", _steel, _text, 32, 100f);
            _pauseHomeButton = CreateButton("Home", v.transform, "ДОМОЙ", _panelSoft, _text, 32, 100f);
            _pauseSoundButton = CreateButton("Sound", v.transform, "НАСТРОЙКИ ЗВУКА", _panelSoft, _text, 30, 96f);
            _pauseSoundButtonText = _pauseSoundButton.GetComponentInChildren<Text>();
        }

        private void BuildCustomizationPanel()
        {
            _customizationPanel = CreatePanel("CustomizationPanel", _playScreen.transform, new Color(0.07f, 0.08f, 0.10f, 0.72f), 0);
            RectTransform rt = _customizationPanel.GetComponent<RectTransform>();
            // Keep center clear: panel sits on bottom and sides via margins; use bottom sheet style.
            rt.anchorMin = new Vector2(0.03f, 0.02f);
            rt.anchorMax = new Vector2(0.97f, 0.36f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _customizationPanel.SetActive(false);

            VerticalLayoutGroup v = CreateVertical(rt, 10f, new RectOffset(20, 20, 16, 16));
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _customTitleText = CreateText("Title", v.transform, "Кастомизация", 38, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            _customTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

            CreateText("ColorLabel", v.transform, "Цвет кузова", 26, FontStyle.Normal, _muted, TextAnchor.MiddleLeft).gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            _colorRow = CreateRow(v.transform, 72f);
            CreateText("RimLabel", v.transform, "Диски", 26, FontStyle.Normal, _muted, TextAnchor.MiddleLeft).gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            _rimRow = CreateRow(v.transform, 72f);
            CreateText("DecalLabel", v.transform, "Наклейки", 26, FontStyle.Normal, _muted, TextAnchor.MiddleLeft).gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            _decalRow = CreateRow(v.transform, 72f);

            _customCommitButton = CreateButton("Commit", v.transform, "ГОТОВО", _teal, _text, 32, 96f);
        }

        private RectTransform CreateRow(Transform parent, float height)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return row.GetComponent<RectTransform>();
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = CreatePanel("SettingsPanel", _safeRoot, new Color(0f, 0f, 0f, 0.72f), 0);
            Stretch(_settingsPanel.GetComponent<RectTransform>());
            _settingsPanel.SetActive(false);

            GameObject card = CreatePanel("Card", _settingsPanel.transform, _panel, 20);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.1f, 0.18f);
            crt.anchorMax = new Vector2(0.9f, 0.82f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            VerticalLayoutGroup v = CreateVertical(crt, 14f, new RectOffset(28, 28, 28, 28));
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            CreateText("Title", v.transform, "НАСТРОЙКИ ЗВУКА", 42, FontStyle.Bold, _text, TextAnchor.MiddleCenter)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 64;

            _musicToggle = CreateToggleRow(v.transform, "Музыка", out _musicSlider);
            _sfxToggle = CreateToggleRow(v.transform, "Эффекты", out _sfxSlider);

            CreateSpacer(v.transform, 16f);
            _settingsCloseButton = CreateButton("Close", v.transform, "ЗАКРЫТЬ", _teal, _text, 32, 96f);
        }

        private Toggle CreateToggleRow(Transform parent, string label, out Slider slider)
        {
            GameObject row = CreatePanel(label + "Row", parent, _panelSoft, 12);
            row.AddComponent<LayoutElement>().preferredHeight = 150;
            VerticalLayoutGroup v = CreateVertical(row.transform, 8f, new RectOffset(16, 16, 12, 12));
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            GameObject top = new GameObject("Top", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            top.transform.SetParent(row.transform, false);
            top.AddComponent<LayoutElement>().preferredHeight = 52;
            HorizontalLayoutGroup h = top.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;

            Text t = CreateText("Label", top.transform, label, 30, FontStyle.Normal, _text, TextAnchor.MiddleLeft);
            LayoutElement tle = t.gameObject.AddComponent<LayoutElement>();
            tle.flexibleWidth = 1;

            Toggle toggle = CreateToggle(top.transform);
            LayoutElement tgle = toggle.gameObject.AddComponent<LayoutElement>();
            tgle.preferredWidth = 96;
            tgle.preferredHeight = 52;

            slider = CreateSlider(row.transform, _steel, _teal);
            slider.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            return toggle;
        }

        private void WireStaticButtons()
        {
            _homePlayButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                ShowGarage(_progress);
                GarageRequested?.Invoke();
            });
            _homeSoundButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                OpenSettings();
            });

            _garageBackButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                HomeRequested?.Invoke();
            });

            _playPauseButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                PauseRequested?.Invoke();
            });

            _completeNextButton.onClick.AddListener(() => { PlayUiClick(); NextRequested?.Invoke(); });
            _completeReplayButton.onClick.AddListener(() => { PlayUiClick(); RestartRequested?.Invoke(); });
            _completeHomeButton.onClick.AddListener(() => { PlayUiClick(); HomeRequested?.Invoke(); });

            _pauseResumeButton.onClick.AddListener(() => { PlayUiClick(); ResumeRequested?.Invoke(); });
            _pauseRestartButton.onClick.AddListener(() => { PlayUiClick(); RestartRequested?.Invoke(); });
            _pauseHomeButton.onClick.AddListener(() => { PlayUiClick(); HomeRequested?.Invoke(); });
            _pauseSoundButton.onClick.AddListener(() => { PlayUiClick(); OpenSettings(); });

            _customCommitButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                _customizationPanel.SetActive(false);
                CustomizationCommitted?.Invoke();
            });

            _settingsCloseButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                _settingsPanel.SetActive(false);
            });

            _musicToggle.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetBusEnabled("Music", v);
                UpdateSoundButtonLabels();
            });
            _sfxToggle.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetBusEnabled("UI", v);
                    AudioManager.Instance.SetBusEnabled("Washing", v);
                    AudioManager.Instance.SetBusEnabled("Characters", v);
                    AudioManager.Instance.SetBusEnabled("Ambient", v);
                }
                UpdateSoundButtonLabels();
            });
            _musicSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolume("Music", v);
            });
            _sfxSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetVolume("UI", v);
                    AudioManager.Instance.SetVolume("Washing", v);
                    AudioManager.Instance.SetVolume("Characters", v);
                    AudioManager.Instance.SetVolume("Ambient", v);
                }
            });
        }

        private void OpenSettings()
        {
            SyncSettingsUI();
            _settingsPanel.SetActive(true);
            _settingsPanel.transform.SetAsLastSibling();
        }

        private void SyncSettingsUI()
        {
            if (AudioManager.Instance == null) return;
            _musicToggle.SetIsOnWithoutNotify(AudioManager.Instance.GetBusEnabled("Music"));
            _sfxToggle.SetIsOnWithoutNotify(AudioManager.Instance.GetBusEnabled("UI"));
            _musicSlider.SetValueWithoutNotify(AudioManager.Instance.GetVolume("Music"));
            _sfxSlider.SetValueWithoutNotify(AudioManager.Instance.GetVolume("UI"));
        }

        private void UpdateSoundButtonLabels()
        {
            bool on = true;
            if (AudioManager.Instance != null)
                on = AudioManager.Instance.GetBusEnabled("Music") || AudioManager.Instance.GetBusEnabled("UI");
            string label = on ? "ЗВУК И НАСТРОЙКИ" : "ЗВУК: ВЫКЛ";
            if (_homeSoundButtonText != null) _homeSoundButtonText.text = label;
            if (_pauseSoundButtonText != null) _pauseSoundButtonText.text = "НАСТРОЙКИ ЗВУКА";
        }

        private void PlayUiClick()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click");
        }

        private void UpdateHome(ProgressManager p)
        {
            int completed = p != null ? p.CompletedJobs : 0;
            int total = 10;
            GameConfig cfg = ResolveConfig();
            if (cfg != null && cfg.jobs != null && cfg.jobs.Length > 0) total = cfg.jobs.Length;
            _homeProgressText.text = "Прогресс: " + completed + "/" + total;
            _homeProgressSlider.SetValueWithoutNotify(total > 0 ? (float)completed / total : 0f);
            UpdateSoundButtonLabels();
        }

        private void UpdateGarage(ProgressManager p)
        {
            GameConfig cfg = ResolveConfig();
            JobDef[] jobs = cfg != null ? cfg.jobs : null;
            int count = jobs != null ? jobs.Length : 0;
            int completed = p != null ? p.CompletedJobs : 0;
            _garageProgressText.text = completed + "/" + Mathf.Max(1, count);

            for (int i = 0; i < _jobCards.Count; i++)
            {
                bool active = i < count;
                JobCard card = _jobCards[i];
                card.Root.SetActive(active);
                if (!active) continue;
                JobDef job = jobs[i];
                bool unlocked = p == null || p.IsUnlocked(job.id);
                bool done = p != null && p.IsCompleted(job.id);
                card.JobId = job.id;
                card.Title.text = (i + 1) + ". " + (job != null ? job.titleRu : "Задание");
                card.Description.text = job != null ? job.descriptionRu : string.Empty;
                card.Stars.text = done ? "★ Выполнено" : (unlocked ? "○ Доступно" : "○ Закрыто");
                card.LockOverlay.gameObject.SetActive(!unlocked);
                card.LockText.text = "ЗАКРЫТО";
                card.Button.interactable = unlocked;
                card.Button.onClick.RemoveAllListeners();
                string jid = job.id;
                card.Button.onClick.AddListener(() =>
                {
                    if (p != null && !p.IsUnlocked(jid)) return;
                    PlayUiClick();
                    _currentJobId = jid;
                    JobSelected?.Invoke(jid);
                });
            }
        }

        private GameConfig ResolveConfig()
        {
            // Best-effort; UI should not depend on GameManager internals.
            // GameManager passes ProgressManager; config is accessed via Resources/default.
            GameConfig cfg = Resources.Load<GameConfig>("LittleGarage/GameConfig");
            if (cfg == null) cfg = GameConfig.CreateDefault();
            return cfg;
        }

        private void BuildToolButtons(JobDef job, string selectedLabel, JobTaskDef activeTask = null, int taskIndex = -1)
        {
            for (int i = 0; i < _toolButtons.Count; i++)
            {
                if (_toolButtons[i].Root != null) Destroy(_toolButtons[i].Root);
            }
            _toolButtons.Clear();
            if (_toolBar == null) return;

            List<string> labels = new List<string>(6);
            List<string> ids = new List<string>(6);
            List<Color> colors = new List<Color>(6);

            if (activeTask == null && job != null && job.tasks != null && taskIndex >= 0 && taskIndex < job.tasks.Length)
                activeTask = job.tasks[taskIndex];

            if (job != null && job.tasks != null)
            {
                JobTaskDef task = activeTask != null ? activeTask : FindCurrentTask(job);
                if (task != null && task.kind == TaskKind.Wash && task.tools != null)
                {
                    for (int i = 0; i < task.tools.Length; i++)
                    {
                        WashTool t = task.tools[i];
                        ids.Add(t.ToString());
                        labels.Add(ToolLabel(t));
                        colors.Add(ToolColor(t));
                    }
                }
                else if (task != null && task.kind == TaskKind.Repair)
                {
                    ids.Add("Repair");
                    labels.Add("РЕМОНТ");
                    colors.Add(_steel);
                }
                else if (task != null && (task.kind == TaskKind.Paint || task.kind == TaskKind.Customize))
                {
                    ids.Add("Customize");
                    labels.Add(task.kind == TaskKind.Paint ? "ПОКРАСКА" : "ТЮНИНГ");
                    colors.Add(_amber);
                }
            }

            if (labels.Count == 0)
            {
                ids.Add("Tool");
                labels.Add(string.IsNullOrEmpty(selectedLabel) ? "ИНСТРУМЕНТ" : selectedLabel);
                colors.Add(_steel);
            }

            int selectedIndex = -1;
            if (!string.IsNullOrEmpty(selectedLabel))
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (string.Equals(ids[i], selectedLabel, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(labels[i], selectedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            for (int i = 0; i < labels.Count; i++)
            {
                ToolButton tb = CreateToolButton(_toolBar, labels[i], ids[i], colors[i]);
                bool locked = selectedIndex >= 0 && i > selectedIndex;
                tb.Button.interactable = !locked;
                SetToolSelectedVisual(tb, i == selectedIndex || (selectedIndex < 0 && i == 0));
                if (locked && tb.Background != null)
                    tb.Background.color = new Color(_panelSoft.r * 0.55f, _panelSoft.g * 0.55f, _panelSoft.b * 0.55f, 0.9f);
                _toolButtons.Add(tb);
            }
        }

        private JobTaskDef FindCurrentTask(JobDef job)
        {
            // ShowPlay is called with step info; we don't store step index, but instruction/toolLabel
            // are passed. Use first matching task by tool/instruction heuristic: prefer active task from text.
            if (job == null || job.tasks == null || job.tasks.Length == 0) return null;
            string instr = _playInstructionText != null ? _playInstructionText.text : null;
            if (!string.IsNullOrEmpty(instr))
            {
                for (int i = 0; i < job.tasks.Length; i++)
                {
                    JobTaskDef t = job.tasks[i];
                    if (t != null && string.Equals(t.instructionRu, instr, StringComparison.Ordinal)) return t;
                }
            }
            for (int i = 0; i < job.tasks.Length; i++) if (job.tasks[i] != null) return job.tasks[i];
            return null;
        }

        private ToolButton CreateToolButton(Transform parent, string label, string id, Color accent)
        {
            GameObject root = CreatePanel("Tool_" + id, parent, _panelSoft, 14);
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.minHeight = 96;
            le.preferredHeight = 110;
            le.flexibleWidth = 1;

            Button btn = root.AddComponent<Button>();
            btn.transition = Button.Transition.ColorTint;
            Image bg = root.GetComponent<Image>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            cb.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            btn.colors = cb;

            VerticalLayoutGroup v = CreateVertical(root.transform, 4f, new RectOffset(8, 8, 10, 10));
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var iconWrap = new GameObject("IconWrap", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            iconWrap.transform.SetParent(v.transform, false);
            iconWrap.GetComponent<LayoutElement>().preferredHeight = 36;
            HorizontalLayoutGroup ih = iconWrap.GetComponent<HorizontalLayoutGroup>();
            ih.childAlignment = TextAnchor.MiddleCenter;
            ih.childControlWidth = false;
            ih.childControlHeight = false;
            ih.childForceExpandWidth = true;
            ih.childForceExpandHeight = true;
            Image iconImg = CreateImage("Icon", iconWrap.transform, accent);
            iconImg.sprite = _circleSprite;
            iconImg.rectTransform.sizeDelta = new Vector2(30, 30);

            Text t = CreateText("Label", v.transform, label, 22, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = 24;

            ToolButton tb = new ToolButton { Root = root, Button = btn, Background = bg, Label = t, ToolId = id };
            btn.onClick.AddListener(() =>
            {
                PlayUiClick();
                for (int i = 0; i < _toolButtons.Count; i++) SetToolSelectedVisual(_toolButtons[i], _toolButtons[i] == tb);
                ToolSelected?.Invoke(tb.ToolId);
            });
            return tb;
        }

        private void SetToolSelectedVisual(ToolButton tb, bool selected)
        {
            if (tb == null || tb.Background == null) return;
            tb.Background.color = selected ? new Color(_teal.r, _teal.g, _teal.b, 0.95f) : _panelSoft;
            if (tb.Label != null) tb.Label.color = selected ? Color.white : _text;
        }

        private static string ToolLabel(WashTool tool)
        {
            switch (tool)
            {
                case WashTool.Pressure: return "СТРУЯ";
                case WashTool.Foam: return "ПЕНА";
                case WashTool.Sponge: return "ГУБКА";
                case WashTool.Rinse: return "СМЫВ";
                case WashTool.Dryer: return "СУШКА";
                case WashTool.Polisher: return "ПОЛИРОВКА";
                default: return tool.ToString().ToUpperInvariant();
            }
        }

        private Color ToolColor(WashTool tool)
        {
            switch (tool)
            {
                case WashTool.Pressure: return new Color(0.35f, 0.68f, 0.86f, 1f);
                case WashTool.Foam: return new Color(0.85f, 0.90f, 0.95f, 1f);
                case WashTool.Sponge: return new Color(0.90f, 0.74f, 0.35f, 1f);
                case WashTool.Rinse: return new Color(0.45f, 0.78f, 0.90f, 1f);
                case WashTool.Dryer: return new Color(0.95f, 0.86f, 0.55f, 1f);
                case WashTool.Polisher: return new Color(0.72f, 0.80f, 0.92f, 1f);
                default: return _steel;
            }
        }

        private void BuildCustomizationOptions(VehicleDef def)
        {
            ClearRow(_colorRow, _colorButtons);
            ClearRow(_rimRow, _rimButtons);
            ClearRow(_decalRow, _decalButtons);

            Color[] colors = def != null && def.colors != null && def.colors.Length > 0 ? def.colors : DefaultColors();
            for (int i = 0; i < colors.Length; i++)
            {
                int idx = i;
                Button b = CreateSwatchButton(_colorRow, colors[i], "Цвет " + (i + 1));
                b.onClick.AddListener(() => { PlayUiClick(); ColorSelected?.Invoke(idx); HighlightSelection(_colorButtons, idx); });
                _colorButtons.Add(b);
            }
            HighlightSelection(_colorButtons, 0);

            string[] rims = def != null && def.rimStyles != null && def.rimStyles.Length > 0 ? def.rimStyles : new[] { "Стандарт", "Спорт", "Хром" };
            for (int i = 0; i < rims.Length; i++)
            {
                int idx = i;
                Button b = CreateTextOptionButton(_rimRow, rims[i]);
                b.onClick.AddListener(() => { PlayUiClick(); RimSelected?.Invoke(idx); HighlightSelection(_rimButtons, idx); });
                _rimButtons.Add(b);
            }
            HighlightSelection(_rimButtons, 0);

            string[] decals = def != null && def.decals != null && def.decals.Length > 0 ? def.decals : new[] { "Без наклейки", "Полоса", "Звезда" };
            for (int i = 0; i < decals.Length; i++)
            {
                int idx = i;
                Button b = CreateTextOptionButton(_decalRow, decals[i]);
                b.onClick.AddListener(() => { PlayUiClick(); DecalSelected?.Invoke(idx); HighlightSelection(_decalButtons, idx); });
                _decalButtons.Add(b);
            }
            HighlightSelection(_decalButtons, 0);
        }

        private static Color[] DefaultColors()
        {
            return new[]
            {
                new Color(0.85f, 0.23f, 0.20f, 1f),
                new Color(0.22f, 0.52f, 0.85f, 1f),
                new Color(0.25f, 0.70f, 0.45f, 1f),
                new Color(0.95f, 0.78f, 0.22f, 1f),
                new Color(0.90f, 0.90f, 0.90f, 1f),
            };
        }

        private void ClearRow(RectTransform row, List<Button> buttons)
        {
            if (row == null) return;
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            buttons.Clear();
        }

        private Button CreateSwatchButton(Transform parent, Color color, string label)
        {
            GameObject root = CreatePanel("Swatch", parent, _panelSoft, 12);
            Button btn = root.AddComponent<Button>();
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.preferredHeight = 72;
            le.flexibleWidth = 1;

            Image swatch = CreateImage("Color", root.transform, color);
            RectTransform srt = swatch.rectTransform;
            srt.anchorMin = new Vector2(0.15f, 0.15f);
            srt.anchorMax = new Vector2(0.85f, 0.85f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            swatch.sprite = _roundedSoftSprite;
            swatch.type = Image.Type.Sliced;

            Text t = CreateText("Label", root.transform, label, 16, FontStyle.Normal, Color.clear, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return btn;
        }

        private Button CreateTextOptionButton(Transform parent, string label)
        {
            Button btn = CreateButton(label, parent, label, _panelSoft, _text, 22, 72f);
            Text t = btn.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 14;
                t.resizeTextMaxSize = 24;
            }
            return btn;
        }

        private void HighlightSelection(List<Button> buttons, int selected)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                Image img = buttons[i].GetComponent<Image>();
                if (img != null) img.color = i == selected ? _teal : _panelSoft;
                Text t = buttons[i].GetComponentInChildren<Text>();
                if (t != null && t.color.a > 0.5f) t.color = i == selected ? Color.white : _text;
            }
        }

        // --- UI helpers ---

        private Font ResolveFont()
        {
            string[] names = { "Arial", "Segoe UI", "Roboto", "Noto Sans", "DejaVu Sans", "Liberation Sans", "Tahoma" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    Font f = Font.CreateDynamicFontFromOSFont(names[i], 32);
                    if (f != null && f.HasCharacter('Ж')) return f;
                    if (f != null) Destroy(f);
                }
                catch { }
            }
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return Font.CreateDynamicFontFromOSFont("Arial", 32);
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static void AnchorTop(RectTransform rt, float height01)
        {
            rt.anchorMin = new Vector2(0f, 1f - Mathf.Clamp01(height01));
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorBottom(RectTransform rt, float height01)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, Mathf.Clamp01(height01));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color, int radius)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = radius >= 16 ? _roundedSprite : _roundedSoftSprite;
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return go;
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text CreateText(string name, Transform parent, string content, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content ?? string.Empty;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.supportRichText = false;
            return t;
        }

        private Button CreateButton(string name, Transform parent, string label, Color bg, Color fg, int fontSize, float height, float preferredWidth = -1f)
        {
            GameObject go = CreatePanel(name, parent, bg, 14);
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            cb.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.targetGraphic = go.GetComponent<Image>();

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = Mathf.Max(96f, height * 0.8f);
            if (preferredWidth > 0f) le.preferredWidth = preferredWidth;

            Text t = CreateText("Label", go.transform, label, fontSize, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.raycastTarget = false;
            return btn;
        }

        private Slider CreateSlider(Transform parent, Color bg, Color fill)
        {
            GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 32f);

            Image bgImg = CreateImage("Background", root.transform, bg);
            bgImg.sprite = _roundedSoftSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.raycastTarget = true;
            Stretch(bgImg.rectTransform);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fa = fillArea.GetComponent<RectTransform>();
            fa.anchorMin = new Vector2(0f, 0f);
            fa.anchorMax = new Vector2(1f, 1f);
            fa.offsetMin = new Vector2(6, 6);
            fa.offsetMax = new Vector2(-6, -6);

            Image fillImg = CreateImage("Fill", fillArea.transform, fill);
            fillImg.sprite = _roundedSoftSprite;
            fillImg.type = Image.Type.Sliced;
            RectTransform fr = fillImg.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            Image handleImg = CreateImage("Handle", handleArea.transform, Color.white);
            handleImg.sprite = _circleSprite;
            handleImg.raycastTarget = true;
            handleImg.rectTransform.sizeDelta = new Vector2(28, 28);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fr;
            slider.handleRect = handleImg.rectTransform;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }

        private Toggle CreateToggle(Transform parent)
        {
            GameObject root = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            Image bg = CreateImage("Background", root.transform, _steel);
            bg.sprite = _roundedSoftSprite;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            Image check = CreateImage("Checkmark", root.transform, _teal);
            check.sprite = _roundedSoftSprite;
            check.type = Image.Type.Sliced;
            RectTransform crt = check.rectTransform;
            crt.anchorMin = new Vector2(0.1f, 0.15f);
            crt.anchorMax = new Vector2(0.9f, 0.85f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            Toggle t = root.GetComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = check;
            t.isOn = true;
            return t;
        }

        private static VerticalLayoutGroup CreateVertical(Transform parent, float spacing, RectOffset padding)
        {
            // parent may already be the object we want; if it has no layout, add.
            VerticalLayoutGroup v = parent.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        private static HorizontalLayoutGroup CreateHorizontal(Transform parent, float spacing, RectOffset padding)
        {
            HorizontalLayoutGroup h = parent.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        // --- Procedural sprites ---

        private Sprite GetRoundedSprite(int radius, int border)
        {
            string key = "r_" + radius + "_" + border;
            Sprite s;
            if (_spriteCache.TryGetValue(key, out s) && s != null) return s;
            int size = Mathf.Max(32, radius * 4);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float r = radius;
            float cx1 = r, cx2 = size - r - 1, cy1 = r, cy2 = size - r - 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < cx1) dx = cx1 - x;
                    else if (x > cx2) dx = x - cx2;
                    if (y < cy1) dy = cy1 - y;
                    else if (y > cy2) dy = y - cy2;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    byte ba = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, ba);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            int b = Mathf.Clamp(border, 1, size / 2 - 1);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.name = key;
            _spriteCache[key] = s;
            return s;
        }

        private Sprite GetCircleSprite(int size)
        {
            string key = "c_" + size;
            Sprite s;
            if (_spriteCache.TryGetValue(key, out s) && s != null) return s;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    byte ba = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, ba);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = key;
            _spriteCache[key] = s;
            return s;
        }
    }

    internal static class RectTransformExt
    {
        public static void StretchFull(this RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}