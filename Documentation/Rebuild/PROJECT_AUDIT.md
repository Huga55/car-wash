# PROJECT_AUDIT

Дата: 2026-11-24  
Репозиторий: `/e/my/wash-cars`  
Ограничение: Unity Editor не установлен в sandbox; анализ выполнен статически по файлам проекта, коду, manifest, ProjectSettings и git-истории. Визуальные проверки Unity/PlayMode/сборка не запускались.

## 1. Executive summary

Проект находится на Unity **2022.3.61f1** (Built-in/legacy pipeline по признакам manifest: нет URP/HDRP пакетов), с одной сценой `Assets/Scenes/Game.unity` и минимальным набором пакетов. Текущая игра — 2D pseudo-3D car wash с процедурной графикой/звуком, 30 уровнями и 20 типами транспорта, реализованными кодом (`CarArt`). Реально работающие игровые системы сосредоточены вокруг мойки; ремонта, замены деталей, покраски, кастомизации и 3D-гаража в legacy runtime нет.

В репозитории также присутствует параллельный незавершённый прототип `Assets/LittleGarage/*`, который частично соответствует целевой модульной идее, но не интегрирован в подтверждённый рабочий pipeline и имеет незавершённые/непроверенные части. Для целевой игры “Car Wash: Auto Repair Garage”-подобного масштаба legacy-архитектуру нельзя безопасно расширять без крупной переработки.

## 2. Unity / Project settings

| Пункт | Факт | Источник/доказательство | Статус |
|---|---|---|---|
| Unity version | `2022.3.61f1` | `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 2022.3.61f1`) | Подтверждено |
| Render Pipeline | Признаки Built-in: нет `com.unity.render-pipelines.universal`, `com.unity.render-pipelines.high-definition`, нет SRP assets в проекте. | `Packages/manifest.json`, поиск по Assets/ProjectSettings | Подтверждено косвенно |
| .NET / scripting backend / API compat | `ProjectSettings/ProjectSettings.asset` отсутствует; точный scripting backend, API compatibility level, managed stripping level не подтверждены. | `ls ProjectSettings/` показывает только `EditorBuildSettings.asset`, `ProjectVersion.txt` | Не подтверждено |
| Input System | Используется legacy `UnityEngine.Input` + `EventSystem/StandaloneInputModule`. Пакет `com.unity.inputsystem` отсутствует. | `Assets/Scripts/WashingController.cs`, `Assets/Scripts/Bootstrap.cs`, `Packages/manifest.json` | Подтверждено |
| UI system | UGUI (`com.unity.ugui`), UI создаётся runtime-кодом. | `Packages/manifest.json`, `Assets/Scripts/GameController.cs`, `UiFactory.cs` | Подтверждено |
| Scenes in Build Settings | Только `Assets/Scenes/Game.unity`, enabled. | `ProjectSettings/EditorBuildSettings.asset` | Подтверждено |
| Main scene content | Минимальная сцена; Bootstrap присутствует как объект/компонент, runtime создаёт камеру/EventSystem/GameController. | `Assets/Scenes/Game.unity`, `Assets/Scripts/Bootstrap.cs` | Подтверждено |
| Packages | `test-framework`, `ugui`, modules: ui,imgui,imageconversion,audio,unitywebrequest,jsonserialize,androidjni,uielements. | `Packages/manifest.json` | Подтверждено |
| ProjectSettings.asset | Отсутствует. | `ls ProjectSettings/` | Подтверждено (риск) |
| Git состояние на аудит | Изменены `Assets/Editor/BuildScript.cs`, `Assets/Scenes/Game.unity`, `Packages/manifest.json`, `README.md`, `REPORT.md`; untracked `ARCHITECTURE.md`, `Assets/LittleGarage*`, `BUILD_WINDOWS.md`, `build-windows.ps1`, `docs/`, `prompt.txt`, `Documentation/Rebuild/`. | `git status --short` | Подтверждено |

