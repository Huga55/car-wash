#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCarWashBuild
{
    /// <summary>
    /// CI verification entry point. It runs the pure visual checks (every car,
    /// stage order, zonal wash progress), then starts Play Mode and captures the
    /// five main screens at four aspect ratios. Screenshots and the pass/fail
    /// report are written to -customBuildPath so the workflow can upload them.
    /// </summary>
    public static class VisualVerification
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

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
        private static int _waitFrames;
        private static string _lastCapture;
        private static bool _playing;
        private static bool _finished;

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
            Debug.Log("[verify] entering Play Mode for screen capture");
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
                int visible = 0;
                foreach (Color32 p in surface.Texture.Pixels) if (p.a > 96) visible++;
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
            if (_frame > 60 * 60)
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

            if (_screenIndex >= Screens.Length)
            {
                _screenIndex = 0;
                _resolutionIndex++;
                if (_resolutionIndex >= Resolutions.Length)
                {
                    Finish(true, "all screens captured");
                    return;
                }
            }

            int[] res = Resolutions[_resolutionIndex];
            Screen.SetResolution(res[0], res[1], false);
            Invoke(Screens[_screenIndex]);

            string name = string.Format("{0}x{1}_{2}.png", res[0], res[1], Screens[_screenIndex]);
            string path = Path.Combine(_output, name);
            ScreenCapture.CaptureScreenshot(path, 1);
            _report.Add("CAPTURE " + name + " (actual " + Screen.width + "x" + Screen.height + ")");
            _lastCapture = path;
            _screenIndex++;
            _waitFrames = 18;
        }

        private static void Invoke(string screen)
        {
            if (screen == "Home") Call("ShowHome");
            else if (screen == "Levels") Call("ShowLevels");
            else if (screen == "Play") Call("StartLevel", 0);
            else if (screen == "Pause") Call("ShowPause");
            else if (screen == "Complete") Call("CompleteLevel");
        }

        private static void Call(string method, params object[] args)
        {
            MethodInfo info = typeof(GameController).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new MissingMethodException("GameController." + method);
            info.Invoke(_controller, args);
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
