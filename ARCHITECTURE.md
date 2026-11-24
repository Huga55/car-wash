# Little Car Workshop — architecture contract (STEP 2 vertical slice)

Unity 2022.3.61f1, Built-in render pipeline, C# 9. All new runtime code lives under
`Assets/LittleGarage/`. Legacy `Assets/Scripts/*` is kept only as history and must
not be referenced or started by new systems; `Assets/Scenes/Game.unity` boots only
`LittleGarage.Bootstrap` (plus a scene Camera).

> Status legend (STEP 3 §9, 5 categories):
> **CODE** = implemented in code; **OFFLINE** = verified by offline checks;
> **UNITY** = verified in Unity Editor (import/compile/tests/PlayMode);
> **BUILD** = verified in a Windows build; **NOT** = still unverified / blocked.
> Current slice: runtime + tests + build scripts CODE + OFFLINE; BUILD = NOT.
> UNITY = partial: the user ran Unity 2022.3.61f1, which generated `ProjectSettings/*.asset`
> and reported two rounds of diagnostics: 13 `CS1069` (missing `particlesystem`/`physics`
> modules, fixed in `Packages/manifest.json`) and 2 `CS0117` plus 3 warnings
> (`ParticleSystemStopBehavior`, unused fields — fixed in code). Clean recompile not yet
> confirmed. Unity is not installed in this sandbox, so no further Editor/Build
> verification is possible here.
> `ProjectSettings/ProjectSettings.asset` was recovered by Unity Editor (not fabricated);
> it is untracked. Unity cleared `EditorBuildSettings.asset`, so `Game.unity` was restored
> as the single enabled build scene. See `REPORT.md` §§2–4 and roadmap §5.1.

## Design rules

* No monolith: every responsibility below is a separate class/file.
* All content is configured through ScriptableObjects in `Core/Definitions.cs`.
  `GameConfig.CreateDefault()` creates a complete working catalog in memory, so
  the game runs even if no assets were authored in the Editor.
* All 3D geometry, materials and effects are generated procedurally at runtime.
  No external/protected art is used.
* The body paint surface is an atlas texture: each paintable panel mesh has UVs
  inside a rect of one 256×256 state map. Raycasts use `hit.textureCoord`.
* Input goes through one `InputRouter`; UI consumes events through `UIManager`.
* Russian text only in player-facing strings.

## Namespace

Everything is in namespace `LittleGarage`.

## Core enums (`Core/GameTypes.cs`)

```csharp
public enum VehicleKind { Sedan, Sports, SUV, Pickup, Police, FireTruck, Ambulance, Truck, Bus, DumpTruck }
public enum PartKind { WheelFL, WheelFR, WheelRL, WheelRR, Engine, Battery, Windshield, HeadlightL, HeadlightR, MirrorL, MirrorR, Bumper }
public enum WashTool { Pressure, Foam, Sponge, Rinse, Dryer, Polisher }
public enum WorkZone { WashBay, RepairBay, PaintBay }
public enum TaskKind { Wash, Repair, Paint, Customize }
public enum SurfaceChannel { Dirty=0, Wet=1, Foam=2, Polish=3 } // texture RGBA
public enum ScreenId { Home, Garage, Play, Complete }
```

## Core definitions (`Core/Definitions.cs`)

```csharp
[CreateAssetMenu] public sealed class VehicleDef : ScriptableObject {
  public string id, displayNameRu;
  public VehicleKind kind;
  public Color bodyColor, accentColor;
  public float length=4.4f, width=1.85f, height=1.45f, wheelRadius=0.36f, dirt=0.9f;
  public Color[] colors;                       // selectable paint colors
  public string[] rimStyles;                   // "Стандарт", "Спорт", "Хром"
  public string[] decals;                      // "", "Полоса", "Звезда"
  public PartKind[] serviceableParts;          // parts this vehicle exposes
  public float cameraDistance=7.5f;
}
[CreateAssetMenu] public sealed class ToolDef : ScriptableObject {
  public WashTool tool; public string labelRu, hintRu, soundKey;
  public float radius=0.075f; public Color color;
}
[CreateAssetMenu] public sealed class JobTaskDef {
  public TaskKind kind; public string titleRu, instructionRu;
  public WashTool[] tools; public PartKind part;
  public Color paintColor; public bool optionalCustomization;
}
[CreateAssetMenu] public sealed class JobDef : ScriptableObject {
  public string id, titleRu, descriptionRu, vehicleId;
  public WorkZone zone; public JobTaskDef[] tasks; public int reward;
}
[CreateAssetMenu] public sealed class GameConfig : ScriptableObject {
  public VehicleDef[] vehicles; public JobDef[] jobs; public ToolDef[] tools;
  public static GameConfig CreateDefault();     // runtime fallback catalog
}
```

