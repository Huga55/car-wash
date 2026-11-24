# ASSET_INVENTORY

Дата: 2026-11-24  
Репозиторий: `/e/my/wash-cars`  
Метод: статический аудит файловой системы и кода. Unity Editor не установлен, поэтому preview/mesh stats через Editor, import settings, реальные polycount/material draw calls в Unity не подтверждены.

## 1. Сводка инвентаризации

| Категория | Найдено | Статус |
|---|---:|---|
| 3D-модели (`.fbx/.obj/.blend/.gltf/.glb`) | 0 | Отсутствуют |
| Текстуры/изображения (`.png/.jpg/...`) | 0 в `Assets/` | Отсутствуют |
| Аудио (`.wav/.mp3/.ogg`) | 0 | Отсутствуют |
| Prefab (`.prefab`) | 0 | Отсутствуют |
| Material assets (`.mat`) | 0 | Отсутствуют |
| Animation/Animator (`.anim/.controller`) | 0 | Отсутствуют |
| Shader assets | 1 | `Assets/LittleGarage/Shaders/CarSurface.shader` |
| Scenes | 1 | `Assets/Scenes/Game.unity` |
| C# scripts | 52 (`.cs` files total in Assets; includes tests/editor/runtime) | Подтверждено |
| Runtime procedural “car models” | 20 CarKind в legacy + 10 VehicleDef в LittleGarage prototype | Кодовые/процедурные, не 3D asset files |

## 2. 3D-модели автомобилей

### 2.1 Legacy procedural cars (`Assets/Scripts/CarArt.cs`, `GameData.cs`)

Все автомобили legacy-игры генерируются процедурно в Texture2D. Это **не 3D-модели**.

| Название / CarKind | Формат | Полигоны | Материалы | Текстуры | Корректные UV | Collider | Animator/Clips | Использование в новой игре | Категория |
|---|---|---:|---:|---|---|---|---|---|---|
| Sedan | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Только как визуальный reference/placeholder | C |
| Beetle | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Sports | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Race | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Taxi | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Police | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Ambulance | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Bus | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| SchoolBus | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| FireTruck | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Tractor | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Excavator | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Bulldozer | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| DumpTruck | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Mixer | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| GarbageTruck | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| IceCream | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Limo | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| MonsterTruck | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |
| Van | Procedural 2D sprite | N/A | N/A | N/A | N/A | N/A | N/A | Placeholder/reference | C |

**Категория C обоснование:** целевой бриф требует 3D low-poly машины, repair parts, paint/customization на реальных 3D объектах. Texture2D sprites не дают mesh/collider/material/part hierarchy.

### 2.2 LittleGarage procedural 3D prototypes (`Assets/LittleGarage/Vehicles/VehicleFactory.cs`)

В репозитории есть процедурная генерация 3D-мешей в runtime-коде. Это не импортированные model assets, но технический прототип.

| Название | Формат | Полигоны | Материалы | Текстуры | UV | Collider | Animator | Использование | Категория |
|---|---|---:|---:|---|---|---|---|---|---|
| Sedan (VehicleDef `sedan`) | Runtime procedural mesh | Не измерено (нет Unity Editor) | Runtime materials | Нет | Генерируются кодом, качество не подтверждено | MeshCollider на paintable/body parts | Нет | Можно как временный прототип для vertical slice | B |
| Sports (`sports`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично/по коду | Нет | Временный прототип | B |
| SUV (`suv`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| Pickup (`pickup`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| Police (`police`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| FireTruck (`fire`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| Ambulance (`ambulance`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| Truck (`truck`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| Bus (`bus`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |
| DumpTruck (`dump`) | Runtime procedural mesh | Не измерено | Runtime | Нет | Кодом | Частично | Нет | Временный прототип | B |

**Ограничение:** категория B условна. Без Unity Editor не подтверждены полигоны, UV-артефакты, коллайдеры, производительность, визуальное качество.

## 3. Инструменты, окружение, детали, персонажи

| Группа | Найденные assets | Фактическая реализация | Категория | Комментарий |
|---|---|---|---|---|
| Инструменты мойки | Нет 3D model files | Legacy: UI icons/sprites в `UiFactory.cs`; LittleGarage: `ToolController.CreateHandTool` procedural primitives. | B/C | Для финала нужны читаемые 3D tools/prefabs; текущие — placeholders. |
| Окружение/гараж | Нет model files | Legacy: 2D background `GameArt.cs`; LittleGarage: procedural garage primitives в `GarageEnvironment.cs`. | B/C | 3D garage есть только как процедурный прототип. |
| Детали автомобилей | Нет отдельных model files | Legacy: нет реальных деталей; LittleGarage: `VehiclePart` + procedural wheel/part primitives. | B | Нужна доработка до полноценной repair/customization схемы. |
| Персонажи/животные | Нет model files | Legacy: procedural 2D animal portraits `AnimalActor.cs`. | C для целевого брифа | Не входят в core garage loop; можно исключить. |

## 4. Текстуры, материалы, шейдеры

| Asset | Путь | Тип | Статус | Оценка |
|---|---|---|---|---|
| CarSurface.shader | `Assets/LittleGarage/Shaders/CarSurface.shader` | Shader | Присутствует | B - полезен для state-map wash, требует проверки в Unity/Built-in pipeline. |
| Legacy materials/textures | - | - | Отсутствуют | Процедурно создаются в runtime. |
| Import textures/models | - | - | Отсутствуют | Нет texture pipeline/asset source. |

## 5. Анимации, VFX, UI

| Группа | Наличие | Реализация | Оценка |
|---|---|---|---|
| Animation clips / Animator controllers | 0 assets | Legacy анимации кодом (coroutines, lerp); LittleGarage VehiclePart tweens. | C как asset pipeline, B как прототип поведения. |
| Particle systems assets | 0 prefab/assets | Кодом в `GameController`/`EffectFx` (runtime-created particles). | B для прототипа, нужны оптимизированные VFX presets. |
| UI icons/sprites | 0 image assets | Процедурные sprites в `UiFactory.cs`. | B для placeholder, C для финального art. |
| Fonts | 0 font assets | OS fallback fonts в runtime. | B/C: нужен лицензированный/контролируемый font asset. |

## 6. Недостающие ресурсы для целевого брифа

| Нужный ресурс | Почему нужен | Приоритет |
|---|---|---|
| 3D low-poly модели машин (>=6-10) | Реальный 3D garage/wash/repair/paint | P0 |
| Раздельные детали: колёса, бамперы, фары, зеркала, двигатель, лобовое | Repair/customization state machine | P0 |
| 3D garage environment | Визуальная цель продукта | P0 |
| Tool 3D models/prefabs | Читаемость взаимодействия | P1 |
| PBR/stylized материалы, texture atlases | Единый стиль и производительность | P1 |
| Audio clips (UI, wash, repair, paint, ambient, reward) | ASMR/feedback качество | P1 |
| VFX prefabs | Контроль качества и перфоманса | P1 |
| UI sprite/font assets | Консистентный UI и лицензии | P2 |

## 7. Ограничения аудита

- Невозможно честно измерить polycount, UV issues, draw calls, texture memory, import settings без Unity Editor/Profiler.
- В репозитории отсутствуют бинарные 3D/art/audio assets; поэтому оценка “можно использовать” относится только к процедурным прототипам/коду.
- Любой вывод о визуальном качестве существующих procedural models требует запуска в Unity.
