# Аудит проекта: `/e/my/wash-cars`

Дата аудита: 2026-11-24  
Аудитор: автоматический code/doc audit по локальному репозиторию.

## 1. Область аудита и ограничения

### Проверено

- Корень проекта: `prompt.txt`, `README.md`, `ARCHITECTURE.md`, `REPORT.md`, `.gitignore`, `.github/workflows/build.yml`.
- Unity-проект: `Assets/Scenes/Game.unity`, `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`.
- Скрипты: все файлы `Assets/Scripts/*.cs`, `Assets/Editor/*.cs`.
- Параллельный прототип: `Assets/LittleGarage/**` (Bootstrap, GameManager, Core, Systems, Audio, UI, Vehicles, Shader, Tests, asmdef meta).
- Наличие/отсутствие ассетов: поиск по `fbx/obj/png/jpg/wav/mp3/ogg/prefab/mat/controller/asset` и общий обход `Assets`.
- Git состояние: `git status --short`, последние коммиты.

### Ограничения

- `ProjectSettings/ProjectSettings.asset` отсутствует в репозитории. Build/player/input/orientation/bundle settings нельзя полноценно подтвердить из ProjectSettings; частично подтверждаются из кода/README/REPORT/workflow.
- Визуальные Google Play скриншоты не анализировались: модель не поддерживает image input. Это ограничение касается референса, но не кода.
- Аудит документационный/статический; Unity Editor/PlayMode/сборка в этой сессии не запускались.

## 2. Краткий вердикт

| Область | Статус | Комментарий |
|---|---|---|
| Текущая игра | Рабочий прототип | Legacy 2D pseudo-3D car wash, процедурная графика/звук, один экранный flow. |
| Соответствие новому брифу | Не соответствует | Бриф требует 3D low-poly garage, 3D cars, repair/paint/customization, модульную runtime-систему. |
| 3D-ассеты/текстуры/аудиофайлы | Отсутствуют | В репозитории нет моделей/текстур/аудио/prefab-контента; visuals/audio генерируются кодом. |
| Архитектура | Монолит + зародыш новой | `GameController.cs` — 1528 строк, orchestration + UI + flow + effects. `Assets/LittleGarage` содержит зачатки целевой архитектуры, но неполные и не интегрированы. |
| Переиспользование | Ограниченное | Можно переносить идеи/алгоритмы: mask wash, procedural audio, save/settings, localization, UI helpers, CI patterns. Визуальные 2D car sprites/2D scene не подходят как целевые 3D ассеты. |

## 3. Структура репозитория (фактическая)

| Путь | Назначение | Статус |
|---|---|---|
| `Assets/Scenes/Game.unity` | Единственная сцена. Содержит Bootstrap/GameObject; build settings включает только её. | `[ФАКТ]` |
| `Assets/Scripts/*` | Legacy runtime: Bootstrap, GameController, wash/input, art, audio, UI, progress, localization, update. | `[ФАКТ]` |
| `Assets/Editor/*` | `BuildScript.cs`, `VisualVerification.cs`. | `[ФАКТ]` |
| `Assets/LittleGarage/*` | Новый/параллельный прототип целевой архитектуры: Core, Systems, Audio, UI, Vehicles, Shader, Tests. | `[ФАКТ, НЕ ИНТЕГРИРОВАНО]` |
| `Packages/manifest.json` | Минимальный набор пакетов: UGUI + несколько встроенных модулей. | `[ФАКТ]` |
| `ProjectSettings/ProjectVersion.txt` | Unity 2022.3.61f1. | `[ФАКТ]` |
| `ProjectSettings/EditorBuildSettings.asset` | Включена только `Assets/Scenes/Game.unity`. | `[ФАКТ]` |
| `ProjectSettings/ProjectSettings.asset` | Отсутствует. | `[ФАКТ]` |
| `.github/workflows/build.yml` | Verify + Windows build + Android build + release. | `[ФАКТ]` |