## Vehicle layer (`Vehicles/`)

```csharp
public sealed class VehiclePart : MonoBehaviour {
  public PartKind Kind; public bool Broken, Installed = true;
  public GameObject GoodVisual, DamagedVisual;
  public void SetBroken(bool v); public void SetInstalled(bool v);
}
public sealed class VehicleInstance : MonoBehaviour {
  public VehicleDef Def; public SurfaceStateSystem Surface;
  public Transform Root, HoodPivot;
  public readonly Dictionary<PartKind,VehiclePart> Parts;
  public void SetBodyColor(Color c); public void SetRimStyle(int i);
  public void SetDecal(int i); public void RestoreAppearance();
  public Transform WheelAnchor(PartKind k);
  public VehicleAppearance CaptureAppearance();
  public void ApplyAppearance(VehicleAppearance a);
}
public static class VehicleFactory {
  public static VehicleInstance Create(VehicleDef def, Transform parent);
}
public struct VehicleAppearance { public Color Body; public int Rim, Decal; }
```

`VehicleFactory` builds real 3D meshes (not flat shapes): rounded body shell,
hood, roof, glazing with transparent PBR-ish material, wheel arches, tyres with
sidewall/rim, head/tail lamps, mirrors, bumpers, interior, engine bay with
visible components. Panel meshes are paintable and carry MeshColliders; wheels
and serviceable parts carry `VehiclePart`.

## Garage layer (`Garage/`)

```csharp
public sealed class GarageEnvironment : MonoBehaviour {
  public void Build();                  // floor, tiled walls, ceiling lights,
                                        // drains, racks, lift, workbenches,
                                        // wash rings, wet floor patches
  public Transform BayAnchor(WorkZone z);
  public void SetZone(WorkZone z);      // moves emphasis lights/props
}
public sealed class GarageCamera : MonoBehaviour {
  public void Focus(Vector3 target, float distance);
  public void SetZone(WorkZone zone);
}
```

## Effects (`Effects/EffectFx.cs`)

```csharp
public static class EffectFx {
  public static ParticleSystem CreateSpray(Transform parent, Color color, float rate);
  public static ParticleSystem CreateFoam(Transform parent);
  public static ParticleSystem CreateDust(Transform parent);
  public static void Burst(ParticleSystem ps, Vector3 pos, int count);
}
```
Pooled, short-lived, max ~200 live particles total.

## Systems (`Systems/`)

```csharp
public sealed class SurfaceStateSystem {
  public Texture2D Map { get; }        // RGBA: R dirt, G wet, B foam, A polish
  public float Cleanliness { get; } public float FoamCoverage { get; }
  public float Dryness { get; } public float Polish { get; }
  public void Reset(VehicleDef def);
  public bool Paint(Vector2 uv, float radius, WashTool tool);
  public void BeginAutoFinish(); public bool AdvanceAutoFinish(float dt);
  public bool IsWashFinished();                // public for tests / completion guard
}
public sealed class InputRouter : MonoBehaviour {
  public event Action<Vector2> PointerDown, PointerMove, PointerUp;  // screen px
  public event Action<Vector2> OrbitDelta; public event Action<float> ZoomDelta;
  public Vector2 LastPosition { get; }
  public void SetPaintEnabled(bool v);
}
public sealed class ToolController : MonoBehaviour {
  public WashTool Current { get; }
  public void Select(WashTool t); public void SetAvailable(WashTool[] tools);
  public event Action<WashTool> Changed;
  public GameObject CreateHandTool(WashTool t);   // visible 3D tool model
}
public sealed class WashingSystem : MonoBehaviour {
  public float Progress { get; }
  public bool IsComplete { get; }
  public event Action Completed;
  public void Begin(VehicleInstance car, WashTool[] tools, SurfaceStateSystem surface);
  public void ApplyStroke(Vector2 uv, float dt);
  public void SetTool(WashTool t);                // cannot skip ahead
  public void SetToolRadius(WashTool tool, float radius);
  public void Advance(float dt);                  // auto-finish + stage flow
  public WashTool CurrentTool { get; } public bool IsComplete { get; }
}
public sealed class RepairSystem : MonoBehaviour {
  public float Progress { get; } public bool IsComplete { get; }
  public event Action Completed;
  public void Begin(VehicleInstance car, PartKind part);
  public void PointerDown(GameObject hitObject, Vector2 screen);
  public void PointerMove(Vector2 screen);
  public void PointerUp();
  public void Advance(float dt);
}
public sealed class CustomizationSystem : MonoBehaviour {
  public event Action Changed, Completed;
  public void Begin(VehicleInstance car);
  public void SetColor(int i); public void SetRim(int i); public void SetDecal(int i);
  public void Commit();
}
public sealed class ProgressManager {
  public int CompletedJobs { get; } public bool IsUnlocked(string jobId);
  public bool IsCompleted(string jobId); public VehicleAppearance GetSaved(string vehicleId);
  public void Complete(string jobId, int reward, VehicleAppearance look);
  public void ResetAll();
}
public sealed class SaveSystem { public void Save(ProgressData d); public ProgressData Load(); }
public sealed class LevelManager : MonoBehaviour {
  public JobDef Current { get; }
  public void StartJob(string id); public void CompleteCurrent();
  public event Action<JobDef> JobStarted; public event Action JobFinished;
}
public sealed class AudioManager : MonoBehaviour {
  public static AudioManager Instance { get; }
  public void Play(string key); public void SetLoop(string key, bool on, float intensity);
  public void PlayMusic(string track); public void SetVolume(string bus, float v);
  public float GetVolume(string bus);
}
public sealed class UIManager : MonoBehaviour {
  public void ShowHome(ProgressManager p); public void ShowGarage(ProgressManager p);
  public void ShowPlay(JobDef job, int step, int steps, string instruction,
                       string toolLabel, JobTaskDef activeTask = null);
  public void HideTools();
  public void SetProgress(float value); public void SetHint(string text);
  public void ShowComplete(string title, string body, int reward);
  public void ShowPause(bool v); public void ShowCustomization(VehicleDef def);
  public event Action<string> JobSelected; public event Action<string> ToolSelected;
  public event Action PauseRequested, ResumeRequested, RestartRequested, HomeRequested, NextRequested;
  public event Action<int> ColorSelected, RimSelected, DecalSelected;
  public event Action CustomizationCommitted;
}
```

