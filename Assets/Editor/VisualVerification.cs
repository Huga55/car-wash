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
                // game-ci cannot upload artifacts from a failed step, so make
                // every failure visible in the Unity log as well.
                foreach (string line in _report)
                    if (line.StartsWith("FAIL", StringComparison.Ordinal)) Debug.LogError("[verify] " + line);
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

                if (!CheckMechanicsAndStates()) return false;

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
                    surface.Paint(0.2f, 0.75f, WashingTools.Radius(WashStage.Wet, 1f, 1.25f), WashStage.Wet);
                if (surface.MinZoneProgress(WashStage.Wet) >= WashSurface.ZoneFinish)
                {
                    _report.Add("FAIL repeated strokes finished a stage");
                    return false;
                }
                _report.Add("PASS repeated strokes cannot finish a stage");

                for (int y = 0; y <= 20; y++)
                    for (int x = 0; x <= 30; x++)
                        surface.Paint((x + 0.5f) / 31f, (y + 0.5f) / 21f,
                            WashingTools.Radius(WashStage.Wet, 1f, 1.25f), WashStage.Wet);
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

        private struct StateSample
        {
            public string Name;
            public float R, G, B, A;
        }

        private static WashSurface BuildSurface(CarKind kind, float scale, out Color32[] car,
            out byte[] panelMask)
        {
            car = CarArt.Render(kind, new Color32(72, 156, 255, 255),
                new Color32(255, 208, 74, 255), scale, 384, 256, out panelMask);
            var surface = new WashSurface(car, 384, 256, 0.9f, 17, DirtKind.Mud, panelMask);
            surface.Upload();
            return surface;
        }

        private static bool CheckMechanicsAndStates()
        {
            try
            {
                // A - boundaries follow the real paint mask, not sprite alpha.
                var kinds = new[] { CarKind.Sedan, CarKind.Sports, CarKind.Bus };
                for (int k = 0; k < kinds.Length; k++)
                {
                    Color32[] carA;
                    byte[] maskA;
                    var surfaceA = BuildSurface(kinds[k], 1f, out carA, out maskA);
                    Rect bounds = surfaceA.BodyBounds;
                    if (bounds.width < 0.35f || bounds.height < 0.25f ||
                        bounds.xMin < 0f || bounds.yMin < 0f || bounds.xMax > 1f || bounds.yMax > 1f)
                    {
                        _report.Add("FAIL A body bounds for " + kinds[k]);
                        return false;
                    }
                    Vector2 panelUv, detailUv;
                    if (!FindMaskSamples(maskA, out panelUv, out detailUv) ||
                        !surfaceA.IsPaintable(panelUv.x, panelUv.y) ||
                        surfaceA.IsPaintable(detailUv.x, detailUv.y))
                    {
                        _report.Add("FAIL A panel/detail hit test for " + kinds[k]);
                        return false;
                    }
                    float before = surfaceA.Progress(WashStage.Wet);
                    surfaceA.Paint(0.01f, 0.01f, 0.12f, WashStage.Wet);
                    surfaceA.Upload();
                    if (Math.Abs(surfaceA.Progress(WashStage.Wet) - before) > 0.0001f)
                    {
                        _report.Add("FAIL A off-body stroke changed progress for " + kinds[k]);
                        return false;
                    }
                }
                _report.Add("PASS A boundaries: panel mask excludes glass/wheels/lamps");

                // B - radius is 25% larger by default, configurable, clamped and
                // interpolated so a fast swipe leaves no gaps.
                float baseline = WashingTools.Radius(WashStage.Wet, 1f, 1f);
                float increased = WashingTools.Radius(WashStage.Wet, 1f, 1.25f);
                float ratio = increased / baseline;
                if (Mathf.Abs(ratio - 1.25f) > 0.001f ||
                    Mathf.Abs(WashingTools.Radius(WashStage.Wet, 1f, 0.1f) / baseline - 0.8f) > 0.001f ||
                    Mathf.Abs(WashingTools.Radius(WashStage.Wet, 1f, 9f) / baseline - 1.6f) > 0.001f ||
                    WashingTools.Radius(WashStage.Wet, 0.74f, 1.25f) >= increased)
                {
                    _report.Add("FAIL B radius ratio/clamp/scale: " + ratio.ToString("F3"));
                    return false;
                }
                Color32[] ignored;
                byte[] ignoredMask;
                var line = BuildSurface(CarKind.Sedan, 1f, out ignored, out ignoredMask);
                var stamps = BuildSurface(CarKind.Sedan, 1f, out ignored, out ignoredMask);
                float brush = increased;
                line.PaintLine(0.08f, 0.32f, 0.92f, 0.32f, brush, WashStage.Wet);
                for (int i = 0; i <= 80; i++)
                    stamps.Paint(Mathf.Lerp(0.08f, 0.92f, i / 80f), 0.32f, brush, WashStage.Wet);
                stamps.Upload();
                if (line.Progress(WashStage.Wet) < stamps.Progress(WashStage.Wet) * 0.75f)
                {
                    _report.Add("FAIL B interpolated stroke left gaps");
                    return false;
                }
                _report.Add("PASS B radius +25%, clamped 0.8-1.6, adaptive, gap-free");

                // Smooth completion must be gradual, not an instant texture swap.
                // BeginCompletion only finalises zones that already reached the
                // 82% gameplay threshold, so first bring every zone just above
                // it and leave a small remainder for the animation.
                var completion = BuildSurface(CarKind.Sedan, 1f, out ignored, out ignoredMask);
                PaintAlmostFinished(completion, WashStage.Wet);
                if (completion.MinZoneProgress(WashStage.Wet) < WashSurface.ZoneFinish ||
                    completion.MinZoneProgress(WashStage.Wet) >= 0.999f)
                {
                    _report.Add("FAIL completion setup progress=" +
                                completion.MinZoneProgress(WashStage.Wet).ToString("F3"));
                    return false;
                }
                completion.BeginCompletion(WashStage.Wet);
                completion.AdvanceCompletion(0.5f);
                float half = completion.Progress(WashStage.Wet);
                completion.AdvanceCompletion(1f);
                float done = completion.Progress(WashStage.Wet);
                if (half <= 0f || half >= 0.999f || done < 0.999f)
                {
                    _report.Add("FAIL smooth stage completion half=" + half.ToString("F3") +
                                " done=" + done.ToString("F3"));
                    return false;
                }
                _report.Add("PASS smooth completion: " + half.ToString("F2") + " -> " + done.ToString("F2"));

                // C-F - generate the nine required state images from the exact
                // overlay pixels that are uploaded to the car texture, then
                // compare them numerically so a parameter-only change cannot
                // be mistaken for a visible difference.
                Color32[] car;
                byte[] panelMask;
                var surface = BuildSurface(CarKind.Sedan, 1f, out car, out panelMask);
                var samples = new List<StateSample>();
                AddState(samples, "State_01_DryDirty", surface, car);
                PaintFraction(surface, WashStage.Wet, 0.45f);
                AddState(samples, "State_02_PartialWet", surface, car);
                Swipe(surface, WashStage.Wet);
                surface.FillCurrent(WashStage.Wet);
                AddState(samples, "State_03_FullWet", surface, car);
                PaintFraction(surface, WashStage.Foam, 0.45f);
                AddState(samples, "State_04_PartialFoam", surface, car);
                Swipe(surface, WashStage.Foam);
                surface.FillCurrent(WashStage.Foam);
                AddState(samples, "State_05_FullFoam", surface, car);
                PaintFraction(surface, WashStage.Scrub, 0.45f);
                AddState(samples, "State_06_PartialScrub", surface, car);
                Swipe(surface, WashStage.Rinse);
                surface.FillCurrent(WashStage.Rinse);
                AddState(samples, "State_07_CleanWet", surface, car);
                PaintFraction(surface, WashStage.Dry, 0.45f);
                AddState(samples, "State_08_PartialDry", surface, car);
                Swipe(surface, WashStage.Dry);
                surface.FillCurrent(WashStage.Dry);
                surface.FinishAll();
                surface.BeginFinalShine();
                surface.AdvanceFinalShine(0.5f);
                AddState(samples, "State_09_DryShiny", surface, car);
                surface.EndFinalShine();

                StateSample dry = samples[0], wet = samples[2], foam = samples[4],
                    rinse = samples[6], polished = samples[8];
                if (wet.B <= dry.B + 3f)
                {
                    _report.Add("FAIL C wetting is not visibly darker/cooler");
                    return false;
                }
                _report.Add("PASS C wetting changes dry matte dirt into dark glossy water");
                if (foam.R <= wet.R + 20f || foam.A <= wet.A)
                {
                    _report.Add("FAIL D foam is not visibly brighter than wet paint");
                    return false;
                }
                _report.Add("PASS D foam is visibly distinct from wet paint");
                if (rinse.A < 40f || rinse.B <= rinse.R)
                {
                    _report.Add("FAIL E rinse left no clean wet film");
                    return false;
                }
                _report.Add("PASS E sponge/rinse clear foam but keep a clean wet film");
                if (polished.R > 248f || polished.G > 248f || polished.B > 248f || polished.A < 40f)
                {
                    _report.Add("FAIL F dried paint is overbright or invisible");
                    return false;
                }
                _report.Add("PASS F drying ends polished, not white, with one shine sweep");
                for (int i = 1; i < samples.Count; i++)
                {
                    StateSample a = samples[i - 1], b = samples[i];
                    float delta = Mathf.Abs(a.R - b.R) + Mathf.Abs(a.G - b.G) +
                                  Mathf.Abs(a.B - b.B);
                    if (delta < 4f)
                    {
                        _report.Add("FAIL visual states identical: " + a.Name + " / " + b.Name);
                        return false;
                    }
                }
                _report.Add("PASS nine visual states are screenshots and visibly different");

                // G - all three car families and their real scales remain
                // washable with the same stage threshold and adaptive brush.
                float[] scales = { 0.74f, 0.82f, 1f };
                for (int i = 0; i < kinds.Length; i++)
                {
                    var family = BuildSurface(kinds[i], scales[i], out car, out panelMask);
                    int panels = 0;
                    for (int p = 0; p < panelMask.Length; p++) if (panelMask[p] == 255) panels++;
                    if (panels < 4000 || family.VisiblePixelCount() < 6000)
                    {
                        _report.Add("FAIL G no washable paint on " + kinds[i]);
                        return false;
                    }
                    WashStage[] familyStages = GameData.StagesFor(5);
                    for (int s = 0; s < familyStages.Length; s++)
                    {
                        Swipe(family, familyStages[s]);
                        if (family.MinZoneProgress(familyStages[s]) < WashSurface.ZoneFinish)
                        {
                            _report.Add("FAIL G " + kinds[i] + " cannot finish " + familyStages[s]);
                            return false;
                        }
                    }
                }
                _report.Add("PASS G sedan, sports car and large vehicle wash correctly");
                return true;
            }
            catch (Exception e)
            {
                _report.Add("FAIL mechanics/states exception: " + e);
                return false;
            }
        }

        private static bool FindMaskSamples(byte[] mask, out Vector2 panelUv, out Vector2 detailUv)
        {
            panelUv = detailUv = Vector2.zero;
            bool foundPanel = false, foundDetail = false;
            for (int y = 2; y < 254 && !(foundPanel && foundDetail); y++)
            {
                for (int x = 2; x < 382; x++)
                {
                    int i = y * 384 + x;
                    if (!foundPanel && IsRegion(mask, x, y, 255))
                    {
                        panelUv = new Vector2((x + 0.5f) / 384f, (y + 0.5f) / 256f);
                        foundPanel = true;
                    }
                    if (!foundDetail && IsRegion(mask, x, y, 0))
                    {
                        detailUv = new Vector2((x + 0.5f) / 384f, (y + 0.5f) / 256f);
                        foundDetail = true;
                    }
                    if (foundPanel && foundDetail) break;
                }
            }
            return foundPanel && foundDetail;
        }

        private static bool IsRegion(byte[] mask, int x, int y, byte value)
        {
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    if (mask[(y + oy) * 384 + x + ox] != value) return false;
            return true;
        }

        private static void AddState(List<StateSample> samples, string name, WashSurface surface,
            Color32[] car)
        {
            StateSample sample = SampleState(name, surface);
            samples.Add(sample);
            WriteStatePng(name, car, surface);
        }

        private static StateSample SampleState(string name, WashSurface surface)
        {
            var sample = new StateSample { Name = name };
            int count = 0;
            for (int y = 0; y < WashSurface.H; y++)
            {
                for (int x = 0; x < WashSurface.W; x++)
                {
                    Color32 p = surface.OverlayPixel(x, y);
                    if (p.a < 96) continue;
                    sample.R += p.r;
                    sample.G += p.g;
                    sample.B += p.b;
                    sample.A += p.a;
                    count++;
                }
            }
            if (count == 0) throw new Exception("No visible overlay in " + name);
            sample.R /= count;
            sample.G /= count;
            sample.B /= count;
            sample.A /= count;
            _report.Add("STATE " + name + " rgb=" + sample.R.ToString("F1") + "," +
                        sample.G.ToString("F1") + "," + sample.B.ToString("F1") +
                        " a=" + sample.A.ToString("F1"));
            return sample;
        }

        private static void WriteStatePng(string name, Color32[] car, WashSurface surface)
        {
            var pixels = new Color32[WashSurface.W * WashSurface.H];
            for (int y = 0; y < WashSurface.H; y++)
            {
                for (int x = 0; x < WashSurface.W; x++)
                {
                    int i = y * WashSurface.W + x;
                    Color32 o = surface.OverlayPixel(x, y);
                    Color32 c = car[i];
                    float a = o.a / 255f;
                    pixels[i] = new Color32(
                        (byte)Mathf.Clamp(o.r * a + c.r * (1f - a), 0f, 255f),
                        (byte)Mathf.Clamp(o.g * a + c.g * (1f - a), 0f, 255f),
                        (byte)Mathf.Clamp(o.b * a + c.b * (1f - a), 0f, 255f), 255);
                }
            }
            var texture = new Texture2D(WashSurface.W, WashSurface.H, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(_output, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void Swipe(WashSurface surface, WashStage stage)
        {
            for (int y = 0; y <= 20; y++)
                for (int x = 0; x <= 30; x++)
                    surface.Paint((x + 0.5f) / 31f, (y + 0.5f) / 21f,
                        WashingTools.Radius(stage, 1f, 1.25f), stage);
            surface.Upload();
        }

        /// <summary>Covers every zone just past ZoneFinish while leaving a
        /// visible remainder, which is the only state where the smooth finish
        /// animation is used by the game.</summary>
        private static void PaintAlmostFinished(WashSurface surface, WashStage stage)
        {
            float radius = WashingTools.Radius(stage, 1f, 1.25f);
            int columns = Mathf.Max(1, Mathf.CeilToInt(0.90f / (radius * 0.75f)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(0.74f / (radius * 1.00f)));
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                    surface.Paint(Mathf.Lerp(0.05f, 0.95f, x / (float)columns),
                        Mathf.Lerp(0.03f, 0.74f, y / (float)rows), radius, stage);
            surface.Upload();
        }

        private static void PaintFraction(WashSurface surface, WashStage stage, float fraction)
        {
            float radius = WashingTools.Radius(stage, 1f, 1.25f);
            int columns = Mathf.Max(1, Mathf.CeilToInt(1f / (radius * 0.85f)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(1f / (radius * 1.25f)));
            int limit = Mathf.Max(1, Mathf.RoundToInt(columns * fraction));
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= limit; x++)
                    surface.Paint((x + 0.5f) / (columns + 1f), (y + 0.5f) / (rows + 1f), radius, stage);
            surface.Upload();
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
            float radius = WashingTools.Radius(stage, 1f, 1.25f);
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
            if (!ok) Debug.LogError("[verify] FAIL " + reason);

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
