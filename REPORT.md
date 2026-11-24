# Отчёт STEP 3: Unity recovery, validation & Windows build

Дата: 2026-11-24  
Unity target: **2022.3.61f1**, Built-in Render Pipeline  
Рабочая директория: `/e/my/wash-cars`

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

## 9. Точные оставшиеся блокеры

1. Повторная компиляция в Unity после исправления manifest — не выполнялась (Unity есть только у пользователя, не в sandbox).
2. Windows Build Support module и license не подтверждены.
3. Нет повторного прогона CI после исправления флага coverage (§5, раунд 3); Unity Test Runner / Play Mode / screenshot pipeline не запускались.
4. Нет Windows build artifacts.
5. Референс-скриншоты Play Store не анализировались визуально; procedural art/audio — placeholders.

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
