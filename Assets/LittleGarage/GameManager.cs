using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>Screen orchestrator and job sequencer. It owns the other managers
    /// and dispatches input, but never implements a wash or repair mechanic itself.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameManager : MonoBehaviour
    {
        private GameConfig _config;
        private ProgressManager _progress;
        private LevelManager _levels;
        private GarageEnvironment _garage;
        private GarageCamera _cameraRig;
        private UIManager _ui;
        private AudioManager _audio;
        private InputRouter _input;
        private ToolController _tools;
        private WashingSystem _washing;
        private RepairSystem _repair;
        private CustomizationSystem _custom;

        private VehicleInstance _car;
        private SurfaceStateSystem _surface;
        private ParticleSystem _waterFx;
        private ParticleSystem _foamFx;
        private Material _bodyMaterial;
        private Camera _camera;

        private ScreenId _screen;
        private bool _paused;
        private bool _pointerDown;
        private bool _advancing;
        private float _advanceTimer;
        private int _taskIndex;
        private Coroutine _finaleCoroutine;
        private GameObject _finaleRoot;
        private string _activeLoop;
        private string _currentJobId;
        private int _paintFrame;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private Action _onPauseRequested;
        private Action _onResumeRequested;
        private Action _onRestartRequested;
        private Action _onHomeRequested;
        private Action _onNextRequested;
        private Action _onGarageRequested;
        private Action<int> _onColorSelected;
        private Action<int> _onRimSelected;
        private Action<int> _onDecalSelected;
        private Action _onCustomizationCommitted;

        public VehicleInstance CurrentVehicle { get { return _car; } }
        public JobDef CurrentJob { get { return _levels != null ? _levels.Current : null; } }
        public bool IsPaused { get { return _paused; } }

        public void Initialize(GameConfig config)
        {
            _config = config != null ? config : GameConfig.CreateDefault();
            if (_config.vehicles == null || _config.vehicles.Length == 0 || _config.jobs == null || _config.jobs.Length == 0)
            {
                Debug.LogError("LittleGarage: content catalog is empty");
                return;
            }

            _progress = new ProgressManager(_config, new SaveSystem());
            _levels = gameObject.AddComponent<LevelManager>();
            _levels.Init(_config);

            _garage = new GameObject("Garage Environment").AddComponent<GarageEnvironment>();
            _garage.Build();

            _cameraRig = FindObjectOfType<GarageCamera>();
            if (_cameraRig == null)
                _cameraRig = new GameObject("Garage Camera").AddComponent<GarageCamera>();
            _camera = Camera.main;

            _waterFx = EffectFx.CreateSpray(_garage.transform, new Color(0.65f, 0.88f, 1f), 90f);
            _foamFx = EffectFx.CreateFoam(_garage.transform);

            _audio = GetComponent<AudioManager>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioManager>();

            _ui = GetComponent<UIManager>();
            if (_ui == null) _ui = gameObject.AddComponent<UIManager>();
            _ui.JobSelected += OnJobSelected;
            _ui.ToolSelected += OnToolSelected;

            _onPauseRequested = () => SetPaused(true);
            _onResumeRequested = () => SetPaused(false);
            _onRestartRequested = () => StartJob(_currentJobId);
            _onHomeRequested = ShowHome;
            _onNextRequested = StartNextJob;
            _onGarageRequested = ShowGarage;
            _onColorSelected = i => { if (_custom != null) _custom.SetColor(i); };
            _onRimSelected = i => { if (_custom != null) _custom.SetRim(i); };
            _onDecalSelected = i => { if (_custom != null) _custom.SetDecal(i); };
            _onCustomizationCommitted = OnCustomizationCommitted;

            _ui.PauseRequested += _onPauseRequested;
            _ui.ResumeRequested += _onResumeRequested;
            _ui.RestartRequested += _onRestartRequested;
            _ui.HomeRequested += _onHomeRequested;
            _ui.NextRequested += _onNextRequested;
            _ui.GarageRequested += _onGarageRequested;
            _ui.ColorSelected += _onColorSelected;
            _ui.RimSelected += _onRimSelected;
            _ui.DecalSelected += _onDecalSelected;
            _ui.CustomizationCommitted += _onCustomizationCommitted;

            _input = gameObject.AddComponent<InputRouter>();
            _input.PointerDown += OnPointerDown;
            _input.PointerMove += OnPointerMove;
            _input.PointerUp += OnPointerUp;
            _input.OrbitDelta += d => { if (!_paused) _cameraRig.Orbit(d); };
            _input.ZoomDelta += d => { if (!_paused) _cameraRig.Zoom(d); };

            _tools = gameObject.AddComponent<ToolController>();
            _tools.Changed += OnToolChanged;

            _washing = gameObject.AddComponent<WashingSystem>();
            _washing.Completed += OnTaskCompleted;
            _repair = gameObject.AddComponent<RepairSystem>();
            _repair.Completed += OnTaskCompleted;
            _custom = gameObject.AddComponent<CustomizationSystem>();
            _custom.Completed += OnTaskCompleted;

            ShowHome();
        }

        private void Update()
        {
            if (_paused || _screen != ScreenId.Play || _car == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (_washing != null) _washing.Advance(dt);
            if (_repair != null) _repair.Advance(dt);
            if (_car != null) _car.Tick(dt);

            if (_advancing)
            {
                _advanceTimer -= dt;
                if (_advanceTimer <= 0f)
                {
                    _advancing = false;
                    BeginTask(_taskIndex);
                }
            }

            if (_washing != null && !_washing.IsComplete)
                _ui.SetProgress(Mathf.Clamp01(_washing.Progress));
            else if (_repair != null && !_repair.IsComplete && CurrentTaskKind() == TaskKind.Repair)
                _ui.SetProgress(Mathf.Clamp01(_repair.Progress));

            if (!_pointerDown) StopEffects();
        }

        private TaskKind CurrentTaskKind()
        {
            JobDef job = _levels.Current;
            if (job == null || job.tasks == null || _taskIndex < 0 || _taskIndex >= job.tasks.Length)
                return TaskKind.Customize;
            return job.tasks[_taskIndex].kind;
        }

        public void ShowHome()
        {
            _screen = ScreenId.Home;
            _paused = false;
            _pointerDown = false;
            StopFinale();
            StopEffects();
            DestroyCar();
            _garage.SetZone(WorkZone.WashBay);
            _ui.ShowHome(_progress);
            _audio.PlayMusic("menu");
        }

        public void ShowGarage()
        {
            _screen = ScreenId.Garage;
            _paused = false;
            StopFinale();
            StopEffects();
            DestroyCar();
            _ui.ShowGarage(_progress);
            _audio.PlayMusic("menu");
        }

        public void StartJob(string jobId)
        {
            JobDef job = _config.Job(jobId);
            if (job == null) return;

            _currentJobId = jobId;
            _paused = false;
            _screen = ScreenId.Play;
            _advancing = false;
            _taskIndex = 0;
            _pointerDown = false;
            StopFinale();
            StopEffects();
            _input.SetPaintEnabled(true);

            DestroyCar();
            VehicleDef def = _config.Vehicle(job.vehicleId);
            if (def == null)
            {
                Debug.LogError("LittleGarage: job references a missing vehicle: " + job.vehicleId);
                return;
            }

            _car = VehicleFactory.Create(def, _garage.transform);
            _car.name = "Vehicle_" + def.id;
            Vector3 anchor = _garage.BayAnchor(job.zone).position;
            _car.transform.position = anchor;
            _car.transform.rotation = Quaternion.Euler(0f, -32f, 0f);

            VehicleAppearance look = _progress.GetSaved(def.id);
            if (!look.IsEmpty) _car.ApplyAppearance(look);

            _surface = new SurfaceStateSystem();
            _surface.Reset(def);
            _car.Surface = _surface;
            Color bodyColor = !look.IsEmpty ? look.Body : def.bodyColor;
            ApplySurfaceMaterial(_car, _surface, bodyColor);

            _garage.SetZone(job.zone);
            _cameraRig.SetZone(job.zone);
            _cameraRig.Focus(_car.transform.position + Vector3.up * def.height * 0.45f, def.cameraDistance);
            if (_camera == null) _camera = Camera.main;

            _audio.PlayMusic("wash");
            _levels.StartJob(jobId);
            BeginTask(0);
        }

        private void BeginTask(int index)
        {
            JobDef job = _levels.Current;
            if (job == null || job.tasks == null || index >= job.tasks.Length) return;
            JobTaskDef task = job.tasks[index];
            _taskIndex = index;

            string toolLabel = string.Empty;
            switch (task.kind)
            {
                case TaskKind.Wash:
                    if (task.tools == null || task.tools.Length == 0) { OnTaskCompleted(); return; }
                    _washing.Begin(_car, task.tools, _surface);
                    for (int i = 0; i < task.tools.Length; i++)
                    {
                        ToolDef td = _config.Tool(task.tools[i]);
                        if (td != null && td.radius > 0f) _washing.SetToolRadius(task.tools[i], td.radius);
                    }
                    _tools.SetAvailable(task.tools);
                    _tools.Select(task.tools[0]);
                    toolLabel = task.tools[0].ToString();
                    break;
                case TaskKind.Repair:
                    _tools.SetAvailable(new WashTool[0]);
                    _repair.Begin(_car, task.part);
                    toolLabel = "Инструмент repair";
                    break;
                case TaskKind.Paint:
                case TaskKind.Customize:
                    _tools.SetAvailable(new WashTool[0]);
                    _custom.Begin(_car);
                    toolLabel = "Палитра";
                    break;
            }

            _ui.ShowPlay(job, index + 1, job.tasks.Length, task.instructionRu, toolLabel, task);
            _ui.SetHint(task.titleRu);
            _ui.SetProgress(0f);

            if (task.kind == TaskKind.Paint || task.kind == TaskKind.Customize)
            {
                _ui.ShowCustomization(_car.Def);
                if (task.kind == TaskKind.Paint && task.paintColor != default(Color))
                    _custom.SetColor(ClosestColor(_car.Def, task.paintColor));
            }
        }

        private static int ClosestColor(VehicleDef def, Color c)
        {
            if (def.colors == null || def.colors.Length == 0) return 0;
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < def.colors.Length; i++)
            {
                Color d = def.colors[i] - c;
                float dist = d.r * d.r + d.g * d.g + d.b * d.b;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        private string ToolName(WashTool tool)
        {
            ToolDef def = _config.Tool(tool);
            return def != null ? def.labelRu : tool.ToString();
        }

        private void OnJobSelected(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            if (_progress != null && !_progress.IsUnlocked(jobId)) return;
            StartJob(jobId);
        }

        private void OnToolSelected(string name)
        {
            WashTool tool;
            if (!Enum.TryParse(name, out tool)) return;
            _tools.Select(tool);
        }

        private void OnToolChanged(WashTool tool)
        {
            if (_washing != null && CurrentTaskKind() == TaskKind.Wash)
                _washing.SetTool(tool);

            WashTool active = (_washing != null && CurrentTaskKind() == TaskKind.Wash) ? _washing.CurrentTool : tool;
            ToolDef def = _config.Tool(active);
            if (def != null)
            {
                _ui.SetHint(def.hintRu);
                JobDef job = CurrentJob;
                JobTaskDef task = (job != null && job.tasks != null && _taskIndex >= 0 && _taskIndex < job.tasks.Length)
                    ? job.tasks[_taskIndex] : null;
                string instruction = task != null ? task.instructionRu : def.hintRu;
                int steps = (job != null && job.tasks != null) ? job.tasks.Length : 1;
                _ui.ShowPlay(job, _taskIndex + 1, steps, instruction, active.ToString(), task);
                if (_washing != null)
                    _ui.SetProgress(Mathf.Clamp01(_washing.Progress));
            }
            StopLoop();
        }

        private void OnCustomizationCommitted()
        {
            if (CurrentTaskKind() == TaskKind.Paint || CurrentTaskKind() == TaskKind.Customize)
            {
                // Tool-driven paint is a future extension; no tool state is needed here.
                OnTaskCompleted();
            }
        }

        private void OnTaskCompleted()
        {
            if (_advancing || _screen != ScreenId.Play) return;
            StopEffects();
            _ui.SetProgress(1f);
            _audio.Play("complete_task");
            JobDef job = _levels.Current;
            if (job == null) return;

            if (_taskIndex + 1 < job.tasks.Length)
            {
                _advancing = true;
                _advanceTimer = 0.9f;
                _ui.SetHint("Отлично! Готовим следующий этап…");
            }
            else
            {
                CompleteJob();
            }
        }

        private void CompleteJob()
        {
            JobDef job = _levels.Current;
            if (job == null) return;
            VehicleAppearance look = _car != null ? _car.CaptureAppearance() : default(VehicleAppearance);
            _progress.Complete(job.id, job.reward, look);
            _levels.CompleteCurrent();
            StopEffects();
            _screen = ScreenId.Complete;
            _input.SetPaintEnabled(false);
            _tools.SetAvailable(new WashTool[0]);
            _ui.HideTools();
            _audio.PlayMusic("finale");
            _audio.Play("complete_job");
            _ui.SetHint("Отличная работа! Смотри на результат.");
            StartFinale(job);
        }

        private void StartFinale(JobDef job)
        {
            StopFinale();
            _finaleRoot = new GameObject("Finale Showcase");
            _finaleCoroutine = StartCoroutine(FinaleRoutine(job));
        }

        private void StopFinale()
        {
            if (_finaleCoroutine != null)
            {
                StopCoroutine(_finaleCoroutine);
                _finaleCoroutine = null;
            }
            if (_finaleRoot != null)
            {
                Destroy(_finaleRoot);
                _finaleRoot = null;
            }
        }

        private System.Collections.IEnumerator FinaleRoutine(JobDef job)
        {
            // Mandatory 7+ second showcase: gloss sweep, driver/animal, thank-you, drive-off, then result screen.
            float start = Time.realtimeSinceStartup;
            if (_car != null)
            {
                float focusY = _car.Def != null ? _car.Def.height * 0.45f : 0.7f;
                _cameraRig.Focus(_car.transform.position + Vector3.up * focusY,
                    (_car.Def != null ? _car.Def.cameraDistance : 7.2f) * 0.88f);
            }

            // 1. Clean body + a visible highlight pass.
            _ui.SetHint("Чистый кузов! Смотри, как проходит блик.");
            Light gloss = CreateGlossSweep();
            float sweep = 0f;
            while (sweep < 1.35f)
            {
                sweep += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(sweep / 1.35f);
                if (_car != null && gloss != null)
                {
                    float len = _car.Def != null ? _car.Def.length : 4.2f;
                    float top = _car.Def != null ? _car.Def.height : 1.4f;
                    Vector3 local = new Vector3(Mathf.Lerp(-len * 0.58f, len * 0.58f, t), top * 0.85f, 0f);
                    gloss.transform.position = _car.transform.TransformPoint(local);
                    gloss.transform.rotation = _car.transform.rotation * Quaternion.Euler(58f, 0f, 0f);
                    gloss.intensity = Mathf.Sin(t * Mathf.PI) * 2.6f;
                }
                yield return null;
            }
            if (gloss != null) gloss.intensity = 0f;
            if (_foamFx != null && _car != null)
                EffectFx.Burst(_foamFx, _car.transform.position + Vector3.up * 1.4f, 40);
            yield return new WaitForSecondsRealtime(0.25f);

            // 2. Procedural driver + animal appear (temporary placeholder art).
            GameObject driver = null;
            GameObject animal = null;
            if (_car != null)
            {
                driver = CreateFinaleCharacter("Driver", new Color(0.95f, 0.82f, 0.68f, 1f),
                    new Color(0.22f, 0.38f, 0.72f, 1f),
                    _car.transform.position + new Vector3(-1.8f, 0f, 1.4f), _finaleRoot.transform);
                animal = CreateFinaleAnimal(_car.transform.position + new Vector3(-2.4f, 0f, 0.9f), _finaleRoot.transform);
                FaceCharacter(driver, _car.transform.position + Vector3.up * 0.4f);
                FaceCharacter(animal, _car.transform.position + Vector3.up * 0.2f);
            }

            _ui.SetHint("Водитель и питомец радуются чистой машине!");
            yield return new WaitForSecondsRealtime(1.0f);

            Vector3 driverStart = driver != null ? driver.transform.position : Vector3.zero;
            Vector3 animalStart = animal != null ? animal.transform.position : Vector3.zero;
            Vector3 driverTarget = _car != null
                ? _car.transform.position + _car.transform.TransformDirection(new Vector3(-0.45f, 0f, 0.55f))
                : driverStart;
            Vector3 animalTarget = _car != null
                ? _car.transform.position + _car.transform.TransformDirection(new Vector3(-0.85f, 0f, 0.35f))
                : animalStart;

            // 3. Happy hop while approaching the car, with an explicit thank-you.
            float approach = 0f;
            while (approach < 1.2f)
            {
                approach += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(approach / 1.2f));
                if (driver != null) MoveCharacter(driver, Vector3.Lerp(driverStart, driverTarget, t), t);
                if (animal != null) MoveCharacter(animal, Vector3.Lerp(animalStart, animalTarget, t), t);
                yield return null;
            }
            _ui.SetHint("Спасибо! Машинка снова блестит!");
            yield return new WaitForSecondsRealtime(0.8f);

            // 4. Car leaves the bay while the camera follows.
            _audio.Play("engine");
            Vector3 carStart = _car != null ? _car.transform.position : Vector3.zero;
            Quaternion carRotStart = _car != null ? _car.transform.rotation : Quaternion.identity;
            Vector3 carTarget = carStart + (_car != null ? _car.transform.forward * 8f : Vector3.forward * 8f);
            float drive = 0f;
            while (drive < 2.4f)
            {
                drive += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(drive / 2.4f);
                if (_car != null)
                {
                    _car.transform.position = Vector3.Lerp(carStart, carTarget, Mathf.SmoothStep(0f, 1f, t));
                    _car.transform.rotation = Quaternion.Slerp(carRotStart, carRotStart * Quaternion.Euler(0f, -8f * t, 0f), 0.18f);
                    _car.Tick(Time.unscaledDeltaTime);
                }
                _cameraRig.Orbit(new Vector2(Time.unscaledDeltaTime * 18f, 0f));
                if (animal != null) MoveCharacter(animal, animalTarget, drive);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.6f);
            float elapsed = Time.realtimeSinceStartup - start;
            if (elapsed < 7f)
                yield return new WaitForSecondsRealtime(7f - elapsed);

            // 5. Only now show the result screen.
            _ui.ShowComplete("Машина как новая!", job.titleRu + " — работа выполнена", job.reward);
            _finaleCoroutine = null;
            if (_finaleRoot != null)
            {
                Destroy(_finaleRoot);
                _finaleRoot = null;
            }
        }

        private Light CreateGlossSweep()
        {
            GameObject go = new GameObject("GlossSweep");
            go.transform.SetParent(_finaleRoot != null ? _finaleRoot.transform : transform, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.97f, 0.86f, 1f);
            light.intensity = 0f;
            light.range = 8f;
            light.spotAngle = 34f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static GameObject CreateFinaleCharacter(string name, Color skin, Color clothes, Vector3 pos, Transform parent)
        {
            GameObject root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = pos;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            RemoveCollider(body);
            SetColor(body, clothes);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            head.transform.localScale = Vector3.one * 0.32f;
            RemoveCollider(head);
            SetColor(head, skin);
            return root;
        }

        private static GameObject CreateFinaleAnimal(Vector3 pos, Transform parent)
        {
            GameObject root = new GameObject("Animal");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = pos;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            body.transform.localScale = new Vector3(0.28f, 0.22f, 0.42f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            RemoveCollider(body);
            SetColor(body, new Color(0.82f, 0.67f, 0.45f, 1f));
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.35f, 0.22f);
            head.transform.localScale = Vector3.one * 0.22f;
            RemoveCollider(head);
            SetColor(head, new Color(0.85f, 0.7f, 0.48f, 1f));
            GameObject earL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            earL.transform.SetParent(root.transform, false);
            earL.transform.localPosition = new Vector3(-0.06f, 0.48f, 0.2f);
            earL.transform.localScale = new Vector3(0.04f, 0.07f, 0.04f);
            RemoveCollider(earL);
            SetColor(earL, new Color(0.55f, 0.42f, 0.28f, 1f));
            GameObject earR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            earR.transform.SetParent(root.transform, false);
            earR.transform.localPosition = new Vector3(0.06f, 0.48f, 0.2f);
            earR.transform.localScale = new Vector3(0.04f, 0.07f, 0.04f);
            RemoveCollider(earR);
            SetColor(earR, new Color(0.55f, 0.42f, 0.28f, 1f));
            return root;
        }

        private static void MoveCharacter(GameObject root, Vector3 pos, float t)
        {
            if (root == null) return;
            root.transform.position = pos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 12f)) * 0.03f;
        }

        private static void FaceCharacter(GameObject root, Vector3 target)
        {
            if (root == null) return;
            Vector3 dir = target - root.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private static void SetCharacterVisible(GameObject root, bool visible)
        {
            if (root == null) return;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].enabled = visible;
        }

        private static void RemoveCollider(GameObject go)
        {
            if (go == null) return;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private static void SetColor(GameObject go, Color c)
        {
            if (go == null) return;
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material m = new Material(shader);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            r.sharedMaterial = m;
        }

        private void StartNextJob()
        {
            JobDef job = _levels.Current;
            string next = job != null ? _levels.NextJobId() : null;
            if (!string.IsNullOrEmpty(next)) StartJob(next);
            else ShowGarage();
        }

        public void SetPaused(bool value)
        {
            if (_screen != ScreenId.Play) return;
            _paused = value;
            _pointerDown = false;
            StopEffects();
            _ui.ShowPause(value);
            if (value) _audio.Play("ui_click");
        }

        private void OnPointerDown(Vector2 screen)
        {
            if (_paused || _screen != ScreenId.Play || _car == null) return;
            if (_ui != null && _ui.IsPointerOverUI(screen)) return;
            _pointerDown = true;
            RaycastHit hit;
            if (!TryHit(screen, out hit)) return;

            if (CurrentTaskKind() == TaskKind.Wash)
               PaintAt(screen, hit, 0f);
            else if (CurrentTaskKind() == TaskKind.Repair)
                _repair.PointerDown(hit.collider != null ? hit.collider.gameObject : null, screen);
        }

        private void OnPointerMove(Vector2 screen)
        {
            if (!_pointerDown || _paused || _screen != ScreenId.Play || _car == null) return;
            RaycastHit hit;
            if (!TryHit(screen, out hit)) { StopEffects(); return; }
            if (CurrentTaskKind() == TaskKind.Wash)
                PaintAt(screen, hit, Mathf.Min(Time.deltaTime, 0.05f));
            else if (CurrentTaskKind() == TaskKind.Repair)
                _repair.PointerMove(screen);
        }

        private void OnPointerUp(Vector2 screen)
        {
            if (!_pointerDown) return;
            _pointerDown = false;
            if (CurrentTaskKind() == TaskKind.Repair) _repair.PointerUp();
            StopEffects();
        }

        private bool TryHit(Vector2 screen, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;
            Ray ray = _camera.ScreenPointToRay(screen);
            int count = Physics.RaycastNonAlloc(ray, _hits, 100f);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider == null) continue;
                if (_hits[i].collider.GetComponentInParent<VehicleInstance>() != _car) continue;
                hit = _hits[i];
                return true;
            }
            return false;
        }

        private void PaintAt(Vector2 screen, RaycastHit hit, float dt)
        {
            if (_washing == null || _washing.IsComplete) return;
            Vector2 uv = hit.textureCoord;
            _washing.ApplyStroke(uv, dt);

            WashTool tool = _washing.CurrentTool;
            string sound = SoundForTool(tool);
            _audio.SetLoop(sound, true, 0.85f);
            _activeLoop = sound;

            ParticleSystem ps = tool == WashTool.Foam || tool == WashTool.Sponge ? _foamFx : _waterFx;
            if (ps != null && (_paintFrame++ % 2 == 0))
            {
                Vector3 pos = hit.point + hit.normal * 0.06f;
                EffectFx.Burst(ps, pos, tool == WashTool.Dryer || tool == WashTool.Polisher ? 2 : 4);
            }
        }

        private static string SoundForTool(WashTool tool)
        {
            switch (tool)
            {
                case WashTool.Foam: return "foam";
                case WashTool.Sponge: return "sponge";
                case WashTool.Rinse: return "rinse";
                case WashTool.Dryer: return "air";
                case WashTool.Polisher: return "polish";
                default: return "water";
            }
        }

        private void StopLoop()
        {
            if (!string.IsNullOrEmpty(_activeLoop))
            {
                _audio.SetLoop(_activeLoop, false, 0f);
                _activeLoop = null;
            }
        }

        private void StopEffects()
        {
            StopLoop();
            if (_waterFx != null) _waterFx.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (_foamFx != null) _foamFx.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ApplySurfaceMaterial(VehicleInstance car, SurfaceStateSystem surface, Color color)
        {
            if (car == null || surface == null) return;
            Shader shader = Shader.Find("LittleGarage/CarSurface");
            if (shader == null)
                shader = Shader.Find("Standard");

            bool shaderChanged = _bodyMaterial == null || (shader != null && _bodyMaterial.shader != shader);
            if (shaderChanged)
            {
                _bodyMaterial = shader != null ? new Material(shader) : null;
                if (_bodyMaterial != null) _bodyMaterial.name = "Car Body (stateful)";
            }
            if (_bodyMaterial == null) return;

            _bodyMaterial.SetColor("_Color", color);
            _bodyMaterial.SetColor("_BaseColor", color);
            if (surface.Map != null)
            {
                _bodyMaterial.SetTexture("_StateMap", surface.Map);
                _bodyMaterial.SetTexture("_DirtMap", surface.Map);
            }

            MeshRenderer[] renderers = car.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;
                if (r.GetComponent<MeshCollider>() == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    string n = mat.name != null ? mat.name : string.Empty;
                    if (n.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mats[m] = _bodyMaterial;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private void DestroyCar()
        {
            StopEffects();
            if (_car != null)
            {
                Destroy(_car.gameObject);
                _car = null;
            }
            _surface = null;
        }

        private void OnDestroy()
        {
            StopFinale();
            if (_ui != null)
            {
                _ui.JobSelected -= OnJobSelected;
                _ui.ToolSelected -= OnToolSelected;
                if (_onPauseRequested != null) _ui.PauseRequested -= _onPauseRequested;
                if (_onResumeRequested != null) _ui.ResumeRequested -= _onResumeRequested;
                if (_onRestartRequested != null) _ui.RestartRequested -= _onRestartRequested;
                if (_onHomeRequested != null) _ui.HomeRequested -= _onHomeRequested;
                if (_onNextRequested != null) _ui.NextRequested -= _onNextRequested;
                if (_onGarageRequested != null) _ui.GarageRequested -= _onGarageRequested;
                if (_onColorSelected != null) _ui.ColorSelected -= _onColorSelected;
                if (_onRimSelected != null) _ui.RimSelected -= _onRimSelected;
                if (_onDecalSelected != null) _ui.DecalSelected -= _onDecalSelected;
                if (_onCustomizationCommitted != null) _ui.CustomizationCommitted -= _onCustomizationCommitted;
            }

            if (_levels != null)
            {
                _levels.JobStarted -= null;
                _levels.JobFinished -= null;
            }
        }
    }
}
