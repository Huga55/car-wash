#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LittleCarWashBuild
{
    /// <summary>
    /// CI verification entry point. It checks pure visual/gameplay rules, then
    /// starts Play Mode and captures the eleven required wash/reward moments
    /// plus the five core screens at four aspect ratios. Screenshots and a
    /// pass/fail report are written to -customBuildPath so the workflow can
    /// upload them. Nothing here is compiled into a player build.
    /// </summary>
    public static class VisualVerification
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const int VerificationLevel = 5; // has the sponge stage

        private static readonly string[] Moments =
        {
            "01_Play", "02_Wetting", "03_WaterStream", "04_Foam", "05_Sponge",
            "06_Rinse", "07_CleanShiny", "08_AnimalAppears", "09_AnimalJoy",
            "10_EnteringCar", "11_FinalScreen"
        };

        private static readonly string[] Screens =
        {
            "Home", "Levels", "Play", "Pause", "Complete"
        };

        private static readonly int[][] Resolutions =
        {
            new[] { 1920, 1080 }, // 16:9
            new[] { 1280, 800 },  // 16:10
            new[] { 1024, 768 },  // 4:3
            new[] { 2560, 1080 }  // ultra-wide
        };

        private static string _output;
        private static string _buildPath;
        private static readonly List<string> _report = new List<string>();
        private static GameController _controller;
        private static int _frame;
        private static int _resolutionIndex;
        private static int _screenIndex;
        private static int _momentIndex;
        private static int _waitFrames;
        private static float _waitUntil;
        private static string _lastCapture;
        private static bool _playing;
        private static bool _finished;
        private static bool _rewardStarted;
        private static float _rewardStart;
        private static bool _rewardDurationChecked;
        private static int _resolutionSettle;
        private static string _pendingScreen;

        public static void Run()
        {
            _buildPath = GetArg("customBuildPath");
            _output = Path.Combine("build", "verification");
            Directory.CreateDirectory(_output);
            if (!string.IsNullOrEmpty(_buildPath)) Directory.CreateDirectory(_buildPath);
            _report.Clear();

            ApplyLegacyInput();
            EditorSceneManager.OpenScene(ScenePath);

            bool ok = CheckPureVisuals();
            WriteReport(ok, "before Play Mode");
            if (!ok)
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
            Debug.Log("[verify] entering Play Mode for screenshots");
            EditorApplication.EnterPlaymode();
        }

        private static bool CheckPureVisuals()
        {
            try
            {
                for (int i = 0; i < GameData.Kinds.Length; i++)
                {
                    Color32[] pixels = CarArt.Render(GameData.Kinds[i],
                        GameData.Levels[i].Body, GameData.Levels[i].Accent,
                        GameData.Levels[i].Size, 384, 256);
                    int opaque = 0;
                    for (int p = 0; p < pixels.Length; p++) if (pixels[p].a > 128) opaque++;
                    if (opaque < 2000)
                    {
                        _report.Add("FAIL car " + GameData.Kinds[i] + " opaque=" + opaque);
                        return false;
                    }
                }
                _report.Add("PASS all " + GameData.Kinds.Length + " cars render");

                WashStage[] stages = GameData.StagesFor(5);
                var expected = new[] { WashStage.Wet, WashStage.Foam, WashStage.Scrub,
                    WashStage.Rinse, WashStage.Dry };
                if (stages.Length != expected.Length)
                {
                    _report.Add("FAIL stage count=" + stages.Length);
                    return false;
                }
                for (int i = 0; i < expected.Length; i++)
                {
                    if (stages[i] != expected[i])
                    {
                        _report.Add("FAIL stage order at " + i + ": " + stages[i]);
                        return false;
                    }
                }
                _report.Add("PASS stage order Wet/Foam/Scrub/Rinse/Dry");

                Color32[] sedan = CarArt.Render(CarKind.Sedan,
                    new Color32(72, 156, 255, 255), new Color32(255, 208, 74, 255), 1f, 384, 256);
                var surface = new WashSurface(sedan, 384, 256, 0.8f, 7, DirtKind.Mud);
                int visible = surface.VisiblePixelCount();
                if (visible < 10000)
                {
                    _report.Add("FAIL visible dirt pixels=" + visible);
                    return false;
                }
                _report.Add("PASS visible dirt overlay (" + visible + " px)");

                for (int i = 0; i < 40; i++)
                    surface.Paint(0.2f, 0.75f, WashingTools.Radius(WashStage.Wet, 1f), WashStage.Wet);
                if (surface.MinZoneProgress(WashStage.Wet) >= WashSurface.ZoneFinish)
                {
                    _report.Add("FAIL repeated strokes finished a stage");
                    return false;
                }
                _report.Add("PASS repeated strokes cannot finish a stage");

                for (int y = 0; y <= 20; y++)
                    for (int x = 0; x <= 30; x++)
                        surface.Paint((x + 0.5f) / 31f, (y + 0.5f) / 21f,
                            WashingTools.Radius(WashStage.Wet, 1f), WashStage.Wet);
                if (surface.MinZoneProgress(WashStage.Wet) < WashSurface.ZoneFinish)
                {
                    _report.Add("FAIL full coverage did not finish a stage");
                    return false;
                }
                _report.Add("PASS all six zones reach " + WashSurface.ZoneFinish.ToString("F2"));

                FieldInfo duration = typeof(GameController).GetField("RewardSceneSeconds",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                float seconds = duration != null && duration.IsLiteral
                    ? Convert.ToSingle(duration.GetRawConstantValue()) : 0f;
                if (seconds < 7f)
                {
                    _report.Add("FAIL reward scene only " + seconds + "s");
                    return false;
                }
                _report.Add("PASS reward scene budget " + seconds.ToString("F1") + "s (no auto-skip)");

                if (string.IsNullOrEmpty(AppInfo.Version) || AppInfo.VersionCode <= 0 ||
                    !AppInfo.UpdateManifestUrl.StartsWith("https://"))
                {
                    _report.Add("FAIL auto-update manifest configuration");
                    return false;
                }
                var newer = new UpdateInfo
                {
                    version = "99.0",
                    versionCode = AppInfo.VersionCode + 1
                };
                var same = new UpdateInfo
                {
                    version = AppInfo.Version,
                    versionCode = AppInfo.VersionCode
                };
                if (!UpdateService.IsNewer(newer) || UpdateService.IsNewer(same) ||
                    string.IsNullOrEmpty(Loc.T(Loc.UpdateCheck)))
                {
                    _report.Add("FAIL auto-update version comparison or localization");
                    return false;
                }
                _report.Add("PASS auto-update manifest and version comparison");

                Vector2 reference = new Vector2(1200f, 800f);
                foreach (int[] res in Resolutions)
                {
                    UiFit fit = UiLayout.Compute(reference, res[0], res[1], new Rect(0f, 0f, res[0], res[1]));
                    float effective = UiLayout.CanvasScaleFactor(reference, res[0], res[1]) * fit.Scale;
                    float usedW = reference.x * effective;
                    float usedH = reference.y * effective;
                    if (usedW > res[0] + 1f || usedH > res[1] + 1f || fit.Scale <= 0f)
                    {
                        _report.Add("FAIL UI does not fit " + res[0] + "x" + res[1] +
                                    " (used " + usedW.ToString("F0") + "x" + usedH.ToString("F0") + ")");
                        return false;
                    }
                }
                _report.Add("PASS UI layout fits all four target resolutions");
                return true;
            }
            catch (Exception e)
            {
                _report.Add("FAIL exception: " + e);
                return false;
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) _playing = true;
        }

        private static void Tick()
        {
            if (_finished) return;
            _frame++;
            if (_frame > 60 * 120)
            {
                Finish(false, "Play Mode timeout");
                return;
            }
            if (!_playing || !EditorApplication.isPlaying) return;

            if (_controller == null) _controller = UnityEngine.Object.FindObjectOfType<GameController>();
            if (_controller == null)
            {
                Finish(false, "GameController not found");
                return;
            }

            if (_waitUntil > 0f && Time.realtimeSinceStartup < _waitUntil) return;
            _waitUntil = 0f;

            // Screen.SetResolution is applied over several frames in the Editor.
            // Waiting before invoking the screen avoids mis-labelled captures.
            if (_resolutionSettle > 0)
            {
                _resolutionSettle--;
                if (_resolutionSettle == 0 && !string.IsNullOrEmpty(_pendingScreen))
                {
                    Invoke(_pendingScreen);
                    if (_finished) return;
                    int[] settled = Resolutions[_resolutionIndex];
                    Capture(string.Format("UI_{0}x{1}_{2}.png",
                        settled[0], settled[1], _pendingScreen));
                    _pendingScreen = null;
                    _waitFrames = 18;
                }
                return;
            }

            if (_waitFrames > 0)
            {
                _waitFrames--;
                if (_waitFrames == 0 && !string.IsNullOrEmpty(_lastCapture))
                {
                    var file = new FileInfo(_lastCapture);
                    if (!file.Exists || file.Length < 1000)
                    {
                        Finish(false, "screenshot missing or empty: " + Path.GetFileName(_lastCapture));
                        return;
                    }
                    _lastCapture = null;
                }
                return;
            }

            if (_momentIndex < Moments.Length)
            {
                RunMoment(Moments[_momentIndex]);
                return;
            }

            if (!_rewardDurationChecked)
            {
                Finish(false, "reward duration was never measured");
                return;
            }

            if (_screenIndex >= Screens.Length)
            {
                _screenIndex = 0;
                _resolutionIndex++;
                if (_resolutionIndex >= Resolutions.Length)
                {
                    CheckDebugMarkers();
                    Finish(true, "all wash moments and UI screens captured");
                    return;
                }
            }

            int[] res = Resolutions[_resolutionIndex];
            Screen.SetResolution(res[0], res[1], false);
            _pendingScreen = Screens[_screenIndex];
            _screenIndex++;
            _resolutionSettle = 12;
        }

        private static void RunMoment(string moment)
        {
            switch (moment)
            {
                case "01_Play":
                    PlayerPrefs.SetInt(GameData.UnlockKey, VerificationLevel);
                    PlayerPrefs.Save();
                    Call("StartLevel", VerificationLevel);
                    break;

                case "02_Wetting":
                    OnStrokeEnded();
                    SetStage(WashStage.Wet);
                    PaintCoverage(WashStage.Wet, 0.32f);
                    break;

                case "03_WaterStream":
                    OnStrokeEnded();
                    OnStroke(WashStage.Wet, new Vector2(0.52f, 0.58f));
                    var stream = (Image)GetField("_stream");
                    if (stream == null || !stream.gameObject.activeSelf ||
                        Mathf.Abs(stream.rectTransform.localRotation.eulerAngles.z) < 1f)
                    {
                        Finish(false, "directional water stream is missing");
                        return;
                    }
                    _report.Add("PASS directional water stream is active");
                    break;

                case "04_Foam":
                    OnStrokeEnded();
                    SetStage(WashStage.Foam);
                    PaintCoverage(WashStage.Foam, 0.62f);
                    break;

                case "05_Sponge":
                    OnStrokeEnded();
                    SetStage(WashStage.Scrub);
                    PaintCoverage(WashStage.Scrub, 0.38f);
                    Vector2 spongeUv = new Vector2(0.48f, 0.54f);
                    OnStroke(WashStage.Scrub, spongeUv);
                    var cursor = (Image)GetField("_toolCursor");
                    var wash = GetField("_washRect") as RectTransform;
                    if (cursor == null || !cursor.gameObject.activeSelf || wash == null)
                    {
                        Finish(false, "sponge cursor is missing");
                        return;
                    }
                    Vector2 contact = new Vector2((spongeUv.x - 0.5f) * wash.sizeDelta.x,
                        (spongeUv.y - 0.5f) * wash.sizeDelta.y);
                    float gap = Vector2.Distance(cursor.rectTransform.anchoredPosition, contact);
                    if (gap > 50f)
                    {
                        Finish(false, "sponge floats " + gap.ToString("F0") + "px from the car");
                        return;
                    }
                    _report.Add("PASS sponge contacts the car (" + gap.ToString("F0") + "px)");
                    break;

                case "06_Rinse":
                    OnStrokeEnded();
                    SetStage(WashStage.Rinse);
                    PaintCoverage(WashStage.Rinse, 0.55f);
                    break;

                case "07_CleanShiny":
                    OnStrokeEnded();
                    SetStage(WashStage.Dry);
                    var cleanSurface = (WashSurface)GetField("_surface");
                    if (cleanSurface == null)
                    {
                        Finish(false, "wash surface missing before clean car capture");
                        return;
                    }
                    cleanSurface.FinishAll();
                    HideCarOverlay();
                    break;

                case "08_AnimalAppears":
                    OnStrokeEnded();
                    StartReward();
                    // Sparkle (1 s) + entrance (1.5 s): the driver is fully in view.
                    _waitUntil = _rewardStart + 2.05f;
                    return;

                case "09_AnimalJoy":
                    // Entry ends at 2.5 s; capture in the middle of the 2 s jump.
                    _waitUntil = _rewardStart + 3.35f;
                    return;

                case "10_EnteringCar":
                    // 1 + 1.5 + 2 + 2 = 6.5 s, then the 2 s boarding phase.
                    _waitUntil = _rewardStart + 7.4f;
                    return;

                case "11_FinalScreen":
                    var card = (GameObject)GetField("_completeCard");
                    if (card == null || !card.activeSelf)
                    {
                        _waitUntil = _rewardStart + GameController.RewardSceneSeconds + 2f;
                        return;
                    }
                    float duration = Time.realtimeSinceStartup - _rewardStart;
                    if (duration < 7f)
                    {
                        Finish(false, "reward scene auto-skipped after " + duration.ToString("F2") + "s");
                        return;
                    }
                    _report.Add("PASS reward scene ran " + duration.ToString("F2") + "s");
                    _rewardDurationChecked = true;
                    break;
            }

            Capture(moment + ".png");
            _momentIndex++;
            _waitFrames = 18;
        }

        private static void StartReward()
        {
            if (_rewardStarted) return;
            MethodInfo info = typeof(GameController).GetMethod("RewardSequence",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null)
            {
                Finish(false, "RewardSequence not found");
                return;
            }
            var sequence = info.Invoke(_controller, null) as IEnumerator;
            if (sequence == null)
            {
                Finish(false, "RewardSequence returned no coroutine");
                return;
            }
            _controller.StartCoroutine(sequence);
            _rewardStarted = true;
            _rewardStart = Time.realtimeSinceStartup;
        }

        private static void SetStage(WashStage stage)
        {
            var stages = GetField("_stages") as WashStage[];
            if (stages != null)
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    if (stages[i] == stage)
                    {
                        SetField("_stageIndex", i);
                        break;
                    }
                }
            }
            SetField("_stage", stage);
            Call("UpdateStageUi");
        }

        private static void HideCarOverlay()
        {
            object car = GetField("_car");
            if (car == null) return;
            MethodInfo info = car.GetType().GetMethod("HideOverlay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info != null) info.Invoke(car, null);
        }

        private static void PaintCoverage(WashStage stage, float fraction)
        {
            var surface = (WashSurface)GetField("_surface");
            if (surface == null)
            {
                Finish(false, "wash surface missing");
                return;
            }
            float radius = WashingTools.Radius(stage, 1f);
            int columns = Mathf.Max(1, Mathf.CeilToInt(1f / (radius * 0.85f)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(1f / (radius * 1.25f)));
            int limit = Mathf.Max(1, Mathf.RoundToInt(columns * fraction));
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= limit; x++)
                    surface.Paint((x + 0.5f) / (columns + 1f), (y + 0.5f) / (rows + 1f), radius, stage);
            surface.Upload();
        }

        private static void OnStroke(WashStage stage, Vector2 uv)
        {
            Call("OnStrokeStarted", stage);
            Call("OnStrokeMoved", uv, stage);
        }

        private static void OnStrokeEnded()
        {
            Call("OnStrokeEnded");
        }

        private static void CheckDebugMarkers()
        {
            var flagged = new List<string>();
            foreach (Transform t in _controller.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("zone") || lower.Contains("marker") || lower.Contains("debug") ||
                    lower.Contains("gizmo") || lower.Contains("testsquare") ||
                    lower.Contains("cloud") || lower.Contains("sun"))
                    flagged.Add(t.name);
            }
            if (flagged.Count > 0)
                _report.Add("FAIL debug objects in play view: " + string.Join(", ", flagged.ToArray()));
            else
                _report.Add("PASS no zone/marker/debug objects in play view");
        }

        private static void Invoke(string screen)
        {
            if (screen == "Home")
            {
                Call("ShowHome");
                var button = GetField("_updateButton") as UnityEngine.UI.Button;
                var dialog = GetField("_updatePanel") as GameObject;
                if (button == null || !button.gameObject.activeSelf || dialog == null || dialog.activeSelf)
                {
                    Finish(false, "home update button or update dialog is in the wrong state");
                    return;
                }
                _report.Add("PASS home screen exposes the update check");
            }
            else if (screen == "Levels") Call("ShowLevels");
            else if (screen == "Play") Call("StartLevel", 0);
            else if (screen == "Pause") Call("ShowPause");
            else if (screen == "Complete")
            {
                Call("StartLevel", 0);
                Call("CompleteLevel");
            }
        }

        private static void Capture(string name)
        {
            string path = Path.Combine(_output, name);
            ScreenCapture.CaptureScreenshot(path, 1);
            _report.Add("CAPTURE " + name + " (actual " + Screen.width + "x" + Screen.height + ")");
            _lastCapture = path;
        }

        private static void Call(string method, params object[] args)
        {
            MethodInfo info = typeof(GameController).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new MissingMethodException("GameController." + method);
            info.Invoke(_controller, args);
        }

        private static object GetField(string name)
        {
            FieldInfo info = typeof(GameController).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new MissingFieldException("GameController." + name);
            return info.GetValue(_controller);
        }

        private static void SetField(string name, object value)
        {
            FieldInfo info = typeof(GameController).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new MissingFieldException("GameController." + name);
            info.SetValue(_controller, value);
        }

        private static void Finish(bool ok, string reason)
        {
            if (_finished) return;
            _finished = true;
            _report.Add((ok ? "PASS " : "FAIL ") + reason);
            WriteReport(ok, "final");

            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 2);
        }

        private static void WriteReport(bool ok, string stage)
        {
            string path = Path.Combine(_output, "verification.txt");
            var text = new List<string>
            {
                "Little Car Wash visual verification - " + stage,
                "result: " + (ok ? "PASS" : "FAIL")
            };
            text.AddRange(_report);
            File.WriteAllLines(path, text);
            if (!string.IsNullOrEmpty(_buildPath) &&
                !Path.GetFullPath(_buildPath).Equals(Path.GetFullPath(_output), StringComparison.Ordinal))
                File.WriteAllLines(Path.Combine(_buildPath, "verification.txt"), text);
            Debug.Log("[verify] report: " + path);
        }

        private static void ApplyLegacyInput()
        {
            // The game uses Input.GetMouseButton/Input.touchCount and the legacy
            // StandaloneInputModule. Force the old input manager in CI defaults.
            PropertyInfo property = typeof(PlayerSettings).GetProperty("activeInputHandler",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) return;
            object legacy = Enum.Parse(property.PropertyType, "InputManager");
            property.SetValue(null, legacy, null);
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-" + name) return args[i + 1];
            }
            return null;
        }
    }
}
#endif