Git состояние на момент аудита: изменены `Assets/Scenes/Game.unity`, `Packages/manifest.json`; неотслеживаемые `ARCHITECTURE.md`, `Assets/LittleGarage.meta`, `Assets/LittleGarage/`, `prompt.txt`. Это существующее рабочее состояние репозитория; аудит его не менял.

## 4. Unity / сцена / пакеты / сборка

### 4.1 Версия и сцена

| Проверка | Результат | Доказательство |
|---|---|---|
| Unity version | 2022.3.61f1 | `ProjectSettings/ProjectVersion.txt` |
| Единственная игровая сцена | `Assets/Scenes/Game.unity` | `EditorBuildSettings.asset`, `BuildScript.cs` |
| Runtime composition | Сцена минимальная; Bootstrap создаёт камеру/EventSystem/AudioListener/GameController или LittleGarage manager. | `Assets/Scripts/Bootstrap.cs`, `Assets/LittleGarage/Bootstrap.cs`, `Game.unity` |
| Render pipeline packages | Нет URP/HDRP/Input System/TextMeshPro/Cinemachine/Addressables в manifest. | `Packages/manifest.json` |

### 4.2 Packages/manifest

`Packages/manifest.json` содержит только:

- `com.unity.ugui`
- `com.unity.modules.ui`
- `com.unity.modules.imgui`
- `com.unity.modules.imageconversion`
- `com.unity.modules.audio`
- `com.unity.modules.unitywebrequest`
- `com.unity.modules.jsonserialize`
- `com.unity.modules.androidjni`
- `com.unity.modules.uielements`

Вывод: проект сейчас рассчитан на простой built-in/UGUI стек. Для 3D low-poly garage по брифу потребуется осознанное решение по render pipeline, input, UI, asset pipeline и tooling; текущий manifest это не покрывает. `[ВЫВОД]`

### 4.3 ProjectSettings

| Настройка | Статус |
|---|---|
| `ProjectSettings.asset` | Отсутствует. Нельзя подтвердить product name, bundle id, orientation, scripting backend, color space, active input handler, quality, graphics API из файла. |
| Ориентация | Код `Assets/Scripts/Bootstrap.cs` принудительно ставит `ScreenOrientation.LandscapeLeft` на Android не в Editor. README/REPORT ранее описывали portrait для старого дизайна — есть расхождение документации и кода. |
| Build target в CI | Windows verify использует `StandaloneLinux64` GameCI builder; build job — `StandaloneWindows64` и `Android`. |

## 5. Аудит runtime-кода (`Assets/Scripts`)

### 5.1 Карта файлов

| Файл | Строк | Роль | Оценка |
|---:|---:|---|---|
| `GameController.cs` | 1528 | Монолитный оркестратор: UI build, screens, level flow, wash/reward/pause/update/settings, effects. | Переписать/разделить. |
| `WashSurface.cs` | 520 | Процедурная маска грязи/пены/мокроты, 256×256, 6 зон, stroke interpolation, completion. | Идея переиспользуема, реализация 2D-специфична. |
| `CarArt.cs` | 675 | Процедурная отрисовка 20 видов транспорта в Texture2D. | Не 3D ассет. Переиспользовать только как временный placeholder/справочник форм. |
| `AudioHelper.cs` | 523 | Процедурный audio runtime, 5 buses/источников, loop/SFX/music, settings. | Сильный кандидат на переиспользование идеи/кода после адаптации. |
| `UiFactory.cs` | 445 | Runtime UI primitives, шрифт fallback, sprites, tool icons, buttons/labels. | Частично переиспользуемо для прототипа; для целевого UI лучше заменить на структурированный UI слой. |
| `WashingController.cs` | 246 | Input sampling, UV mapping, brush radius, stroke events, completion API. | Логику input/stroke можно адаптировать, но сейчас завязана на 2D wash rect. |
| `Localization.cs` | 232 | Русские строки, stage/tool labels, настройки. | Переиспользуемо. |
| `GameData.cs` | 194 | 30 уровней, CarKind/DirtKind/AnimalKind, stage progression, PlayerPrefs keys. | Переиспользуемо как data seed; несовместимо с 3D JobDef/VehicleDef схемой напрямую. |
| `UpdateService.cs` | 203 | Проверка `update.json`, скачивание APK, Windows installer flow. | Отдельная подсистема; переиспользуема осторожно, но не core gameplay. |
| `CarViewController.cs` | 129 | Управление 2D car/overlay RectTransform, idle/wash pulse/drive away. | 2D-only. |
| `GameArt.cs` | 115 | Процедурный фон wash bay. | 2D-only placeholder. |
| `AnimalActor.cs` | 92 | Reward driver animation. | Не относится к новому 3D garage брифу; опционально выбросить. |
| `ProgressManager.cs` | 70 | PlayerPrefs unlock/completed/stars. | Простая save-модель, можно расширить/заменить. |
| `Bootstrap.cs` | 67 | Entry point, fps/sleep/orientation/audio init/spawn GameController. | Переиспользуем как bootstrap pattern, но переписать под новый runtime. |
| `UiLayout.cs` | 47 | Safe-area/canvas fit helper. | Переиспользуемо. |
| `UpdateChecker.cs` | 41 | Coroutine host для update checks/install. | Вспомогательное. |
| `AppInfo.cs` | 16 | Версия/URL manifest/releases. | Переиспользуемо. |

