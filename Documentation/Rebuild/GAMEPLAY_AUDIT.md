# GAMEPLAY_AUDIT

Дата: 2026-11-24  
Основание: статический анализ `Assets/Scripts/*`, `Assets/LittleGarage/*`, README/REPORT, CI workflow. Unity runtime не запускался.

## 1. Текущее состояние gameplay (legacy)

### 1.1 Реализованные пользовательские потоки

| Поток | Статус | Доказательство |
|---|---|---|
| Home screen / start | Реализовано | `GameController.cs` UI panels, `ScreenId.Home` |
| Level select | Реализовано | `GameController.cs`, `GameData.Levels`, `ProgressManager` |
| Wash gameplay | Реализовано | `WashingController`, `WashSurface`, `CarArt`, `GameController` |
| Stage progression (Wet/Foam/Scrub/Rinse/Dry) | Реализовано | `GameData.StagesFor`, `WashSurface.Progress/Fill/Complete`, `WashingTools` |
| Reward/complete screen | Реализовано | `GameController` complete card, stars/progress |
| Pause/settings/audio | Реализовано | `GameController`, `AudioHelper` settings/PlayerPrefs |
| Save progress | Реализовано | `ProgressManager` PlayerPrefs keys |
| Update check/install UI | Реализовано в коде | `AppInfo`, `UpdateService`, `UpdateChecker`, `GameController` |

### 1.2 Не реализованные потоки целевого брифа

| Поток | Статус в legacy | Статус в LittleGarage prototype | Комментарий |
|---|---|---|---|
| 3D garage exploration/camera | Нет | Частично | Есть `GarageCamera`, `GarageEnvironment`, но качество/интеграция не подтверждены. |
| Engine repair | Нет | Частично/не подтверждено | Есть `RepairSystem`/`VehiclePart`, но полный engine repair flow не подтверждён Unity запуском. |
| Wheel replacement | Нет | Частично/не подтверждено | Part kinds/wheels присутствуют, UX/flow не подтверждён. |
| Windshield/part replacement | Нет | Частично/не подтверждено | Part model есть, сценарии не проверены. |
| Painting | Нет | Частично | `CustomizationSystem` и body color logic есть, полноценный paint bay flow не подтверждён. |
| Customization (decals/rims) | Нет | Частично | Data fields есть, UX/visual result не подтверждён. |
| Job/task system with mixed operations | Нет | Частично | `JobDef`/tasks есть в Definitions, orchestration требует проверки. |

## 2. Детальный аудит wash-механики

### 2.1 Hit detection / tool application

| Вопрос | Текущая реализация | Оценка |
|---|---|---|
| Как определяется попадание | `WashingController.TryScreenToUv` проецирует screen point в `RectTransform` машины, затем `WashSurface` применяет кисть в UV маски; используется panel/silhouette mask. | Хорошо для 2D overlay, плохо для 3D mesh. |
| Поддержка мыши/тача | `Input.GetMouseButton`, `Input.touchCount/GetTouch`, `EventSystem.IsPointerOverGameObject`. | Работает для legacy UI. |
| Интерполяция штриха | `PaintLine` интерполирует по distance/radius. | Хорошо. |
| Радиус | `WashingTools.Radius` * `brushRadiusMultiplier` (1.25 default) * size factor. | Хорошо, но баланс требует PlayMode проверки. |

### 2.2 Surface/mask/storage

| Вопрос | Текущая реализация | Оценка |
|---|---|---|
| Маски | Float arrays: `_wet`, `_foam`, `_clean`, `_scrub`, `_dry`, `_dirt`, `_silhouette`, zone sums. | Эффективно и понятно. |
| Текстура | Одна `Texture2D` 256x256 RGBA32, dirty rect upload через `SetPixels32`. | Подходит для mobile 2D. |
| Зоны | 6 зон (3x2), `ZoneFinish=0.82`, этап завершается по min zone progress. | Хорошее anti-cheese решение. |
| Completion | `BeginCompletion/AdvanceCompletion` плавно добивает остаток. | Хорошо. |

### 2.3 Water/foam/sponge/dry

