# TARGET_ARCHITECTURE

Дата: 2026-11-24  
Цель: целевая архитектура Little Car Workshop под Unity 2022.3.61f1, Windows/Android, 3D garage gameplay.

## 1. Архитектурные принципы

- **Один runtime entry point**: `Bootstrap` -> `GameManager`.
- **Одна ответственность на систему**: wash, repair, paint, input, UI, save, audio, camera — отдельные модули.
- **Data-driven content**: машины, задания, инструменты, детали, цвета, награды описываются ScriptableObject/definitions.
- **No monolith**: `GameManager` только оркестрирует состояние и события.
- **No per-car unique scripts**: различия в данных/конфигурации/prefab composition.
- **Mobile-safe**: object pooling, ограниченные VFX, минимум allocations in Update, простые colliders.

## 2. Предлагаемая структура папок

```text
Assets/
  Scenes/
    Game.unity
  Art/
    Vehicles/
    Garage/
    Tools/
    UI/
    VFX/
  Audio/
    Music/
    SFX/
  Prefabs/
    Vehicles/
    Parts/
    Tools/
    Garage/
    UI/
  Resources/ or Addressable-like catalog (if adopted)
  Scripts/
    Core/
    Data/
    Gameplay/
      Wash/
      Repair/
      Paint/
      Camera/
      Input/
      Jobs/
    UI/
    Audio/
    Save/
    Bootstrap/
  Tests/
    EditMode/
    PlayMode/
```

Можно сохранить неймспейс/папку `LittleGarage` во время миграции, но в итоге должен остаться один канонический runtime.

## 3. Основные системы

| Система | Назначение | Ответственность | Зависимости | Основные данные | Взаимодействие |
|---|---|---|---|---|---|
| Bootstrap | Точка входа | Создать/загрузить config, managers, scene services. | Unity scene, Resources/defs | GameConfig asset/ref | Создаёт GameManager и service graph. |
| GameManager | Оркестрация | Game state, job flow, transitions, pause/restart. | All systems | CurrentJob, CurrentVehicle, GameState | Подписывается на UI/input/system events. |
| GameConfig/ContentCatalog | Data catalog | Доступ к VehicleDef/JobDef/ToolDef/PartDef. | ScriptableObjects | defs arrays/maps | Read-only для систем. |
| Level/JobManager | Прогрессия задач | Выбор/запуск/завершение job, unlock logic. | GameConfig, ProgressManager | JobDef, job state | Сообщает GameManager о старте/завершении. |
| VehicleFactory/VehicleSpawner | Создание машины | Спавн prefab, инициализация частей/материалов/appearance. | VehicleDef, Part prefabs | VehicleInstance, part map | Отдаёт VehicleInstance GameManager/Wash/Repair. |
| VehicleInstance | Модель машины в сцене | Ссылки на части, paintable surfaces, anchors, appearance. | PartController, materials | VehicleDef, appearance | Используется всеми gameplay systems. |
| InputRouter | Единый ввод | Pointer/raycast/orbit/zoom, UI block detection. | Camera, EventSystem, Physics | pointer state | Шлёт события в active interaction system. |
| CameraRig | Камера | Focus/transition/orbit limits/zoom. | Vehicle anchors, job zone | camera target, bounds | GameManager задаёт focus; InputRouter даёт orbit/zoom. |
| WashingSystem | Мойка | Tool application по surface masks, progress. | SurfaceStateSystem, ToolController, InputRouter | tool/stage progress | Вызывает Complete event. |
| SurfaceStateSystem | Состояние поверхности | Dirt/wet/foam/polish masks, UV raycast paint, shader params. | MeshCollider/UV, shader/material | state maps/textures | Используется WashingSystem/VehicleInstance. |
| ToolController | Инструменты | Текущий инструмент, доступность, cursor/hand tool, VFX hooks. | UIManager, defs | ToolDef | UI/Input выбирают tool; gameplay применяет. |
| RepairSystem | Ремонт | State machine деталей: broken/removed/replaced/fixed. | VehiclePart/PartController, InputRouter | PartDef, repair steps | Вызывает Complete event. |
| Customization/PaintSystem | Покраска/кастомизация | Цвета, decals, rims, preview/apply. | VehicleInstance materials, UI | appearance options | Сохраняет appearance через ProgressManager. |
| ProgressManager/SaveSystem | Сохранение | Profile load/save/reset, unlocks, stars, appearance. | File/PlayerPrefs/JSON | ProgressData | GameManager/JobManager читают и пишут. |
| UIManager | UI | Экраны, HUD, buttons, hints, settings. | Localization, ProgressManager, events | view models | Эмитит команды, слушает state updates. |
| AudioManager | Звук | Music/SFX/loop buses, settings. | Audio assets/clips or procedural fallback | bus volumes, clip map | Gameplay/UI вызывают Play/SetLoop. |
| VFXManager/Pool | Эффекты | Spawn/despawn particles/decals/simple feedback. | Prefabs/pools | pooled instances | Wash/repair/paint запрашивают эффекты. |