### 5.2 Ключевые наблюдения по legacy runtime

1. **Монолитный GameController.** UI, состояние экранов, обработка input, reward sequence, audio settings, update UI, effects — в одном классе (`Assets/Scripts/GameController.cs`, 1528 строк). Это основной технический долг.
2. **2D pseudo-3D.** Машина — `Image` + `Texture2D` (`CarViewController`, `CarArt`), грязь — overlay texture/mask (`WashSurface`), фон — baked texture (`GameArt`). Нет 3D meshes/materials/colliders.
3. **Одна механика.** Реально реализована мойка со стадиями Wet/Foam/Scrub/Rinse/Dry. Repair/paint/customization отсутствуют.
4. **Input старый.** `WashingController` использует `Input.mousePosition`/`Input.GetTouch` и `EventSystem.current.IsPointerOverGameObject`; New Input System не используется.
5. **Сохранение простое.** `ProgressManager` хранит unlock/completed/stars в PlayerPrefs. Нет vehicle appearance, job types, repair/paint state, settings migration versioning как в целевой схеме.
6. **Локализация встроенная.** `Localization.cs` содержит русский словарь и stage labels; архитектурно приемлемо для маленького проекта, но не data-driven.
7. **Update subsystem встроена в gameplay controller.** `UpdateService`/`UpdateChecker`/UI в `GameController` усложняют основной flow.

## 6. Аудит `Assets/LittleGarage` (параллельный прототип)

`LittleGarage` выглядит как начатая реализация целевой архитектуры, но она неполная и не связана с legacy `Assets/Scripts`.

| Область | Файлы | Наблюдение |
|---|---|---|
| Core | `GameTypes.cs`, `Definitions.cs`, `ProgressData.cs`, `VehicleAppearance.cs`, `SaveSystem.cs` | Есть enums/defs/save DTO. Хорошее направление. |
| Bootstrap/GameManager | `Bootstrap.cs`, `GameManager.cs` | Есть оркестратор, но он ссылается на отсутствующие/непроверенные типы и системы в текущем виде проекта. |
| Systems | `InputRouter.cs`, `LevelManager.cs`, `ProgressManager.cs`, `SurfaceStateSystem.cs`, `ToolController.cs`, `WashingSystem.cs` | Есть зачатки wash/input/progression/tooling. Нет RepairSystem, CustomizationSystem, GarageEnvironment, CameraRig, полноценного UI. |
| Audio | `AudioManager.cs` | Процедурный audio, mixer groups, loop/one-shot/music. |
| UI | `UIManager.cs` | Частичный UI scaffold. |
| Vehicles | `VehicleFactory.cs`, `VehicleInstance.cs`, `VehiclePart.cs` | Процедурная генерация 3D mesh/parts из примитивов. Это не production art pipeline, но важный технический прототип. |
| Shader | `CarSurface.shader` | State-map shader для dirt/wet/foam/polish. |
| Tests | `Editor/GameFlowTests.cs`, `PlayMode/SceneSmokeTests.cs` | Есть тестовые файлы, но в текущем состоянии репозитория их компилируемость/актуальность не подтверждена запуском. |