## Orchestration (`GameManager.cs`, `Bootstrap.cs`)

`Bootstrap` is the only *game* component placed in `Game.unity` (a Camera is also
present); it constructs root objects and calls `GameManager.Initialize()`.
`GameManager` owns all managers, runs the job task sequence, moves the car between
bays, updates HUD, handles pause/restart and completion. It never implements
individual mechanics. After the final wash stage it runs a 7+ second finale
(gloss sweep, procedural driver/animal, explicit «Спасибо!», drive-away) before
showing the result screen; the finale coroutine/root is stopped on restart/home/destroy.

## Editor / QA (5-category status)

* `Assets/Editor/BuildScript.cs` — CODE: `BuildWindows()`/`BuildWindowsCI()`, strict
  `Application.unityVersion` check, scene-0 GUID validation, timestamped logs, file-only
  `.exe` verification. OFFLINE: path/version/grep + stubs compile. UNITY/BUILD: NOT.
* `build-windows.ps1` + `BUILD_WINDOWS.md` — CODE: direct `Unity.exe`, exact-version
  enforcement, no silent fallback. OFFLINE: structural checks. UNITY/BUILD: NOT.
* `.github/workflows/build.yml` — CODE: `game-ci/unity-test-runner@v4` (EditMode+PlayMode)
  gate, no legacy `VisualVerification.Run`, exact `Builds/Windows/LittleCarWorkshop.exe`
  artifact paths. Executed in GH Actions: NOT (no license/secrets here).
* `Assets/LittleGarage/Tests/Editor` and `Tests/PlayMode` — CODE + OFFLINE compile:
  catalog, vehicle/UV coverage, six-stage wash cycle, tool lock, auto-finish,
  save/load, corrupt-save fallback, part state, scene boot smoke. Unity Editor tests: NOT.
* `Packages/manifest.json` (CODE): `com.unity.test-framework 1.1.33`, `com.unity.ugui`,
  plus built-in modules — `ui`, `imgui`, `imageconversion`, `audio`, `unitywebrequest`,
  `jsonserialize`, `androidjni`, `uielements`, and (added after real Unity CS1069 errors)
  `particlesystem`, `physics`, `screencapture`. The last three fix forwarded-type compile
  errors for `ParticleSystem`, `RaycastHit`, `MeshCollider`, `ScreenCapture`.
  Fix applied and manifest JSON validated; Unity recompile not yet confirmed (NOT).
* `Documentation/Rebuild/PLAYMODE_VERIFICATION_CHECKLIST.md` — CODE (manual checklist
  with 6 screenshots); Play Mode execution/screenshots: NOT.
* Blockers: missing `ProjectSettings/*.asset` (recovery via Editor §5.1, in person);
  Unity 2022.3.61f1 + Windows Build Support + license absent → no import/compile/
  Editor tests/PlayMode/build verification.
* Legacy `Assets/Editor/VisualVerification.cs` still compiles in Editor scope but is no
  longer invoked by CI; if it fails Unity compile it must be fixed there (NOT verified).