| Этап | Реализация | Риск/ограничение |
|---|---|---|
| Wet | Увеличивает wet mask, runoff/droplets visuals. | 2D visual only. |
| Foam | Накладывает foam + bubbles/soap visuals. | 2D only. |
| Scrub | Требует foam/scrub interaction, уменьшает dirt. | Порог/ощущение должны проверяться на UX. |
| Rinse | Убирает foam, переводит к clean/wet states. | Визуал процедурный. |
| Dry | Повышает dry/shine, final sweep. | 2D only. |

### 2.4 Stage completion / tools / camera / cars

| Аспект | Текущее состояние | Оценка |
|---|---|---|
| Переключение инструментов | Автоматически по stage; UI показывает tool label/icon. | Просто и понятно. |
| Разные автомобили | 20 CarKind, 30 levels; размеры/грязь/типы различаются. | Контент есть, но визуально 2D. |
| Камера | Ортографическая 2D камера. | Не подходит для 3D garage. |
| Завершение этапа | По min zone progress >= 0.82, затем плавное добивание. | Хорошо. |

## 3. Найденные проблемы и архитектурные ограничения

| ID | Проблема | Влияние | Приоритет |
|---|---|---|---|
| G1 | Монолит `GameController` | Любая новая механика увеличивает риск регрессий. | P0 |
| G2 | 2D overlay wash привязан к RectTransform/UI | Нельзя напрямую перенести на 3D mesh raycast. | P0 |
| G3 | Нет repair/paint/customization в legacy runtime | Целевой бриф не покрыт. | P0 |
| G4 | Нет реальных 3D parts/colliders/material variants | Невозможно честное взаимодействие с деталями. | P0 |
| G5 | Verification CI привязан к legacy flow | Будет блокировать новые изменения. | P1 |
| G6 | Update subsystem и старые имена LittleCarWash | Риск конфликтов при ребрендинге/новой сборке. | P1 |
| G7 | Save model только про уровни/stars | Недостаточно для appearance/jobs/parts/customization. | P1 |
| G8 | Процедурный контент без asset pipeline | Трудно контролировать качество/арт-направление. | P1 |

## 4. Что сохранить, переработать, выбросить

### Сохранить (с адаптацией)

- Идею zoned completion и dirty-rect texture update из `WashSurface`.
- Stroke interpolation/radius scaling из `WashingController`.
- PlayerPrefs/settings паттерны, русскую локализацию.
- Procedural audio bus concept (временно), build script/logging patterns.

### Переработать

- `GameController` -> modular orchestration.
- Save model -> versioned profile with jobs, vehicles, appearance, settings.
- UI runtime generation -> structured UI screens/components.
- CI verification -> smoke tests for new 3D flow.

### Выбросить/не использовать как основу

- 2D CarArt sprites как финальный vehicle presentation.
- 2D CarViewController wash overlay как основная механика.
- Reward animals как обязательный core feature (не в брифе).
- Legacy update/release naming, если оно конфликтует с новым продуктом.

## 5. LittleGarage prototype gameplay assessment

| Система | Код есть | Готовность | Комментарий |
|---|---|---|---|
| InputRouter | Да | Средняя | Есть orbit/pinch/paint, но нужен Unity/UX тест. |
| SurfaceStateSystem | Да | Средняя | Есть state-map; нужно проверить UV/raycast на реальных 3D meshes. |
| WashingSystem | Да | Средняя | Есть tool sequencing/progress; нужна интеграция с UI/effects. |
| RepairSystem | Да | Низкая/не проверена | Требует реального PlayMode сценария. |
| CustomizationSystem | Да | Низкая/средняя | Нужна проверка материалов/appearance save. |
| Progress/Save/Level | Да | Средняя | Модель ближе к целевой, но нужен migration/consistency pass. |
| UIManager | Да | Низкая/средняя | Runtime UGUI большой; нужна проверка экранов/ссылок. |

## 6. Вывод

Текущая игра — хороший 2D wash prototype, но не фундамент для 3D garage+repair+paint продукта. Целесообразно сохранить алгоритмические идеи wash/save/audio, но gameplay architecture перестроить вокруг 3D vehicle entities, job/task definitions, modular systems и новых Unity verification tests.
