# Отчёт STEP 3: Unity recovery, validation & Windows build

Дата: 2026-11-24  
Unity target: **2022.3.61f1**, Built-in Render Pipeline  
Рабочая директория: `/e/my/wash-cars`

> **STEP 4 final CI verification (2026-11-24):** commit `85282fb3f028feb69c6e8a40625691b2f139ef95`
> pushed to `master`, workflow run
> [45432416224](https://github.com/Huga55/car-wash/actions/runs/45432416224) completed
> successfully. Real Unity 2022.3.61f1 tests: EditMode **17/17**, PlayMode **8/8**, total
> **25/25 passed**. Android build, StandaloneWindows64 build and release publish all succeeded;
> the downloaded Windows CI artifact and published `LittleCarWash-windows.zip` are full player
> directories. Game View screenshots and physical-device checks were still not performed.

## 1. Что изменилось в этом шаге

**Никаких новых gameplay-фич не добавлялось** (STEP 3 §7). Что сделано:

- `README.md`, `REPORT.md`, `ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md` — переведены
  на **5-категорийный статус** (реализовано / offline / Unity Editor / Windows build / не проверено).
- Добавлен `Documentation/Rebuild/PLAYMODE_VERIFICATION_CHECKLIST.md` — точный ручной
  прогон с 6 точками скриншотов (§6).
- **Исправлены реальные Unity compile-ошибки из `prompt.txt`**: в `Packages/manifest.json`
  добавлены built-in модули `com.unity.modules.particlesystem`, `com.unity.modules.physics`
  (13 ошибок `CS1069`) и `com.unity.modules.screencapture` (проактивно для PlayMode-теста).
- **Восстановлен build scene list**: пользовательский Unity запуск перезаписал
  `ProjectSettings/EditorBuildSettings.asset` в `m_Scenes: []`; возвращена запись
  `Assets/Scenes/Game.unity` (guid `bbbb…`, enabled 1).
- Исходный код STEP 2 не изменялся: `git status` сохраняет все предыдущие модификации
  и untracked-файлы; не было `reset`/`clean`/commit/push.

## 2. Проверка ProjectSettings (§2)

### Исходное состояние (до запуска Unity пользователем)

`ProjectSettings/ProjectSettings.asset` отсутствовал, не был в `.gitignore` и никогда не
коммитился; копий в `/e`, `/tmp`, Docker и альтернативного источника настроек не было.
Поэтому поддельный `.asset` не создавался.

### Фактический результат: восстановление выполнено пользователем в Unity

После того как пользователь открыл проект в Unity, Editor сгенерировал полный набор файлов:
`ProjectSettings.asset`, `AudioManager.asset`, `DynamicsManager.asset`, `EditorSettings.asset`,
`GraphicsSettings.asset`, `InputManager.asset`, `MemorySettings.asset`, `NavMeshAreas.asset`,
`PackageManagerSettings.asset`, `Physics2DSettings.asset`, `PresetManager.asset`,
`QualitySettings.asset`, `TagManager.asset`, `TimeManager.asset`, `UnityConnectSettings.asset`,
`VFXManager.asset`, `VersionControlSettings.asset`, `ClusterInputManager.asset`,
а также `Packages/packages-lock.json` и `Assets/Editor/VisualVerification.cs.meta`.
Это **реальный вывод Unity**, а не ручная фабрикация.

Проверено содержимое сгенерированного:

| Настройка | Значение | Совместимость |
|---|---|---|
| `ProjectVersion.txt` | `m_EditorVersion: 2022.3.61f1`, revision `6c53ebaf375d` | ✅ точная версия подтверждена |
| `activeInputHandler` (ProjectSettings.asset) | `0` = Input Manager (Old) | ✅ ожидает `InputRouter` |
| `InputManager.asset` | ось `Mouse ScrollWheel` присутствует | ✅ scroll zoom работает |
| `GraphicsSettings.asset` | `m_CustomRenderPipeline: {fileID: 0}` | ✅ Built-in RP, не URP/HDRP |
| `EditorBuildSettings.asset` | изначально `m_Scenes: []` | ❌→✅ исправлено вручную: возвращён `Game.unity`, guid `bbbb…` |

`EditorBuildSettings.asset` был перезаписан Unity без сцены — это ломало бы `BuildScript.cs`
и старт билда. Запись восстановлена вручную в формате Unity 2022 (`!u!1045`, `serializedVersion: 2`).
Файлы остаются **untracked** — коммит не выполнялся (STEP 3 §1 запрещает commit/push).

`Packages/packages-lock.json` сначала был сгенерирован до module fix, но к моменту второго
лога Unity обновил его: записи `com.unity.modules.particlesystem`, `com.unity.modules.physics`
и `com.unity.modules.screencapture` присутствуют. Вручную lock не редактировался.

## 3. Проверка Unity installation (§3)

| Проверка | Результат |
|---|---|
| `which unity/unity-editor/Unity.exe` | Не найдено. |
| Стандартные пути (`/opt/Unity*`, `/usr/local/Unity*`, `/Applications/Unity*`, `C:\Program Files\Unity*`) | Не найдены. |
| Env-переменные Unity | Не заданы. |
| Пакеты (`dpkg -l \| grep -i unity`) | Нет. |
| Docker | Daemon есть, но **образов нет**, license secrets нет. |
| `pwsh` | Не установлен. |

**Итог для sandbox:** Unity по-прежнему недоступен напрямую, но **пользователь запустил
Unity вне sandbox** — это подтверждено сгенерированными `ProjectSettings/*.asset` с точной
версией `2022.3.61f1 (6c53ebaf375d)` и реальными CS1069-ошибками в `prompt.txt`.
Другие версии не использовались. Windows Build Support и лицензия по-прежнему не подтверждены.

### Самый короткий надёжный путь (вручную)

**Вариант A — машина пользователя (Unity уже открыт):**

1. Дождаться реимпорта изменённого `Packages/manifest.json`; Unity обновит `packages-lock.json`.
2. Перекомпилировать проект и прислать новый Console/Editor log — ожидаемо 13 `CS1069` исчезнут.
3. Убедиться, что в `File → Build Settings` сцена `Assets/Scenes/Game.unity` снова одна и включена
   (мы восстановили её в `EditorBuildSettings.asset`, но стоит подтвердить в UI).
4. Продолжить по разделам 4–7 этого отчёта.
5. Для Windows build: подтвердить установленный модуль **Windows Build Support (IL2CPP/Mono)**
   и активную лицензию.

**Вариант B — CI (уже настроен в `.github/workflows/build.yml`):**

- fork/пуш в GitHub + `secrets.UNITY_LICENSE`, `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
- workflow сам прогоняет `unity-test-runner@v4` и `unity-builder@v4` с `unityVersion: 2022.3.61f1`.
- В текущей среде секретов нет — запустить нельзя.

## 4. Импорт и компиляция в Unity (§4)

### Реальный результат: компиляция запускалась пользователем и **упала**

В `prompt.txt` предоставлены 13 ошибок Unity C# компилятора (`CS1069`) из
`Assets/LittleGarage/`:

| Тип | Файлы | Forwarded assembly | Требуемый built-in package |
|---|---|---|---|
| `ParticleSystem` | `Effects/EffectFx.cs` (5), `GameManager.cs` (2) | `UnityEngine.ParticleSystemModule` | `com.unity.modules.particlesystem` |
| `RaycastHit` | `GameManager.cs` (3) | `UnityEngine.PhysicsModule` | `com.unity.modules.physics` |
| `MeshCollider` | `Vehicles/VehicleInstance.cs` (3) | `UnityEngine.PhysicsModule` | `com.unity.modules.physics` |

**Root cause:** `Packages/manifest.json` не содержал эти built-in module packages.
asmdef `LittleGarage.Runtime` не использует `overrideReferences`, поэтому модули должны
быть включены именно через manifest. Это была ошибка STEP 2 scaffolding, а не логики кода.

**Fix (применён):** в `Packages/manifest.json` добавлены:

```json
"com.unity.modules.particlesystem": "1.0.0",
"com.unity.modules.physics": "1.0.0",
"com.unity.modules.screencapture": "1.0.0"
```

`com.unity.modules.screencapture` добавлен проактивно: `ScreenCapture.CaptureScreenshot`
используется в `Tests/PlayMode/SceneSmokeTests.cs` и мог бы дать такую же CS1069 при
компиляции test assemblies. Остальные используемые Unity API покрыты уже включёнными
пакетами (`com.unity.ugui`, `com.unity.modules.ui`, `audio`, `jsonserialize`) или core module:
полный scan типов в `Assets/LittleGarage/**/*.cs` выполнен, дополнительных пропусков нет.

**Статус fix раунда 1:** применён в коде, JSON проверен как валидный. Повторная компиляция
пользователем дала новый лог (раунд 2 ниже).

### Раунд 2 — второй лог пользователя (после module fix)

В обновлённом `prompt.txt` — 2 ошибки `CS0117` и 3 warning:

| Уровень | Место | Причина | Исправление |
|---|---|---|---|
| error `CS0117` | `GameManager.cs:747`, `748` | код писал `ParticleSystem.ParticleSystemStopBehavior.StopEmitting`, но в UnityEngine перечисление **`UnityEngine.ParticleSystemStopBehavior`**, а не вложенный тип `ParticleSystem` | заменено на `ParticleSystemStopBehavior.StopEmitting` |
| warning `CS0219` | `GameManager.cs:348` | `ToolDef unused = null;` — локальная переменная не читалась | удалена, оставлен комментарий о будущем расширении |
| warning `CS0414` | `UIManager.cs:105` | поле `_currentScreen` только присваивалось | добавлен `public ScreenId CurrentScreen => _currentScreen;` (полезно для QA/тестов) |
| warning `CS0414` | `RepairSystem.cs:90` | поле `_stageT` только обнулялось | поле и присваивание удалены |

**Почему offline-проверка раньше не поймала CS0117:** в `/tmp/opencode/compilecheck/ExtraStubs.cs`
перечисление было ошибочно объявлено **внутри** `ParticleSystem`. Stub приведён к реальному
Unity API (namespace-level enum); после этого offline runtime build и tests build снова
дают `0 Warning(s), 0 Error(s)`, behavior harness — `ALL OFFLINE CHECKS PASSED`.

**Статус:** оба раунда ошибок исправлены в коде. **Успешная компиляция в Unity всё ещё не
подтверждена** — нужен третий лог/Console без ошибок.

### Статические предпроверки (не заменяют Unity)

- Все `.cs` в `Assets/LittleGarage/` имеют `.meta`; нет дублирующихся GUID.
- `Bootstrap.cs.meta` GUID = сценный `m_Script` GUID = `9e3b1c5f4a8d2c7b6e5f4a3b2c1d0e9f`.
- `Game.unity.meta` GUID = `EditorBuildSettings.asset` GUID = `bbbb…`; сцена одна и первая.
- `ProjectVersion.txt` и все build-скрипты указывают `2022.3.61f1`.
- Shader import и Runtime Initialize Order по-прежнему не подтверждены.

## 5. Editor tests (§5)

### Раунд 3 — запуск CI test-runner (после исправлений компиляции)

Третий лог в `prompt.txt` — уже не ошибки C#, а падение самого шага
`game-ci/unity-test-runner@v4`:

```
game-ci test ... --no-coverageEnabled --coverageOptions=... --artifactsPath=build/verification ...
[WARN] Unknown argument: noCoverageEnabled
Error: Test run failed with exit code 1
```

**Root cause:** `unity-test-runner@v4` превращает входной параметр `coverageEnabled: false` в флаг
`--no-coverageEnabled`, но `game-ci/cli v0.1.77` включает yargs `negation-prefix: false`.
Флаг не распознаётся, strict-парсер выходит с кодом 1 **до запуска Unity** — тесты ещё не
выполнялись, поэтому это не «тесты упали», а ошибка конфигурации CI.

**Fix (применён):** в `.github/workflows/build.yml` входной параметр `coverageEnabled: false` удалён;
вместо него выставлен `GAME_CI_COVERAGE_ENABLED: 'false'` — задокументированный CLI-путь
`GAME_CI_<SCREAMING_SNAKE_CASE>` (проверен по исходникам CLI: `camelCase` даёт
`coverageEnabled`, булево `'false'` разбирается в `false`). Намерение отключить coverage
сохранено, но сломанный флаг больше не передаётся.

**Статус:** исправление в коде (workflow). **Повторный прогон CI не выполнялся** —
результат «тесты прошли» не заявляется.

### Раунды 4–5 — инфраструктура CI (после фикса coverage)

Следующий прогон подтвердил, что `--no-coverageEnabled` больше нет и образ
`unityci/editor:ubuntu-2022.3.61f1-linux-il2cpp-3` успешно скачивается, но лог заканчивается
стеком `game-ci` с `Test run failed with exit code 1` **до вывода лога Unity**. Причина в
самом логе Unity, который в тот раз не был приложен; `Upload test results` также не выполнился,
потому что шаг тестов упал.

**Fix (применён):** `Upload test results` выполняется теперь всегда (`if: always()`,
`if-no-files-found: warn`) — `playmode.log` / `editmode.log` и XML-результаты сохраняются и при
падении тестов.

Следующий прогон упал ещё раньше:

```
Error: Failed to resolve the latest game-ci CLI release: GitHub API returned 403.
```

**Root cause:** `cliVersion: latest` (значение по умолчанию action) заставляет
`unity-test-runner@v4` запрашивать GitHub Releases API; на shared-раннерах лимит API часто
отдаёт 403. До Unity дело снова не дошло.

**Fix (применён):** в workflow явно зафиксирован `cliVersion: v0.1.77` — та же версия, что уже
кэшировалась в предыдущем прогоне. По исходникам action, при не-`latest` версии API-запрос
`resolveLatestTag()` не выполняется вообще.

**Статус:** оба исправления — только код workflow. **Повторный прогон CI не выполнялся**;
успех тестов не заявляется.

### Раунд 6 — снова `Test run failed with exit code 1` без лога Unity

После фикса `cliVersion` CLI снова кэшируется, образ скачивается, но в предоставленном
фрагменте снова только `[ERROR] Error: Unable to find image ... locally` (это **хвост**
сообщения об ошибке `docker run`, а не причина) и стек `System.run` → `reject(...)`.
Разбор собранного бинаря `game-ci v0.1.77` подтверждает: строка `game-ci:14465` — это
`reject(Object.assign(new Error(errorMessage), ...))` в `System.run`, т.е. завершился
ненулевым кодом `docker run`. Настоящая причина печатается **до** этой строки в stdout
контейнера (лог Unity), которого во фрагменте нет.

**Fix (применён):** добавлен шаг `Summarize Unity test failure` (`if: failure()`), который
в конце лога шага выводит последние 100 релевантных строк из
`build/verification/*.log` (`Aborting batchmode`, `error CS…`, `Compilation failed`,
`license`, `Exception`), либо явно сообщает, что логов нет — контейнер упал до старта Unity.

**Статус:** изменение workflow. Причина падения Unity **по-прежнему не установлена**;
успех тестов не заявляется.

**Не запускались.** Ожидаемые тесты обнаружены в коде (но не в Unity Runner):

| Тест | Покрытие (по коду) | Статус в Unity |
|---|---|---|
| `WashCycleTests.FirstJob_HasCompleteSixStageWashCycle` | 6 этапов, sequential | Не запущен (есть только в исходниках) |
| `WashCycleTests.VehicleFactory_CreatesSedanWithFullUvCoverage` | 8+ colliders, UV 256/256 | Не запущен |
| `WashCycleTests.FullWashCycle_TransitionsSurfaceAndCompletesOncePerStage` | реальные переходы + Completed | Не запущен |
| `WashCycleTests.WashingSystem_DoesNotAllowSkippingAhead` | tool lock | Не запущен |
| `WashCycleTests.AutoFinish_ForcesCleanDryPolishedSurface` | auto-finish | Не запущен |
| `GameFlowTests.*` / `SceneSmokeTests.*` | catalog/save/part/scene boot | Не запущены |

Различение по §5: **discovered** — нет (Unity runner не запускался); **executed** — 0;
**passed** — 0; **failed** — 0; **not executed** — все перечисленные выше.
Оффлайн-harness STEP 2 остаётся отдельным доказательством и **не подменяет** Unity tests.

## 6. Play Mode (§6)

**Не тестировался.** Автоматические скриншоты невозможны без Editor; вместо выдуманных
результатов подготовлен [PLAYMODE_VERIFICATION_CHECKLIST.md](Documentation/Rebuild/PLAYMODE_VERIFICATION_CHECKLIST.md)
с точными действиями и 6 обязательными скриншотами:

1. Initial garage (Home screen)
2. Vehicle before washing
3. Foam stage
4. Final clean vehicle
5. Celebration
6. Result screen

Пока чеклист не пройден человеком, визуальное качество, camera framing, ввод, stage transitions
и 7+ секунд финала остаются **не проверенными**.

## 7. Windows build (§7)

**Не выполнялся.** EXE, data-папка и timestamped build log не созданы. `build-windows.ps1`
и `BuildScript.cs` готовы и проверены статически (VERSION pin `2022.3.61f1`, strict version check,
scene/GUID validation, file-only EXE check), но **Unity не производил сборку** —
результат «build succeeded» не заявляется.

## 8. Статус по 5 категориям

| Категория | Статус |
|---|---|
| 1. Реализовано в коде | ✅ STEP 2 slice + build/CI + тесты + чеклист |
| 2. Проверено offline | ✅ STEP 2 compile/behavior/tests compile; STEP 3 static GUID/meta/version/path checks |
| 3. Проверено в Unity Editor | ⚠️ Частично: Unity пользователем создал ProjectSettings; 2 раунда ошибок (13 `CS1069` + 2 `CS0117`) и 3 warning исправлены (§2, §4); чистая компиляция/tests/PlayMode — ❌ не подтверждены |
| 4. Проверено в Windows build | ❌ Нет (EXE не собран) |
| 5. Всё ещё не проверено | повторный import/compile после fix, Editor tests, Play Mode, 6 скриншотов, Windows EXE, Android, визуал/перф |

> STEP 4 update: the historical STEP 3 rows above predate the final UI commit. For commit
> `85282fb` the CI run above confirms Unity import/compile and 25/25 Editor/PlayMode tests,
> plus successful Windows and Android builds and a full Windows release zip. Game View captures,
> physical devices and visual/performance inspection remain unverified (see §§10–13).

## 9. Точные оставшиеся блокеры

1. Повторная компиляция в Unity после исправления manifest — не выполнялась (Unity есть только у пользователя, не в sandbox).
2. Windows Build Support module и license не подтверждены.
3. Нет повторного прогона CI после исправления флага coverage (§5, раунд 3); Unity Test Runner / Play Mode / screenshot pipeline не запускались.
4. Нет Windows build artifacts.
5. Референс-скриншоты Play Store не анализировались визуально; procedural art/audio — placeholders.

> STEP 4 update: blockers 1–4 were resolved for the final UI commit by CI run 45432416224
> (Unity 2022.3.61f1 import/compile/tests, Android + Windows builds, release assets). Blocker 5
> and all Game View/device verification remain open because no Unity Editor, screenshots or
> physical devices were available in this sandbox.

## 10. Рекомендуемый следующий шаг

1. **Сейчас (у пользователя, Unity уже запущен):** перекомпилировать после правок раунда 2
   (`ParticleSystemStopBehavior` и warnings) → прислать новый Editor log. Ожидаемо: ошибок и
   warning из `prompt.txt` больше нет. Если появятся новые — прислать их.
2. Затем: подтвердить в `File → Build Settings`, что `Assets/Scenes/Game.unity` одна и включена
   (мы восстановили запись в `EditorBuildSettings.asset`) → записать Editor log → прогнать
   Editor tests → пройти Play Mode чеклист и сделать 6 скриншотов → подтвердить модуль
   Windows Build Support → запустить `build-windows.ps1` → заполнить фактические результаты
   категорий 3/4 в `README.md` и этом отчёте.

Шаг 2 не может быть честно выполнен в текущем sandbox.

---

## 11. STEP 4 — COMPLETE UI/UX REPAIR, SCREEN FLOW AND RESPONSIVE LAYOUT (финальный отчёт по §14 `prompt.txt`)

Дата: 2026-11-24. Правки находятся в `/tmp/opencode/wash-cars-ui`; код, тесты и CI в
рамках этой документальной правки не изменялись. Разделы 1–10 описывают STEP 3 и предыдущие
прогоны CI; их
факты о Unity/артефактах **не применяются** к новому UI-коммиту. Ниже — честный статус именно
STEP 4.

### 1. Root causes discovered

1. **Разрозненное управление видимостью.** `UIManager.SetScreen()` переключал только
   четыре primary-экрана, а `ShowPause`, `ShowCustomization`, `ShowComplete`, `HideTools`,
   `OpenSettings` и кнопки вызывали `SetActive()` напрямую. Параллельно состояние хранилось
   в `GameManager._screen` и `UIManager._currentScreen`, то есть существовали два
   расходящихся состояния экрана.
2. **Home → garage.** Кнопка `PlayButton` и показывала garage через `ShowGarage(_progress)`,
   и одновременно поднимала `GarageRequested`; при переходе через `GameManager` экран мог
   остаться в несогласованном состоянии.
3. **Ранние возвраты.** `StartJob()` выставлял `_screen = ScreenId.Play` до проверки job/vehicle,
   а `BeginTask()` при пустых `task.tools` вызывал `OnTaskCompleted()` и возвращался **до**
   `ShowPlay()`. Garage/HUD могла остаться видимой за игровым экраном.
4. **CanvasScaler.** Portrait-референс 1080x1920 использовал `Match Width Or Height` с
   одноразовым `matchWidthOrHeight = 0.2f` в landscape (`MatchForCurrentScreen()`),
   без пересчёта при смене разрешения. Это давало переполнение/обрезание на альбомных
   и нестандартных aspect ratio.
5. **Task selection.** Счётчик задач имел фиксированную высоту и мог обрезаться; у job cards
   не было ограничений ширины внутри `ScrollRect` (отсутствовали согласованные
   `LayoutElement`/`ContentSizeFitter`/insets), поэтому карточки занимали почти всю ширину,
   нижняя часть списка была недостижима, скролл работал ненадёжно.
6. **Gameplay HUD.** Инструкция перекрывала заголовок/сцену, нижняя панель занимала 20% экрана,
   прозрачные панели HUD имели `raycastTarget = true` и перехватывали клики/тач по машине.
7. **Пауза.** `SetPaused()` не менял `Time.timeScale`; игровое время продолжало идти.
8. **Complete/finale.** `GameManager` скрывал игровые контролы внешним `HideTools()`, но
   логический `ScreenId.Complete` и визуальный primary-экран расходились: результат должен
   показываться только после 7+ секунд финала, а состояние экрана уже было другим.
9. **Модальные слои.** Pause, Customization и Settings включались независимо и могли
   сосуществовать; у results/pause/settings/customization не было надёжных input-блокеров.
10. **Tool buttons.** Каждый `ShowPlay()` удалял и пересоздавал кнопки через `Destroy()`,
    отложенный до конца кадра, поэтому в течение кадра возникали временные дубликаты.
11. **Ввод/EventSystem.** Отсутствовала проверка уже существующего `StandaloneInputModule`;
    повторная инициализация могла создать дубликат.

### 2. Screens and components modified

| Экран / компонент | Что изменено |
|---|---|
| Main menu / Home (`UIManager.BuildHomeScreen`) | scrollable контент, best-fit заголовок/подзаголовок, progress card, компактные кнопки, `GarageRequested` вместо прямого показа garage |
| Task selection / Garage | header с back/title/counter, constrained content insets, `ScrollRect` + `RectMask2D` + `VerticalLayoutGroup` + `ContentSizeFitter`, перерабатываемые job cards |
| Gameplay HUD | отдельные `_topBar`, `_instructionPanel`, `_hintPanel`, `_toolBar`, best-fit текст, `raycastTarget = false` для декора |
| Pause | overlay на `Play`, modal blocker, resume/restart/home/sound, `timeScale` 0/1 |
| Result / Complete | модальный фон, карточка с тремя действиями, показывается только после finale |
| Customization sheet | нижний sheet + input blocker + scrollable options rows, минимальная высота касания |
| Settings dialog | `OpenSettings`/`CloseSettings` через `ApplyVisibility`, возврат в предыдущий overlay, modal blocker |
| Tool panel | in-place обновление кнопок, locked/disabled состояния, 96/104 unit touch targets |
| CanvasScaler / safe area | `UiLayoutMetrics`, `match = 0.5`, recompute on resolution change, `SafeRoot` по `Screen.safeArea` |
| `GameManager.cs` | screen orchestration, re-entry guard, `timeScale`, finale `_finalizing` guard, input blocking, `CompleteJobForTests()` seam |
| `Core/GameTypes.cs` | добавлен `UiOverlay { None, Pause, Customization, Settings }` |
| `UI/UiLayoutMetrics.cs` (новый файл) | единый контракт layout-значений и чистые функции для тестов |

### 3. Screen management changes

* Единственный authoritative writer видимости — `UIManager.ApplyVisibility(...)`; все
  primary-screen и overlay переходы идут через него. Primary screens (`Home`, `Garage`,
  `Play`, `Complete`) взаимно исключительны; `Pause`, `Customization`, `Settings` — только
  overlays и никогда не выбирают primary screen. Settings запоминает overlay-возврат
  (`_settingsReturnOverlay`).
* Добавлены introspection-свойства: `ActivePrimaryScreen` (возвращает `null`, если активно
  не ровно один primary), `IsPrimaryScreenActive`, `ActiveOverlay`, `IsOverlayActive`,
  `VisibleJobCardCount`, `ActiveToolButtonCount`, `ScalerMatch`,
  `ScalerReferenceResolution` (плюс прежний `CurrentScreen`).
* `GameManager.Initialize()` имеет re-entry guard против повторных систем/подписок;
  `ShowHome`/`ShowGarage`/`StartJob` восстанавливают `Time.timeScale = 1`, останавливают
  finale и очищают transient-состояние; `SetPaused()` меняет `timeScale` 0/1; `OnDestroy()`
  тоже восстанавливает `timeScale`.
* `StartJob()` переходит в `Play` только после валидных `vehicle`/`level`; `BeginTask()`
  показывает HUD даже для task без инструментов до завершения.
* `CompleteJob()` выставляет логический `Complete`, вызывает `HideTools()` и запускает
  finale; `_finalizing`/`IsFinaleActive` блокируют pause, orbit, zoom и игровой Update.
  `ShowComplete()` вызывается только в конце `FinaleRoutine`; `StopFinale()` чистит
  coroutine/root на home/restart/destroy.
* `EnsureEventSystem()` переиспользует `EventSystem.current` и добавляет
  `StandaloneInputModule` только при отсутствии; Canvas создаётся один раз.

### 4. Responsive layout changes

* `UiLayoutMetrics`: reference 1080x1920, `CanvasScaler.ScaleWithScreenSize`,
  `ScreenMatchMode.MatchWidthOrHeight`, `match = 0.5` для всех orientation; значение
  пересчитывается в `Update()` при изменении `Screen.width/height` (без одноразового
  landscape-значения 0.2).
* Garage content получает разные side insets для portrait (0.06) и landscape (0.14) и
  учитывает высоту header (0.12); header title/counter используют flexible/min widths и
  best-fit вместо фиксированной высоты.
* Home и customization list теперь scrollable (`ScrollRect`, `RectMask2D`, clamped movement,
  layout groups + `ContentSizeFitter`) — нижний контент достижим.
* Job cards ограничены шириной (`LayoutElement.flexibleWidth`, `minWidth = 0` у info),
  текст wrap/best-fit, lock overlay не перехватывает raycast.
* HUD-полосы разделены контрактом: top 0.12, instruction 0.78–0.845, hint 0.20–0.255,
  bottom 0.17; `HudBandsAreSeparated()` проверяет отсутствие пересечений.
* Bottom bar и tool targets компактнее (progress 22, tool min 96 / preferred 104), locked
  инструменты визуально приглушены и `interactable = false`.
* Декоративные image/text HUD имеют `raycastTarget = false`; pause/result/settings/
  customization — modal blockers. Safe-area обработка через `SafeRoot` сохранена.

### 5. Main menu fixes

Title/subtitle и progress card переведены на best-fit + `LayoutElement` внутри scrollable
контента; кнопки ИГРАТЬ / ЗВУК И НАСТРОЙКИ получили согласованные размеры и минимальную
высоту касания; progress slider стал визуальным (`MakeSliderVisualOnly`). Кнопка ИГРАТЬ
больше не показывает garage сама — только поднимает `GarageRequested`, переходом управляет
`GameManager`.

### 6. Task selection fixes

Back/title/counter выровнены в header; счётчик имеет `minWidth = 96`, `preferredWidth = 140`,
`flexibleWidth = 0` и не обрезается. Контент ограничен side insets и обрезается через
`RectMask2D`; список использует `Clamped` vertical scroll, `VerticalLayoutGroup` и
`ContentSizeFitter`. Карточки создаются по требованию (10 placeholders), имеют `minHeight = 128`,
flexible width, wrap/best-fit текст, читаемые unlocked/completed/locked состояния.

### 7. Gameplay HUD fixes

Заголовок/этап и пауза вынесены в компактный top bar; instruction и hint — в отдельные
полосы с зазорами; центральная область остаётся свободной для 3D-машины. Bottom bar
уменьшен до 0.17 экрана, tool buttons обновляются на месте (без transient-дубликатов).
`GameManager.OnPointerDown()` блокирует gameplay-ввод, если `UIManager.IsPointerOverUI()`
попал по UI.

### 8. Pause/result screen fixes

Pause доступен только из `Play`, показывает overlay, не активирует другой primary screen,
останавливает/возвращает `timeScale`, предоставляет resume/restart/home/settings и чистит
эффекты. Result показывается только после сохранённого 7+ секундного finale; во время
finale HUD/tools скрыты, primary остаётся `Play` для 3D-шоукейса, ввод заблокирован
(`_finalizing` + `ScreenId.Complete`). Dialog result — модальный с input-блокером и
навигацией next/restart/home. Повторные home/restart останавливают finale и coroutines.

### 9. Tests added or updated

Добавлено **7 PlayMode `UiFlowTests`** (все прошли в финальном CI-прогоне):

| Тест | Что утверждает |
|---|---|
| `ShowGarage_HidesMainMenu_AndLeavesExactlyOnePrimaryScreen` | после `ShowGarage(null)` активен только `Garage`, `Home` выключен, overlay нет |
| `StartJob_HidesTaskSelection_AndShowsOnlyGameplay` | garage → job: `Play` — единственный primary, `VisibleJobCardCount == 0` |
| `Pause_DoesNotActivateAnotherPrimaryScreen_AndResumeRestoresGameplay` | pause сохраняет `Play` единственным primary, активирует `Pause`, `timeScale == 0`; resume снимает overlay и возвращает `timeScale == 1` |
| `Restart_KeepsOneCanvasOneEventSystem_AndOneToolButtonPerTool` | после повторных `StartJob`: количество Canvas/EventSystem/tool-кнопок неизменно, нет stale `Tool_` объектов |
| `ReturnHome_ClearsGameplayOverlays_AndRestoresMainMenu` | home после pause: только `Home`, overlay нет, tools 0, `timeScale == 1` |
| `Finale_KeepsGameplayVisible_UntilResultIsShown` | сразу после completion логический `Complete` + finale активен, но визуально `Play`, tools 0; после finale — `Complete`, `Play` выключен |
| `AllTransitions_MaintainExactlyOnePrimaryScreen` | Home → Garage → Play → Pause → Resume → Home: ровно один primary на каждом шаге |

Добавлено **5 EditMode `UiLayoutTests`** (только чистые данные контракта, не отрендеренный UI):

| Тест | Что утверждает |
|---|---|
| `LayoutContract_UsesDeliberatePortraitReference` | reference resolution ровно 1080x1920 |
| `LayoutContract_UsesOneDocumentedScalerMatchForEveryAspect` | `ScalerMatch == 0.5` для 16:9, 16:10, 4:3 и 3:4 |
| `LayoutContract_HudBandsAreSeparated` | top/instruction/hint/bottom не пересекаются по anchor-границам |
| `LayoutContract_GarageContentIsNarrowerInLandscape` | landscape side inset больше portrait |
| `LayoutContract_ToolButtonsKeepMinimumTouchTarget` | preferred >= min >= 96 единиц |

Существующие **13 тестов** (`GameFlowTests` 7, `WashCycleTests` 5, `SceneSmokeTests` 1)
не изменялись.

### 10. Actual test results

* **Offline:** C# compile checks с stubbed Unity/NUnit прошли (`0 errors`); это только
  компиляция, **не замена Unity Editor tests**.
* **Unity Editor (real, in CI):** `game-ci/unity-test-runner@v4` запустил настоящие EditMode-
  и PlayMode-наборы на коммите `85282fb` в Unity **2022.3.61f1** (run 45432416224):
  `editmode-results.xml — 17/17 — Passed in 3.243s`,
  `playmode-results.xml — 8/8 — Passed in 13.225s`,
  `Test Results — 25/25 — Passed in 16.467s`. Сюда входят все 12 новых UI-тестов.
  Локально Unity в sandbox недоступен; запуск выполнялся именно в GitHub Actions.
* **CI:** workflow `Build Little Car Workshop (Android + Windows)` завершился `success`
  для точного SHA `85282fb`; job'ы verify, Build Android, Build StandaloneWindows64 и
  Publish in-app update manifest — `success`.
* **Build / published artifact (CI):** скачанный CI-артефакт
  `LittleCarWash-StandaloneWindows64` и опубликованный `LittleCarWash-windows.zip` содержат
  `LittleCarWorkshop.exe`, `UnityPlayer.dll` и `LittleCarWorkshop_Data/` (107 записей).
  `update.json` release v1.1.0 содержит `notes: "fix: address CI failures (iteration 1)"`,
  то есть ассеты пересобраны из этого коммита.
* **Game View / aspect ratios / screenshots:** не выполнялись.
* **Android/Windows physical devices:** не выполнялись.

### 11. Screenshots captured

Ноль. Ни один из 8 требуемых §12 скриншотов (главное меню, выбор задания, старт задания,
этап мойки, другой инструмент, пауза, результат, другой aspect ratio) в этом sandbox не
сделан; CI тоже не снимает Game View. Прохождение тестов и сборка **не являются** визуальным
подтверждением: clipping, overlap, layering и удобство тач-целей остаются **не проверенными**.

### 12. Remaining issues

1. Пройти Play Mode вручную и снять 8 скриншотов минимум при 16:9, 16:10, 4:3 и узком
   landscape; проверить clipping/overlap/layering и тач-цели. Это единственный способ
   подтвердить визуальные улучшения.
2. Проверить safe area и ввод на физическом Android-планшете и Windows.
3. Сохранённое требование к будущим релизам: публиковать **полный** Windows player
   (`LittleCarWorkshop.exe`, `UnityPlayer.dll`, `LittleCarWorkshop_Data/`), а не только `.exe`.
   Для этого коммита требование выполнено и проверено по скачанному CI-артефакту и
   опубликованному zip; известных блокирующих CI-проблем нет.

### 13. Implemented vs verified (5 категорий)

| Категория | Статус STEP 4 |
|---|---|
| Implemented in code | ✅ Экраны, `ApplyVisibility`, `UiOverlay`, `UiLayoutMetrics`, layout, finale guards, 12 новых тестов |
| Verified offline | ✅ Stubbed C# compile; статические проверки исходников не заменяют Unity |
| Verified in Unity Editor | ✅ GitHub Actions, Unity 2022.3.61f1, commit `85282fb`: 25/25 реальных EditMode+PlayMode тестов passed (17 + 8); локально Unity недоступен |
| Verified in Game View at multiple aspect ratios | ❌ Нет (скриншоты/замеры не делались) |
| Verified on physical Android/Windows devices | ❌ Нет |
| Windows build / published artifact | ✅ CI build + publish succeeded; скачаны полный CI-артефакт и release zip с `.exe`, `UnityPlayer.dll`, `LittleCarWorkshop_Data/` |