## 3. Структура папок

| Путь | Назначение | Комментарий |
|---|---|---|
| `Assets/Scenes/Game.unity` | Единственная игровая сцена. | Legacy flow + сейчас также ссылается на Bootstrap. |
| `Assets/Scripts/` | Legacy runtime. | 15 C# файлов, ~4724 строк; основной монолит `GameController.cs` (1528 строк). |
| `Assets/Editor/` | Build/verification. | `BuildScript.cs`, `VisualVerification.cs`; verification жёстко завязан на legacy flow. |
| `Assets/LittleGarage/` | Новый параллельный прототип. | 29 C# файлов, ~8404 строк; есть Core/Systems/Vehicles/Garage/UI/Audio/Tests, но интеграция/готовность не подтверждены Unity-запуском. |
| `Packages/` | Unity manifest/lock. | Минимальный набор зависимостей. |
| `ProjectSettings/` | Частично. | Нет `ProjectSettings.asset`. |
| `.github/workflows/build.yml` | CI build/verify/release. | GameCI, Unity 2022.3.61f1, verify + Windows/Android. |
| `docs/` | Ранее созданные рабочие заметки. | Не часть требуемого `Documentation/Rebuild/`, но полезны. |

## 4. Архитектура legacy runtime

### 4.1 Основные компоненты

| Компонент | Файл | Ответственность | Оценка |
|---|---|---|---|
| Bootstrap | `Assets/Scripts/Bootstrap.cs` | FPS/sleep/orientation, камера, EventSystem, AudioManager init, UpdateChecker, создание GameController. | Малый, понятный, но legacy-specific. |
| GameController | `Assets/Scripts/GameController.cs` | Экраны, UI, flow уровней, wash/reward/pause/audio settings/update dialog, эффекты, orchestration. | Монолит, высокий риск изменений. |
| WashingController | `Assets/Scripts/WashingController.cs` | Input mouse/touch, UV mapping в wash rect, радиус кисти, stroke interpolation, progress/complete API. | Переиспользуема идея, но 2D-UV bound. |
| WashSurface | `Assets/Scripts/WashSurface.cs` | 256x256 overlay, маски wet/foam/clean/scrub/dry, 6 зон, dirty rect upload, completion/final shine. | Сильная алгоритмическая база для wash. |
| CarArt | `Assets/Scripts/CarArt.cs` | Процедурная отрисовка 20 CarKind в Texture2D + panel mask. | 2D placeholder, не 3D asset. |
| CarViewController | `Assets/Scripts/CarViewController.cs` | Позиционирование/idle/wash pulse/drive away 2D RectTransform. | 2D-only. |
| GameArt | `Assets/Scripts/GameArt.cs` | Процедурный фон автомойки. | 2D-only. |
| AnimalActor | `Assets/Scripts/AnimalActor.cs` | Driver/reward character animation. | Не требуется целевым брифом. |
| AudioManager/AudioHelper | `Assets/Scripts/AudioHelper.cs` | Процедурная музыка/SFX, 5 источников, bus gains, settings. | Полезная база. |
| UiFactory/UiLayout | `Assets/Scripts/UiFactory.cs`, `UiLayout.cs` | Runtime UI примитивы, safe area. | Прототипно полезно. |
| ProgressManager | `Assets/Scripts/ProgressManager.cs` | Unlock/completed/stars в PlayerPrefs. | Простая, но ограниченная модель. |
| Localization/GameData | `Assets/Scripts/Localization.cs`, `GameData.cs` | Русские строки, 30 уровней, типы машин/грязи/животных. | Переиспользуемы как data/reference. |
| UpdateService/UpdateChecker/AppInfo | `Assets/Scripts/UpdateService.cs`, `UpdateChecker.cs`, `AppInfo.cs` | Update check/download/install flow. | Отдельная подсистема, не core gameplay. |

### 4.2 Сильные стороны