## 4. Data model

### ScriptableObjects / defs

| Asset | Поля | Назначение |
|---|---|---|
| VehicleDef | id, displayNameRu, prefab/ref, kind, camera profile, paintable parts, default appearance, supported jobs/tags | Описание машины. |
| JobDef | id, vehicleId, zone, tasks, reward, unlock rule | Описание задания. |
| TaskDef | kind (Wash/Repair/Paint/Customize), target part/tool, instruction, completion rule | Шаг задания. |
| ToolDef | id, kind, labelRu, icon, prefab, SFX/VFX keys, radius/strength | Инструмент. |
| PartDef | id, part kind, broken prefab/state, fixed prefab/state, install steps | Ремонтная деталь. |

### Runtime/save data

```csharp
ProgressData {
  version;
  stars;
  completedJobs[];
  unlockedJobs[];
  selectedVehicleId;
  vehicleAppearances[];
  audioSettings;
}
```

## 5. Interaction pipeline

1. UI/InputRouter получает pointer.
2. Если pointer над UI — gameplay interaction блокируется.
3. InputRouter делает raycast в 3D сцену.
4. GameManager/active system определяет, может ли текущий tool/step обработать hit.
5. Target system применяет действие: paint surface mask / repair part state / apply paint.
6. System обновляет progress и сообщает UI/Audio/VFX.
7. При completion JobManager/GameManager переводит на следующий шаг.

## 6. Scene composition

`Game.unity` содержит только bootstrap/root objects:
- Bootstrap
- EventSystem
- Main Camera + CameraRig
- LightRig
- UI root
- Managers root (или создаётся Bootstrap-ом)

Машины/инструменты/динамические объекты спавнятся runtime.

## 7. Сохранение и миграция

- Версионировать `ProgressData.version`.
- При несовместимости или повреждении JSON — fallback на defaults + лог warning.
- Сохранять после завершения job и при изменении settings.
- Не сохранять transient wash mask state, если это не требуется дизайном.

## 8. Тестируемость

- EditMode: catalog integrity, job refs, save round-trip, state machine transitions, tool/surface math.
- PlayMode: scene bootstrap, job start, wash progress, repair step, paint apply, complete screen, save reload.
- Build smoke: Windows batchmode build, log parsing, artifact presence.

## 9. Миграция от legacy

| Этап | Действие | Риск |
|---|---|---|
| 1 | Зафиксировать один entry point и отключить legacy GameController flow. | Сломать старые сцены/CI. |
| 2 | Перенести save/audio/localization полезные части. | Потеря совместимости старых saves. |
| 3 | Сделать 3D vertical slice: 1 garage, 1 car, wash+simple repair+paint. | Переоценка объёма. |
| 4 | Обновить CI verification и build scripts. | Старый release flow сломается. |
| 5 | Удалить/архивировать legacy scripts после стабилизации. | Потеря reference code. |
