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
        private CanvasScaler _scaler;
        private RectTransform _root;
        private RectTransform _safeRoot;
        private EventSystem _eventSystem;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
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
        private RectTransform _garageContent;

        // Play HUD
        private Text _playTitleText;
        private Text _playTaskText;
        private Text _playInstructionText;
        private Text _playHintText;
        private Slider _playProgressSlider;
        private Button _playPauseButton;
        private GameObject _topBar;
        private GameObject _instructionPanel;
        private GameObject _hintPanel;
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
        private RectTransform _customContent;
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

        // Single authoritative UI visibility state. Every Show*/Hide* method goes through ApplyVisibility.
        private ScreenId _currentScreen = ScreenId.Home;
        private bool _pauseVisible;
        private bool _customizationVisible;
        private bool _settingsVisible;
        private bool _hudVisible;
        private bool _toolsVisible;
        private UiOverlay _settingsReturnOverlay = UiOverlay.None;

        /// <summary>Read-only view of the primary screen; useful for QA and tests.</summary>
        public ScreenId CurrentScreen => _currentScreen;

        /// <summary>Returns the active primary screen only when exactly one is active.</summary>
        public ScreenId? ActivePrimaryScreen
        {
            get
            {
                int count = 0;
                ScreenId found = ScreenId.Home;
                if (IsActive(_homeScreen)) { count++; found = ScreenId.Home; }
                if (IsActive(_garageScreen)) { count++; found = ScreenId.Garage; }
                if (IsActive(_playScreen)) { count++; found = ScreenId.Play; }
                if (IsActive(_completeScreen)) { count++; found = ScreenId.Complete; }
                return count == 1 ? found : (ScreenId?)null;
            }
        }

        public UiOverlay ActiveOverlay
        {
            get
            {
                if (_settingsVisible) return UiOverlay.Settings;
                if (_pauseVisible) return UiOverlay.Pause;
                if (_customizationVisible) return UiOverlay.Customization;
                return UiOverlay.None;
            }
        }

        public bool IsOverlayActive(UiOverlay overlay)
        {
            return ActiveOverlay == overlay;
        }

        public bool IsPrimaryScreenActive(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.Home: return IsActive(_homeScreen);
                case ScreenId.Garage: return IsActive(_garageScreen);
                case ScreenId.Play: return IsActive(_playScreen);
                case ScreenId.Complete: return IsActive(_completeScreen);
                default: return false;
            }
        }

        public int ActiveToolButtonCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _toolButtons.Count; i++)
                    if (_toolButtons[i] != null && IsActive(_toolButtons[i].Root)) count++;
                return count;
            }
        }

        public int VisibleJobCardCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _jobCards.Count; i++)
                    if (IsActive(_jobCards[i] != null ? _jobCards[i].Root : null)) count++;
                return count;
            }
        }

        public float ScalerMatch => _scaler != null ? _scaler.matchWidthOrHeight : UiLayoutMetrics.ScalerMatch;

        public Vector2 ScalerReferenceResolution => _scaler != null
            ? _scaler.referenceResolution
            : UiLayoutMetrics.ReferenceResolution;

        private static bool IsActive(GameObject go)
        {
            // activeSelf is true for children of a deactivated screen; QA/test
            // counters must report what is actually visible and hittable.
            return go != null && go.activeInHierarchy;
        }

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
            public Image Icon;
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
            ApplyScalerIfNeeded();
            ApplySafeAreaIfNeeded();
        }

        public void ShowHome(ProgressManager p)
        {
            EnsureBuilt();
            _progress = p;
            ApplyVisibility(ScreenId.Home, false, false, false, false, false);
            UpdateHome(p);
        }

        public void ShowGarage(ProgressManager p)
        {
            EnsureBuilt();
            _progress = p;
            ApplyVisibility(ScreenId.Garage, false, false, false, false, false);
            UpdateGarage(p);
        }

        public void ShowPlay(JobDef job, int step, int steps, string instruction, string toolLabel, JobTaskDef activeTask = null)
        {
            EnsureBuilt();
            _currentJobId = job != null ? job.id : _currentJobId;
            ApplyVisibility(ScreenId.Play, false, false, false, true, true);

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
            // Finale keeps the primary Play screen for the 3D showcase but removes gameplay chrome.
            if (_currentScreen == ScreenId.Play)
                ApplyVisibility(ScreenId.Play, false, false, false, false, false);
            ClearToolButtons();
        }

        private void ClearToolButtons()
        {
            for (int i = 0; i < _toolButtons.Count; i++)
            {
                GameObject root = _toolButtons[i] != null ? _toolButtons[i].Root : null;
                if (root == null) continue;
                root.transform.SetParent(null, false);
                Destroy(root);
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
            ApplyVisibility(ScreenId.Complete, false, false, false, false, false);
            _completeTitleText.text = string.IsNullOrEmpty(title) ? "Готово!" : title;
            _completeBodyText.text = body ?? string.Empty;
            _completeRewardText.text = "Награда: " + Mathf.Max(0, reward);
        }

        public void ShowPause(bool v)
        {
            EnsureBuilt();
            if (v && _currentScreen != ScreenId.Play) return;
            if (v)
            {
                _settingsReturnOverlay = UiOverlay.None;
                ApplyVisibility(ScreenId.Play, true, false, false, false, false);
                UpdateSoundButtonLabels();
            }
            else if (_pauseVisible)
            {
                ApplyVisibility(ScreenId.Play, false, false, false, true, true);
            }
        }

        public void ShowCustomization(VehicleDef def)
        {
            EnsureBuilt();
            if (_currentScreen != ScreenId.Play) return;
            _customDef = def;
            _settingsReturnOverlay = UiOverlay.None;
            ApplyVisibility(ScreenId.Play, false, true, false, false, false);
            _customTitleText.text = "Кастомизация";
            BuildCustomizationOptions(def);
        }

        private void HideCustomization()
        {
            if (!_customizationVisible) return;
            ApplyVisibility(ScreenId.Play, false, false, false, true, true);
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            EnsureBuilt();
            EventSystem current = EventSystem.current;
            if (current != null) _eventSystem = current;
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
            ApplyVisibility(ScreenId.Home, false, false, false, false, false);
            ApplyScaler(true);
            ApplySafeArea(true);
        }

        private void EnsureEventSystem()
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.transform.SetParent(transform, false);
                _eventSystem = esGo.AddComponent<EventSystem>();
            }
            if (_eventSystem.GetComponent<StandaloneInputModule>() == null)
                _eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("UI Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGo.AddComponent<GraphicRaycaster>();

            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = UiLayoutMetrics.ReferenceResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = UiLayoutMetrics.ScalerMatch;

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

        private void ApplyScalerIfNeeded()
        {
            if (_scaler == null) return;
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height) return;
            ApplyScaler(true);
        }

        private void ApplyScaler(bool force)
        {
            if (_scaler == null) return;
            if (!force && _lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
            // One documented match value for every orientation; see UiLayoutMetrics.
            _scaler.matchWidthOrHeight = UiLayoutMetrics.ScalerMatchForAspect(aspect);
            ApplyGarageContentInsets(aspect);
        }

        private void ApplyGarageContentInsets(float aspect)
        {
            if (_garageContent == null) return;
            float inset = UiLayoutMetrics.GarageContentSideInset01(aspect);
            _garageContent.anchorMin = new Vector2(inset, 0f);
            _garageContent.anchorMax = new Vector2(1f - inset, 1f - UiLayoutMetrics.GarageHeaderHeight01);
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

        /// <summary>
        /// The only method allowed to change UI visibility. Primary screens are mutually
        /// exclusive; overlays are children of the active primary and never select a new one.
        /// </summary>
        private void ApplyVisibility(ScreenId screen, bool pause, bool customization, bool settings,
            bool hud, bool tools)
        {
            _currentScreen = screen;
            _pauseVisible = pause && screen == ScreenId.Play;
            _customizationVisible = customization && screen == ScreenId.Play;
            _settingsVisible = settings;
            _hudVisible = hud && screen == ScreenId.Play;
            _toolsVisible = tools && screen == ScreenId.Play && !_pauseVisible
                && !_customizationVisible && !_settingsVisible;

            if (_homeScreen != null) _homeScreen.SetActive(screen == ScreenId.Home);
            if (_garageScreen != null) _garageScreen.SetActive(screen == ScreenId.Garage);
            if (_playScreen != null) _playScreen.SetActive(screen == ScreenId.Play);
            if (_completeScreen != null) _completeScreen.SetActive(screen == ScreenId.Complete);

            if (_topBar != null) _topBar.SetActive(_hudVisible);
            if (_instructionPanel != null) _instructionPanel.SetActive(_hudVisible);
            if (_hintPanel != null) _hintPanel.SetActive(screen == ScreenId.Play);
            if (_toolBar != null) _toolBar.gameObject.SetActive(_toolsVisible);
            if (_pausePanel != null) _pausePanel.SetActive(_pauseVisible);
            if (_customizationPanel != null) _customizationPanel.SetActive(_customizationVisible);
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(_settingsVisible);
                if (_settingsVisible) _settingsPanel.transform.SetAsLastSibling();
            }
        }

        private void BuildHomeScreen()
        {
            Image bg = CreateImage("Bg", _homeScreen.transform, new Color(0.08f, 0.09f, 0.11f, 1f));
            Stretch(bg.rectTransform);

            ScrollRect scroll = CreateVerticalScroll(_homeScreen.transform, 16f, new RectOffset(32, 32, 24, 24));
            RectTransform scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRt.anchorMax = new Vector2(0.98f, 0.98f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            RectTransform layout = scroll.content;

            Text title = CreateText("Title", layout, "МАЛЕНЬКИЙ АВТОСЕРВИС", 60, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 64f;
            titleLe.preferredHeight = 96f;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 36;
            title.resizeTextMaxSize = 60;

            Text subtitle = CreateText("Subtitle", layout, "Мойка, ремонт и тюнинг машин", 30, FontStyle.Normal, _muted, TextAnchor.MiddleCenter);
            LayoutElement subLe = subtitle.gameObject.AddComponent<LayoutElement>();
            subLe.minHeight = 40f;
            subLe.preferredHeight = 56f;
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 20;
            subtitle.resizeTextMaxSize = 30;

            CreateSpacer(layout, 12f);

            GameObject progressCard = CreatePanel("ProgressCard", layout, _panel, 22);
            LayoutElement pcLe = progressCard.AddComponent<LayoutElement>();
            pcLe.minHeight = 140f;
            pcLe.preferredHeight = 168f;
            VerticalLayoutGroup pcv = CreateVertical(progressCard.transform, 10f, new RectOffset(24, 24, 20, 20));
            pcv.childAlignment = TextAnchor.MiddleCenter;
            _homeProgressText = CreateText("ProgressText", pcv.transform, "Прогресс: 0/10", 28, FontStyle.Normal, _text, TextAnchor.MiddleCenter);
            _homeProgressText.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            _homeProgressSlider = CreateSlider(pcv.transform, _steel, _teal);
            _homeProgressSlider.interactable = false;
            MakeSliderVisualOnly(_homeProgressSlider);
            LayoutElement psLe = _homeProgressSlider.gameObject.AddComponent<LayoutElement>();
            psLe.preferredHeight = 28f;
            psLe.minHeight = 24f;

            CreateSpacer(layout, 16f);

            _homePlayButton = CreateButton("PlayButton", layout, "ИГРАТЬ", _teal, _text, 40, 120f);
            _homeSoundButton = CreateButton("SoundButton", layout, "ЗВУК И НАСТРОЙКИ", _panelSoft, _text, 28, 96f);
            _homeSoundButtonText = _homeSoundButton.GetComponentInChildren<Text>();
        }

        private void BuildGarageScreen()
        {
            CreateImage("Bg", _garageScreen.transform, new Color(0.09f, 0.10f, 0.12f, 1f)).rectTransform.StretchFull();

            var topBar = CreatePanel("Top", _garageScreen.transform, _panel, 0).GetComponent<RectTransform>();
            AnchorTop(topBar, UiLayoutMetrics.GarageHeaderHeight01);
            HorizontalLayoutGroup top = CreateHorizontal(topBar, 16f, new RectOffset(20, 20, 12, 12));
            top.childAlignment = TextAnchor.MiddleCenter;
            _garageBackButton = CreateButton("Back", top.transform, "НАЗАД", _steel, _text, 30, 96f, 180f);
            LayoutElement backLe = _garageBackButton.GetComponent<LayoutElement>();
            backLe.minWidth = 160f;
            backLe.flexibleWidth = 0;

            Text title = CreateText("Title", top.transform, "ВЫБОР ЗАДАНИЯ", 40, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.minWidth = 120f;
            titleLe.flexibleWidth = 1;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = 40;

            _garageProgressText = CreateText("Progress", top.transform, "0/10", 30, FontStyle.Bold, _amber, TextAnchor.MiddleRight);
            LayoutElement counterLe = _garageProgressText.gameObject.AddComponent<LayoutElement>();
            counterLe.minWidth = 96f;
            counterLe.preferredWidth = 140f;
            counterLe.flexibleWidth = 0;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(_garageScreen.transform, false);
            _garageContent = content.GetComponent<RectTransform>();
            ApplyGarageContentInsets((float)Screen.width / Mathf.Max(1f, Screen.height));
            _garageContent.offsetMin = new Vector2(0f, 24f);
            _garageContent.offsetMax = new Vector2(0f, -12f);

            ScrollRect scroll = content.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = CreatePanel("Viewport", content.transform, new Color(0, 0, 0, 0), 0).GetComponent<RectTransform>();
            Stretch(viewport);
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
            le.minHeight = 128f;
            le.flexibleWidth = 1f;
            Button btn = root.AddComponent<Button>();
            btn.transition = Button.Transition.ColorTint;
            btn.targetGraphic = root.GetComponent<Image>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            cb.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.disabledColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            btn.colors = cb;

            HorizontalLayoutGroup h = CreateHorizontal(root.transform, 12f, new RectOffset(20, 20, 16, 16));
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childForceExpandWidth = false;

            var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(root.transform, false);
            VerticalLayoutGroup iv = info.GetComponent<VerticalLayoutGroup>();
            iv.spacing = 4f;
            iv.childControlWidth = true;
            iv.childControlHeight = true;
            iv.childForceExpandWidth = true;
            iv.childForceExpandHeight = false;
            LayoutElement infoLe = info.GetComponent<LayoutElement>();
            infoLe.minWidth = 0f;
            infoLe.flexibleWidth = 1f;

            Text title = CreateText("Title", info.transform, "", 32, FontStyle.Bold, _text, TextAnchor.MiddleLeft);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 38f;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 22;
            title.resizeTextMaxSize = 32;
            title.verticalOverflow = VerticalWrapMode.Overflow;

            Text desc = CreateText("Desc", info.transform, "", 24, FontStyle.Normal, _muted, TextAnchor.MiddleLeft);
            LayoutElement descLe = desc.gameObject.AddComponent<LayoutElement>();
            descLe.minHeight = 32f;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            desc.resizeTextForBestFit = true;
            desc.resizeTextMinSize = 18;
            desc.resizeTextMaxSize = 24;

            Text stars = CreateText("Stars", info.transform, "", 24, FontStyle.Bold, _amber, TextAnchor.MiddleLeft);
            LayoutElement starsLe = stars.gameObject.AddComponent<LayoutElement>();
            starsLe.minHeight = 30f;
            stars.verticalOverflow = VerticalWrapMode.Overflow;

            GameObject lockOverlay = CreatePanel("Lock", root.transform, new Color(0f, 0f, 0f, 0.28f), 18);
            LayoutElement lockLe = lockOverlay.AddComponent<LayoutElement>();
            lockLe.ignoreLayout = true;
            Stretch(lockOverlay.GetComponent<RectTransform>());
            lockOverlay.GetComponent<Image>().raycastTarget = false;
            Text lockText = CreateText("LockText", lockOverlay.transform, "ЗАКРЫТО", 28, FontStyle.Bold, _muted, TextAnchor.MiddleCenter);
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

            // Top bar: documented compact height so the play area stays visible.
            _topBar = CreatePanel("TopBar", _playScreen.transform, new Color(_panel.r, _panel.g, _panel.b, 0.82f), 0);
            _topBar.GetComponent<Image>().raycastTarget = false;
            RectTransform topRt = _topBar.GetComponent<RectTransform>();
            AnchorTop(topRt, UiLayoutMetrics.TopBarHeight01);
            HorizontalLayoutGroup th = CreateHorizontal(topRt, 12f, new RectOffset(20, 20, 10, 10));
            th.childAlignment = TextAnchor.MiddleCenter;
            th.childForceExpandWidth = false;

            GameObject info = new GameObject("Info", typeof(RectTransform), typeof(LayoutElement));
            info.transform.SetParent(topRt, false);
            LayoutElement infoLe = info.GetComponent<LayoutElement>();
            infoLe.flexibleWidth = 1;
            infoLe.minWidth = 0f;
            VerticalLayoutGroup infoLayout = CreateVertical(info.transform, 2f, new RectOffset(0, 0, 0, 0));
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandHeight = false;

            _playTitleText = CreateText("Title", infoLayout.transform, "", 32, FontStyle.Bold, _text, TextAnchor.MiddleLeft);
            LayoutElement titleLe = _playTitleText.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 38f;
            _playTitleText.resizeTextForBestFit = true;
            _playTitleText.resizeTextMinSize = 22;
            _playTitleText.resizeTextMaxSize = 32;
            _playTitleText.verticalOverflow = VerticalWrapMode.Overflow;

            _playTaskText = CreateText("Task", infoLayout.transform, "", 22, FontStyle.Normal, _muted, TextAnchor.MiddleLeft);
            LayoutElement taskLe = _playTaskText.gameObject.AddComponent<LayoutElement>();
            taskLe.minHeight = 28f;
            _playTaskText.resizeTextForBestFit = true;
            _playTaskText.resizeTextMinSize = 16;
            _playTaskText.resizeTextMaxSize = 22;
            _playTaskText.verticalOverflow = VerticalWrapMode.Overflow;

            _playPauseButton = CreateButton("Pause", topRt, "ПАУЗА", _steel, _text, 26, 88f, 160f);
            LayoutElement pauseLe = _playPauseButton.GetComponent<LayoutElement>();
            pauseLe.minHeight = UiLayoutMetrics.MinTouchTargetHeight;
            pauseLe.flexibleWidth = 0f;

            // Instruction strip below the top bar, with a deliberate gap to prevent overlap.
            _instructionPanel = CreatePanel("Instruction", _playScreen.transform, new Color(0.12f, 0.13f, 0.15f, 0.58f), 12);
            RectTransform irt = _instructionPanel.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.08f, UiLayoutMetrics.InstructionMinY01);
            irt.anchorMax = new Vector2(0.92f, UiLayoutMetrics.InstructionMaxY01);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            _instructionPanel.GetComponent<Image>().raycastTarget = false;
            _playInstructionText = CreateText("InstructionText", _instructionPanel.transform, "", 24, FontStyle.Normal, _text, TextAnchor.MiddleCenter);
            Stretch(_playInstructionText.rectTransform);
            _playInstructionText.resizeTextForBestFit = true;
            _playInstructionText.resizeTextMinSize = 16;
            _playInstructionText.resizeTextMaxSize = 24;
            _playInstructionText.verticalOverflow = VerticalWrapMode.Truncate;
            _playInstructionText.raycastTarget = false;

            // Hint above the bottom toolbar; keeps the center play area open.
            _hintPanel = CreatePanel("Hint", _playScreen.transform, new Color(0, 0, 0, 0), 0);
            RectTransform hrt = _hintPanel.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.08f, UiLayoutMetrics.HintMinY01);
            hrt.anchorMax = new Vector2(0.92f, UiLayoutMetrics.HintMaxY01);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            _hintPanel.GetComponent<Image>().raycastTarget = false;
            _playHintText = CreateText("HintText", _hintPanel.transform, "", 22, FontStyle.Normal, _amber, TextAnchor.MiddleCenter);
            Stretch(_playHintText.rectTransform);
            _playHintText.resizeTextForBestFit = true;
            _playHintText.resizeTextMinSize = 14;
            _playHintText.resizeTextMaxSize = 22;
            _playHintText.raycastTarget = false;

            // Bottom toolbar: compact, responsive, and high enough for touch.
            GameObject bottom = CreatePanel("BottomBar", _playScreen.transform, new Color(_panel.r, _panel.g, _panel.b, 0.86f), 0);
            bottom.GetComponent<Image>().raycastTarget = false;
            RectTransform brt = bottom.GetComponent<RectTransform>();
            AnchorBottom(brt, UiLayoutMetrics.BottomBarHeight01);

            VerticalLayoutGroup bv = CreateVertical(brt, 8f, new RectOffset(16, 16, 10, 10));
            bv.childAlignment = TextAnchor.UpperCenter;
            bv.childControlHeight = true;
            bv.childControlWidth = true;
            bv.childForceExpandHeight = false;
            bv.childForceExpandWidth = true;

            _playProgressSlider = CreateSlider(bv.transform, _steel, _amber);
            _playProgressSlider.interactable = false;
            MakeSliderVisualOnly(_playProgressSlider);
            LayoutElement sliderLe = _playProgressSlider.gameObject.AddComponent<LayoutElement>();
            sliderLe.preferredHeight = 22f;
            sliderLe.minHeight = 20f;
            sliderLe.flexibleHeight = 0f;

            GameObject tools = new GameObject("Tools", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            tools.transform.SetParent(bottom.transform, false);
            _toolBar = tools.GetComponent<RectTransform>();
            HorizontalLayoutGroup hg = tools.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 8f;
            hg.padding = new RectOffset(2, 2, 0, 0);
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childControlWidth = true;
            hg.childControlHeight = true;
            hg.childForceExpandWidth = true;
            hg.childForceExpandHeight = false;
            LayoutElement tle = tools.GetComponent<LayoutElement>();
            tle.flexibleHeight = 0f;
            tle.minHeight = UiLayoutMetrics.MinTouchTargetHeight;
            tle.preferredHeight = UiLayoutMetrics.ToolButtonPreferredHeight;
        }

        private void BuildCompleteScreen()
        {
            Image bg = CreateImage("Bg", _completeScreen.transform, new Color(0.06f, 0.07f, 0.09f, 0.72f));
            bg.rectTransform.StretchFull();
            bg.raycastTarget = true; // modal result screen: no input leaks to play area.

            GameObject card = CreatePanel("Card", _completeScreen.transform, _panel, 24);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.10f, 0.18f);
            crt.anchorMax = new Vector2(0.90f, 0.82f);
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
            _pausePanel.GetComponent<Image>().raycastTarget = true;

            GameObject card = CreatePanel("Card", _pausePanel.transform, _panel, 20);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.14f, 0.20f);
            crt.anchorMax = new Vector2(0.86f, 0.80f);
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
            _customizationPanel = new GameObject("CustomizationPanel", typeof(RectTransform));
            _customizationPanel.transform.SetParent(_playScreen.transform, false);
            RectTransform rt = _customizationPanel.GetComponent<RectTransform>();
            Stretch(rt);

            // Transparent input blocker so customization is modal over gameplay.
            Image blocker = _customizationPanel.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.14f);
            blocker.raycastTarget = true;

            // Bottom sheet keeps the car visible in the center.
            GameObject sheet = CreatePanel("Sheet", _customizationPanel.transform, new Color(0.09f, 0.10f, 0.12f, 0.94f), 18);
            RectTransform sheetRt = sheet.GetComponent<RectTransform>();
            sheetRt.anchorMin = new Vector2(0.03f, 0.02f);
            sheetRt.anchorMax = new Vector2(0.97f, 0.36f);
            sheetRt.offsetMin = Vector2.zero;
            sheetRt.offsetMax = Vector2.zero;
            sheet.GetComponent<Image>().raycastTarget = true;

            VerticalLayoutGroup shell = CreateVertical(sheetRt, 10f, new RectOffset(16, 16, 14, 14));
            shell.childAlignment = TextAnchor.UpperCenter;
            shell.childControlWidth = true;
            shell.childControlHeight = true;
            shell.childForceExpandWidth = true;
            shell.childForceExpandHeight = false;

            _customTitleText = CreateText("Title", shell.transform, "Кастомизация", 34, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            _customTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;

            ScrollRect scroll = CreateVerticalScroll(shell.transform, 8f, new RectOffset(0, 0, 0, 0));
            _customContent = scroll.content;
            LayoutElement scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 120f;

            CreateText("ColorLabel", _customContent, "Цвет кузова", 24, FontStyle.Normal, _muted, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            _colorRow = CreateRow(_customContent, 68f);
            CreateText("RimLabel", _customContent, "Диски", 24, FontStyle.Normal, _muted, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            _rimRow = CreateRow(_customContent, 68f);
            CreateText("DecalLabel", _customContent, "Наклейки", 24, FontStyle.Normal, _muted, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            _decalRow = CreateRow(_customContent, 68f);

            _customCommitButton = CreateButton("Commit", shell.transform, "ГОТОВО", _teal, _text, 30, 88f);
            _customCommitButton.GetComponent<LayoutElement>().minHeight = UiLayoutMetrics.MinTouchTargetHeight;
        }

        private RectTransform CreateRow(Transform parent, float height)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = UiLayoutMetrics.MinTouchTargetHeight;
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            return row.GetComponent<RectTransform>();
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = CreatePanel("SettingsPanel", _safeRoot, new Color(0f, 0f, 0f, 0.72f), 0);
            Stretch(_settingsPanel.GetComponent<RectTransform>());
            _settingsPanel.GetComponent<Image>().raycastTarget = true;

            GameObject card = CreatePanel("Card", _settingsPanel.transform, _panel, 20);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.12f, 0.16f);
            crt.anchorMax = new Vector2(0.88f, 0.84f);
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
                // GameManager owns the transition; UIManager only raises the request.
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
                HideCustomization();
                CustomizationCommitted?.Invoke();
            });

            _settingsCloseButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                CloseSettings();
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
            EnsureBuilt();
            if (_settingsVisible) return;
            _settingsReturnOverlay = ActiveOverlay;
            SyncSettingsUI();
            ApplyVisibility(_currentScreen, _pauseVisible, _customizationVisible, true, false, false);
        }

        private void CloseSettings()
        {
            if (!_settingsVisible) return;
            UiOverlay back = _settingsReturnOverlay;
            _settingsReturnOverlay = UiOverlay.None;
            ApplyVisibility(_currentScreen, back == UiOverlay.Pause, back == UiOverlay.Customization,
                false, back == UiOverlay.None, back == UiOverlay.None);
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

            // Same tool set: update in place so repeated ShowPlay calls do not create
            // duplicate objects during the frame in which Destroy() is deferred.
            if (_toolButtons.Count == labels.Count)
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    ToolButton tb = _toolButtons[i];
                    if (tb == null) continue;
                    tb.ToolId = ids[i];
                    if (tb.Label != null) tb.Label.text = labels[i];
                    if (tb.Icon != null) tb.Icon.color = colors[i];
                    bool locked = selectedIndex >= 0 && i > selectedIndex;
                    tb.Button.interactable = !locked;
                    SetToolSelectedVisual(tb, i == selectedIndex || (selectedIndex < 0 && i == 0));
                    if (locked && tb.Background != null)
                        tb.Background.color = new Color(_panelSoft.r * 0.55f, _panelSoft.g * 0.55f, _panelSoft.b * 0.55f, 0.9f);
                }
                return;
            }

            ClearToolButtons();
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
            le.minHeight = UiLayoutMetrics.MinTouchTargetHeight;
            le.preferredHeight = UiLayoutMetrics.ToolButtonPreferredHeight;
            le.flexibleWidth = 1;

            Button btn = root.AddComponent<Button>();
            btn.transition = Button.Transition.ColorTint;
            Image bg = root.GetComponent<Image>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            cb.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.65f);
            btn.colors = cb;

            VerticalLayoutGroup v = CreateVertical(root.transform, 2f, new RectOffset(6, 6, 8, 8));
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var iconWrap = new GameObject("IconWrap", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            iconWrap.transform.SetParent(v.transform, false);
            iconWrap.GetComponent<LayoutElement>().preferredHeight = 28;
            HorizontalLayoutGroup ih = iconWrap.GetComponent<HorizontalLayoutGroup>();
            ih.childAlignment = TextAnchor.MiddleCenter;
            ih.childControlWidth = false;
            ih.childControlHeight = false;
            ih.childForceExpandWidth = true;
            ih.childForceExpandHeight = true;
            Image iconImg = CreateImage("Icon", iconWrap.transform, accent);
            iconImg.sprite = _circleSprite;
            iconImg.rectTransform.sizeDelta = new Vector2(24, 24);

            Text t = CreateText("Label", v.transform, label, 20, FontStyle.Bold, _text, TextAnchor.MiddleCenter);
            LayoutElement labelLe = t.gameObject.AddComponent<LayoutElement>();
            labelLe.minHeight = 34f;
            labelLe.flexibleHeight = 1f;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 13;
            t.resizeTextMaxSize = 20;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            ToolButton tb = new ToolButton { Root = root, Button = btn, Background = bg, Icon = iconImg, Label = t, ToolId = id };
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
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                GameObject child = row.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                Destroy(child);
            }
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
            Button btn = CreateButton(label, parent, label, _panelSoft, _text, 20, 68f);
            Text t = btn.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 14;
                t.resizeTextMaxSize = 20;
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
            le.minHeight = Mathf.Max(UiLayoutMetrics.MinTouchTargetHeight, height);
            if (preferredWidth > 0f) le.preferredWidth = preferredWidth;

            Text t = CreateText("Label", go.transform, label, fontSize, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(14, fontSize - 10);
            t.resizeTextMaxSize = fontSize;
            return btn;
        }

        private Slider CreateSlider(Transform parent, Color bg, Color fill)
        {
            GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 28f);

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

        private ScrollRect CreateVerticalScroll(Transform parent, float spacing, RectOffset padding)
        {
            var root = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());

            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreatePanel("Viewport", root.transform, new Color(0f, 0f, 0f, 0f), 0).GetComponent<RectTransform>();
            Stretch(viewport);
            if (viewport.gameObject.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            VerticalLayoutGroup v = content.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = content.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt;
            return scroll;
        }

        private static void MakeSliderVisualOnly(Slider slider)
        {
            if (slider == null) return;
            Graphic[] graphics = slider.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null) graphics[i].raycastTarget = false;
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