### Риски LittleGarage

- **Дублирование имён/пространств.** Есть `Bootstrap`, `ProgressManager`, `AudioManager` и в legacy, и в LittleGarage. Нужна чёткая граница или миграция.
- **Не интегрировано в scene/build.** BuildSettings/scene по-прежнему ориентированы на legacy bootstrap. 
- **Неполнота.** Нет многих файлов, заявленных в `ARCHITECTURE.md` (GarageEnvironment, GarageCamera, RepairSystem, CustomizationSystem, PartController, VFXManager, JobDef assets, Resources catalog и т.д.).
- **Статус неизвестен.** Без Unity compile/test запуска нельзя считать прототип рабочим.

## 7. Ассеты: модели, текстуры, аудио, префабы

| Тип | Результат | Вывод |
|---|---|---|
| 3D модели (`fbx/obj`) | Не найдены | Нет импортированных 3D моделей. |
| Текстуры/изображения (`png/jpg/...`) | Не найдены в Assets | Нет texture assets; визуал процедурный. |
| Аудио (`wav/mp3/ogg`) | Не найдены | Весь звук генерируется кодом. |
| Prefabs | Не найдены | Runtime создаёт объекты кодом. |
| Materials/assets/controllers | Не найдены, кроме shader/meta/script assets | Материалы/anim controllers как assets отсутствуют. |
| Шейдеры | `Assets/LittleGarage/Shaders/CarSurface.shader` | Единственный shader asset. |

## 8. Input / UI / Build / CI

### Input

| Аспект | Текущее состояние | Риск/вывод |
|---|---|---|
| Legacy wash input | `UnityEngine.Input`, mouse + touch, EventSystem UI checks. | Работает для простого прототипа, но плохо масштабируется на 3D camera/orbit/tool raycast. |
| LittleGarage input | `InputRouter.cs` поддерживает mouse LMB paint, RMB/MMB orbit, wheel zoom, 1-finger paint, 2-finger orbit/pinch. | Лучше соответствует 3D, но нужно проверить интеграцию с камерой/physics/UI. |
| New Input System package | Не найден в manifest. | Для кроссплатформенного 3D взаимодействия стоит принять явное решение. |

### UI

| Аспект | Текущее состояние |
|---|---|
| Реализация | Полностью runtime-built UGUI через `UiFactory`/`GameController`. |
| Экраны legacy | Home, Levels, Play, Pause, Exit, Complete, Audio settings, Update dialog. |
| Шрифты | OS font fallback в runtime; нет font assets. |
| Адаптация | `UiLayout` считает safe-area fit; CanvasScaler/overlay canvas создаётся кодом. |

### Build / CI

| Файл | Наблюдение |
|---|---|
| `Assets/Editor/BuildScript.cs` | Явный build method для GameCI; читает `-customBuildPath`, поддерживает Windows/Android target; собирает только `Assets/Scenes/Game.unity`. |
| `Assets/Editor/VisualVerification.cs` | Большой in-editor verification: запускает PlayMode, проверяет UI/flow/wash, снимает скриншоты, пишет отчёт. Сильно завязан на legacy UI/flow. |
| `.github/workflows/build.yml` | Jobs: `verify` (GameCI StandaloneLinux64 + `VisualVerification.Run`), `build-windows`, `build-android`, `release`. Upload artifacts, GitHub release, `update.json`. |