- Рабочая базовая wash-механика с масками и зональным завершением.
- Нет внешних binary assets-зависимостей для простого запуска прототипа.
- Есть CI/build scripts и Editor verification.
- Есть русская локализация и базовые настройки звука/сохранения.

### 4.3 Архитектурные ограничения

- Монолитный `GameController` совмещает UI, flow, rewards, updates, settings, effects.
- Жёсткая привязка к 2D: `RectTransform`, `Texture2D`, overlay sprites.
- Нет entity/component модели для реальных автомобильных деталей.
- Нет data-driven job/task/vehicle definition под repair/paint/customization.
- Нет 3D camera/physics raycast interaction pipeline.
- Verification и build release flow завязаны на legacy имена/сценарии.

## 5. Состояние LittleGarage прототипа

| Область | Наличие | Наблюдение |
|---|---|---|
| Core defs/save/types | Есть | `GameTypes`, `Definitions`, `ProgressData`, `SaveSystem`, `VehicleAppearance`. |
| Bootstrap/GameManager | Есть | Runtime composition; интеграция с системами частичная/требует Unity проверки. |
| Wash/Input/Tools/Surface | Есть | `InputRouter`, `WashingSystem`, `ToolController`, `SurfaceStateSystem`. |
| Repair/Customization | Есть файлы | `RepairSystem.cs`, `CustomizationSystem.cs` присутствуют; полнота/качество требует Unity runtime проверки. |
| Vehicles/Garage/Effects | Есть | Процедурные 3D mesh factory, garage, camera, FX. |
| UI/Audio | Есть | Runtime UIManager, AudioManager. |
| Tests | Есть | EditMode/PlayMode asmdefs и тесты. |
| Интеграция в Build Settings/CI verification | Не подтверждена | Legacy CI verification вызывает `LittleCarWashBuild.VisualVerification.Run` — старый flow. |

## 6. Build / CI / Windows

| Пункт | Факт |
|---|---|
| Editor build script | `Assets/Editor/BuildScript.cs` содержит Windows/Android build methods, логи, проверку сцен, проверку поддержки модуля. |
| Windows output target | `Builds/Windows/LittleCarWorkshop.exe` (в текущем BuildScript). |
| Batchmode entry | `-executeMethod BuildScript.BuildWindows` (также есть legacy namespace wrappers в коде/CI). |
| GitHub Actions | `.github/workflows/build.yml`: verify -> build Windows+Android -> release on master. |
| CI verify | Использует GameCI Linux builder + `LittleCarWashBuild.VisualVerification.Run`; это legacy verification. |
| Windows Build Support | Проверяется в `build-windows.ps1` и BuildScript сообщает об отсутствии модуля. |
| Android SDK/NDK для Windows build | Не нужны для Windows standalone; нужны только для Android target. |

## 7. Технические риски

1. **Отсутствует `ProjectSettings.asset`** — риск неполного воспроизведения настроек проекта.
2. **Два параллельных runtime** (`Assets/Scripts` и `Assets/LittleGarage`) — риск конфликтов, дублирования и неверного entry point.
3. **Legacy CI verification несовместим с новой игрой** — будет блокировать сборку после переработки, если не обновить.
4. **Нет реальных 3D ассетов/текстур/FBX/prefab** — весь контент процедурный; визуальное качество и производительность требуют проверки в Unity.
5. **Нет Unity Editor в sandbox** — все runtime выводы частично статические; перед разработкой/релизом нужен реальный Unity run.
6. **Update/release pipeline привязан к старым именам** (`LittleCarWash`, AppInfo, update.json) — может конфликтовать с переименованием в Little Car Workshop.

## 8. Рекомендация

Для целевого брифа не продолжать лепить 2D legacy. Использовать `LittleGarage`/новую модульную 3D архитектуру как основу вертикального среза, но сначала стабилизировать: единый entry point, единый каталог данных, реальные Unity smoke tests, обновлённый build/verification, и только затем расширять контент. Legacy wash mask/audio/save идеи переносить точечно.