CI вывод: pipeline полезен, но verification жёстко завязан на текущий 2D flow и будет мешать при перестройке, пока не будет адаптирован под новый 3D vertical slice. `[ВЫВОД]`

## 9. Что можно переиспользовать

| Кандидат | Файлы/классы | Что переиспользовать | Ограничения |
|---|---|---|---|
| Идея surface mask | `Assets/Scripts/WashSurface.cs` | Модель staged cleaning, zone coverage, stroke interpolation, dirty rect upload, smooth completion. | 2D Texture2D/Canvas space; для 3D нужен UV raycast + mesh surface map/shader. |
| Процедурный audio | `Assets/Scripts/AudioHelper.cs`, `Assets/LittleGarage/Audio/AudioManager.cs` | Bus separation, loop intensity, music modes, PlayerPrefs settings. | Нужна унификация API и отказ от дублирования; звук пока синтетический placeholder. |
| Save/settings | `Assets/Scripts/ProgressManager.cs`, `Assets/LittleGarage/Core/SaveSystem.cs`, `ProgressManager.cs`, `ProgressData.cs` | Unlock/stars/audio settings, версионированный JSON save. | Надо объединить в одну модель; старый формат несовместим с JobDef/VehicleDef. |
| Локализация | `Assets/Scripts/Localization.cs` | Русские UI строки, словарь, stage labels. | Не data-driven; потребуется расширение под repair/paint/customization. |
| UI helpers | `Assets/Scripts/UiFactory.cs`, `UiLayout.cs` | Быстрый runtime UI для прототипа/отладки. | Для production UI лучше перейти на нормальный UI architecture/prefabs или аккуратный UI toolkit layer. |
| Input stroke mapping | `Assets/Scripts/WashingController.cs` | События stroke started/moved/ended, radius/line interpolation. | Привязано к RectTransform/UV mask. |
| CI/build scaffolding | `Assets/Editor/BuildScript.cs`, `.github/workflows/build.yml` | Build args, artifact/release flow, update manifest pattern. | `VisualVerification.cs` нужно переписать под новый gameplay. |
| 3D prototype ideas | `Assets/LittleGarage/Vehicles/*`, `CarSurface.shader`, `Systems/*` | Procedural vehicle/part structure, surface state shader, input router, job/wash decomposition. | Требует ревизии, интеграции, тестов и замены процедурной геометрии на нормальные low-poly assets. |

## 10. Что нужно переделать

| Область | Почему | Рекомендация |
|---|---|---|
| `GameController.cs` монолит | Невозможно масштабировать на wash+repair+paint+3D camera. | Разбить на Bootstrap/GameManager/UIManager/Systems по ARCHITECTURE. |
| 2D wash view | `CarViewController`/overlay image не дают настоящую 3D поверхность. | Перейти на 3D mesh + UV raycast + surface state texture/material. |
| Процедурные 2D cars | `CarArt` рисует sprites, не 3D модели. | Заменить на реальные low-poly 3D vehicle prefabs/meshes. |
| Отсутствие repair/paint/customization | Бриф требует полный гаражный цикл. | Реализовать PartController/RepairSystem/CustomizationSystem. |
| Scene/bootstrap split | Два bootstrap-а и два набора runtime. | Выбрать одну целевую архитектуру и удалить/мигрировать legacy. |
| Verification | `VisualVerification.cs` проверяет старый 2D flow. | Создать новый smoke/verification под 3D scene и новые экраны. |
| Project settings | Нет `ProjectSettings.asset`. | Восстановить/зафиксировать canonical project settings в репозитории. |

## 11. Почему 2D pseudo-3D нельзя довести до требований нового брифа

1. **Нет реальной 3D геометрии.** Текущие “машины” — нарисованные в Texture2D спрайты (`CarArt.cs`), а не mesh assets с коллайдерами, материалами, pivot-ами и частями.
2. **Нет 3D взаимодействия.** Wash работает по UV прямоугольника `Image`; бриф требует raycast по 3D поверхности, orbit camera, инструменты в 3D пространстве.
3. **Repair невозможен как состояние реальных деталей.** В 2D нет отдельных wheel/engine/windshield/bumper объектов с физическим/логическим состоянием; можно только рисовать поверх.
4. **Paint/customization ограничены спрайтом.** Смена цвета/декалей на процедурном 2D рисунке не эквивалентна материалам/вариантам 3D модели.
5. **Камера и окружение не масштабируются.** Текущий фон — статическая текстура. Бриф требует 3D garage/work bay, освещение, читаемую камеру.
6. **Архитектурный потолок.** Монолитный `GameController` уже совмещает слишком много обязанностей; добавление repair/paint/3D сделает его неподдерживаемым.

Вывод: legacy 2D можно использовать как источник идей и отдельных алгоритмов, но не как фундамент целевой 3D-игры. Нужен вертикальный slice новой 3D архитектуры. `[ВЫВОД]`

## 12. Вердикт по существующим “моделям машин”

Все текущие “модели машин” — **процедурно нарисованные 2D спрайты**, генерируемые кодом в `Assets/Scripts/CarArt.cs`. Они **не являются** 3D моделями, не являются переиспользуемым 3D art asset и не подходят для целевого брифа без полной замены.

| CarKind / визуальный объект | Где определён/рисуется | Фактическая реализация | Вердикт |
|---|---|---|---|
| `Sedan` | `GameData.cs`, `CarArt.cs` (`DrawBody`, `MakeTexture`) | 2D texture, RoundRect/Rect/Circle/Poly | Не переиспользовать как 3D. Можно оставить только как визуальный reference. |
| `Beetle` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Sports` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Race` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Taxi` | `GameData.cs`, `CarArt.cs` | 2D texture + sign/detail | Не переиспользовать как 3D. |
| `Police` | `GameData.cs`, `CarArt.cs` | 2D texture + lightbar/detail | Не переиспользовать как 3D. |
| `Ambulance` | `GameData.cs`, `CarArt.cs` | 2D texture + cross/detail | Не переиспользовать как 3D. |
| `Bus` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `SchoolBus` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `FireTruck` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Tractor` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Excavator` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Bulldozer` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `DumpTruck` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Mixer` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `GarbageTruck` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `IceCream` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Limo` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `MonsterTruck` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |
| `Van` | `GameData.cs`, `CarArt.cs` | 2D texture | Не переиспользовать как 3D. |

Дополнительно: `GameArt.cs` генерирует 2D фон wash bay, `AnimalArt.cs` генерирует 2D driver sprites — это тоже не 3D ассеты. `[ФАКТ]`

## 13. Приоритеты для следующего этапа

1. **Зафиксировать target architecture.** Принять `ARCHITECTURE.md` как базу, но привести в соответствие scene/bootstrap/package decisions.
2. **Сделать 3D vertical slice.** Одна сцена, один garage, одна 3D машина, raycast wash surface, 2-3 инструмента, camera orbit/focus.
3. **Вынести legacy в quarantine.** Не удалять сразу полезные алгоритмы, но перестать считать legacy 2D runtime целевой базой.
4. **Восстановить ProjectSettings.asset и canonical build settings.** Иначе CI/build reproducibility неполная.
5. **Переписать verification.** Текущий `VisualVerification.cs` проверяет старый продукт, не новый 3D бриф.

## 14. Итоговый аудит-вердикт

Проект содержит рабочий legacy 2D wash-прототип с полезными алгоритмическими заготовками (mask wash, procedural audio, save/settings, localization, CI). Однако он **не соответствует** новому брифу по ключевым требованиям: реальная 3D low-poly среда, реальные 3D машины, repair/paint/customization, модульная система под расширение. Существующие “car models” — процедурные 2D спрайты из `CarArt.cs`; переиспользовать их как 3D-ассеты нельзя. Наиболее разумный путь — использовать legacy как reference/донор отдельных идей, а целевую реализацию строить заново вокруг 3D vertical slice и архитектуры из `ARCHITECTURE.md`.
