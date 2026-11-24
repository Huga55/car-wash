# IMPLEMENTATION_ROADMAP

Дата: 2026-11-24  
Основание: `PROJECT_AUDIT.md`, `ASSET_INVENTORY.md`, `GAMEPLAY_AUDIT.md`, `REFERENCE_SPECIFICATION.md`, `TARGET_ARCHITECTURE.md`, `WINDOWS_BUILD_GUIDE.md`.

## Статусные метки (STEP 3 §9 — 5 категорий)

- **[CODE]** — реализовано в коде.
- **[OFFLINE]** — проверено без Unity (stubs C# compile, behavior harness, GUID/path/meta sanity).
- **[UNITY]** — проверено в Unity Editor (импорт/компиляция/Editor tests/PlayMode).
- **[BUILD]** — проверено в Windows build (EXE + data + log).
- **[NOT]** — всё ещё не проверено или блокировано средой.

Текущий срез STEP 3: Stage 0–6, 8 (code/build parts) — **[CODE] + [OFFLINE]**;
**[UNITY]** — **частично**: пользовательский compile выявил 13 `CS1069` (не хватало
built-in модулей `particlesystem`/`physics`) и затем 2 `CS0117` + 3 warning
(`ParticleSystemStopBehavior`, неиспользуемые члены); всё исправлено, но чистая
компиляция не подтверждена. **[BUILD]** — **[NOT]**
(Unity 2022.3.61f1 недоступен в sandbox, license не подтверждена).
Stage 7 (визуальная полировка/перф) — **[NOT]**.
`ProjectSettings/*.asset` восстановлены **самим Unity** у пользователя (не вручную);
`EditorBuildSettings.asset` потребовал возврата сцены `Game.unity`. STEPs 5–7 отчёта
`REPORT.md` остаются невыполненными.

## 1. Цель roadmap

Перевести проект от 2D pseudo-3D wash prototype к 3D Little Car Workshop (wash + repair + paint/customization), сохранив управляемый объём и возможность локальной Windows-сборки.

## 2. Принципы выполнения

- Сначала стабилизация архитектуры и vertical slice, потом масштабирование контента.
- Каждая механика должна иметь критерий завершения, smoke test и диагностику.
- Никаких "магических" per-car scripts; различия через data/prefabs.
- Любой неподтверждённый факт референса помечать как assumption/proposal.

## 3. Этапы

### Stage 0 — Audit freeze and baseline (done in docs)

**Задачи**
- Зафиксировать текущее состояние проекта и ограничения.
- Подготовить документацию в `Documentation/Rebuild/`.

**Зависимости:** нет.  
**Критерий завершения:** все 7 документов созданы, ограничения явно указаны.

### Stage 1 — Project stabilization / single runtime — **[CODE]/[OFFLINE]; ProjectSettings recovery [NOT]**

**Задачи**
- Выбрать канонический runtime (`Assets/LittleGarage` или новый `Assets/Scripts/Core`), убрать двойной Bootstrap ambiguity.
- Восстановить/проверить `ProjectSettings.asset` и канонические player/build settings.
- Починить namespace/assembly boundaries, compile в Unity.
- Обновить scene и build settings на один entry point.

**Зависимости:** Stage 0.  
**Риски:** скрытые настройки проекта отсутствуют; конфликт legacy/LittleGarage.  
**Критерий завершения:** Unity Editor открывает проект, Game scene запускается без compile errors.

### Stage 2 — 3D vertical slice core — **[CODE]/[OFFLINE]; Unity PlayMode [NOT]**

**Задачи**
- 1 гаражная 3D сцена, 1 рабочая машина, camera rig, input raycast.
- VehicleInstance + part anchors + paintable surface.
- UI shell: Home, Garage/Job, Play HUD, Pause, Complete.

**Зависимости:** Stage 1.  
**Критерий завершения:** PlayMode: можно стартовать job, видеть 3D машину, управлять камерой/вводом.

### Stage 3 — Wash system MVP — **[CODE]/[OFFLINE]** (6 tools, real state transitions); Unity [NOT]

**Задачи**
- Перенести mask/state-map идею в 3D UV surface.
- Инструменты: pressure/foam/sponge/rinse/dry/polish (минимум 5).
- VFX/SFX feedback, progress HUD, completion rule.

**Зависимости:** Stage 2.  
**Критерий завершения:** Машину можно помыть до завершения этапа, прогресс корректен, нет critical errors.

### Stage 4 — Repair system MVP — **[CODE]** (код есть), gameplay в Unity [NOT]

**Задачи**
- Part state machine: broken -> removed -> replaced -> fixed.
- Минимум 2-3 repair вида: wheel, windshield, engine/simple part.
- Guided UI hints, audio feedback.

**Зависимости:** Stage 2, data model from Stage 1.  
**Критерий завершения:** Repair job выполняется от начала до конца, состояние сохраняется в job result.

### Stage 5 — Paint/customization MVP — **[CODE]** (код есть), gameplay в Unity [NOT]

**Задачи**
- Выбор цвета/материала, preview/apply.
- Опционально decals/rims если есть assets.
- Save appearance in profile.

**Зависимости:** Stage 2, save model.  
**Критерий завершения:** Внешний вид меняется и восстанавливается после reload.

### Stage 6 — Progression/save/content — **[CODE]/[OFFLINE]** (10 jobs в каталоге, save/load проверен); Unity [NOT]

**Задачи**
- JobDef catalog: минимум 10 jobs.
- 6-10 vehicles, unlock/reward flow.
- Versioned save migration/fallback.

**Зависимости:** Stages 3-5.  
**Критерий завершения:** Последовательное прохождение нескольких jobs с сохранением прогресса.

### Stage 7 — UX polish, audio, performance — **[CODE]** (RU UI/procedural audio), визуал/перф [NOT]

**Задачи**
- Русский UI pass, читаемые подсказки, settings.
- Оптимизация: batching, texture sizes, VFX limits, GC alloc checks.
- Target 30/60 FPS profiles.

**Зависимости:** Stages 3-6.  
**Критерий завершения:** Стабильный PlayMode, приемлемый perf на целевом weak Android profile.

### Stage 8 — Verification, build, release prep — **[CODE]/[OFFLINE]** (build/CI scripts, test gate); реальный build/CI [NOT]

**Задачи**
- Обновить `VisualVerification`/smoke tests под новый flow.
- Обновить `.github/workflows/build.yml` и AppInfo/update naming.
- Windows batchmode build, artifact check, Player.log smoke.
- Android build check (при наличии Android modules).

**Зависимости:** Stage 7.  
**Критерий завершения:** `Builds/Windows/LittleCarWorkshop.exe` собирается локально, smoke run проходит, CI green.

## 4. Рекомендуемый порядок реализации (минимальный риск)

1. Stage 1 stabilization.
2. Stage 2 vertical slice.
3. Stage 3 wash.
4. Stage 5 paint (быстрее даёт видимый результат).
5. Stage 4 repair.
6. Stage 6 content/progression.
7. Stage 7 polish/perf.
8. Stage 8 build/CI.

## 5. Definition of Done по ключевым механикам

| Механика | DoD |
|---|---|
| Wash | Raycast по 3D поверхности, минимум 5 инструментов, видимый прогресс, completion, SFX/VFX. |
| Repair | Минимум 3 типа деталей, понятные шаги, состояние детали меняется и сохраняется в job result. |
| Paint | Выбор цвета/варианта, мгновенный preview/apply, сохранение appearance. |
| Progress | Jobs unlock/save/load/reset работают, повреждённый save не ломает игру. |
| Build | Windows batchmode build создаёт exe, лог без критических ошибок. |

## 5.1 Восстановление ProjectSettings.asset (обязательный шаг перед Unity-проверкой)

`ProjectSettings/ProjectSettings.asset` в репозитории отсутствует и никогда не был закоммичен;
он не в `.gitignore`. Проверено в STEP 3: нет копий в `/e`, `/tmp`, Docker или истории git;
другого легитимного источника настроек в проекте нет. Это **не** ошибочно удалённый файл,
который можно безопасно выдумать вручную: player/input/graphics/quality/physics settings
и active scene задаются Unity. Восстановление — только через Unity Editor, воспроизводимо.

**Предварительные условия:** Unity **2022.3.61f1** + модуль **Windows Build Support
(IL2CPP/Mono)** + активная лицензия. В текущем sandbox их нет → шаг 2 и далее **не выполнялись**.

Точные шаги вручную:

1. Установить Unity **2022.3.61f1** (точная сборка, не «latest 2022.3.x») через Unity Hub.
2. Открыть `/e/my/wash-cars` как существующий проект. Unity создаст недостающие
   `ProjectSettings/*.asset` со значениями по умолчанию для этой версии — это ожидаемый
   результат, а не ошибка импорта.
3. В `Edit → Project Settings → Player`: company/product name, `Default Screen Width/Height`,
   `Run In Background`, **Active Input Handling = Input Manager (Old)** — это ожидает
   `InputRouter` (legacy `Input`, ось `Mouse ScrollWheel`).
4. В `Edit → Project Settings → Graphics/Quality`: подтвердить **Built-in Render Pipeline**
   (не URP/HDRP); shader `Assets/LittleGarage/Shaders/CarSurface.shader` должен импортироваться
   без ошибок. Не менять архитектуру рендера молча.
5. `File → Build Settings`: оставить только `Assets/Scenes/Game.unity`, первой и включённой
   (совпадает с текущим `EditorBuildSettings.asset`, GUID `bbbb…`).
6. Сохранить сцену, дождаться полной компиляции, записать Editor log. Проверить Console
   на errors/warnings. При ошибках — чинить только блокирующие (STEP 3 §8).
7. Закоммитить получившиеся `ProjectSettings/*.asset` **отдельным** изменением (не вместе
   с STEP 2 кодом), чтобы история восстановления была прозрачной.
8. Проверить `Player → Other Settings → Scripting Runtime Version` совместима с C# 9;
   `com.unity.test-framework` в `Packages/manifest.json` подтянут.

**Outcome:** шаг 1 выполнен пользователем (Unity `2022.3.61f1 (6c53ebaf375d)` подтверждён в
`ProjectVersion.txt`); шаг 2 выполнен Unity — сгенерирован полный набор `ProjectSettings/*.asset`
и `Packages/packages-lock.json`. Проверено: `activeInputHandler: 0` (Input Manager Old),
Built-in RP (`m_CustomRenderPipeline: {fileID: 0}`), ось `Mouse ScrollWheel` присутствует.
Шаг 3–4 частично подтверждены косвенно; шаг 5 **потребовал ручной правки**: Unity выписал
`m_Scenes: []`, запись `Game.unity` (guid `bbbb…`) возвращена. Шаг 6 выявил 13 `CS1069` → исправлено в `manifest.json` → пользователь прислал второй лог
(2 `CS0117` + 3 warning), исправлено в коде → **третий лог/чистая компиляция ожидается**.
Шаг 7 (коммит) **не выполнялся** по запрету STEP 3; файлы untracked.

Поддельные `.asset`-файлы не создавались: настройки сгенерированы самим Unity. После успешной
перекомпиляции обновить `REPORT.md` §4 и статус категории 3 фактическим результатом.

## 6. Основные риски и смягчение

| Риск | Вероятность | Влияние | Смягчение |
|---|---|---|---|
| Нет 3D assets | Высокая | Высокое | Временные primitives/procedural meshes только для vertical slice; параллельно подготовить asset pack. |
| Двойной runtime ломает сборку | Высокая | Высокое | Зафиксировать один entry point и удалить ambiguity до feature work. |
| Missing ProjectSettings.asset | Средняя | Среднее/высокое | Восстановить из Unity Editor и закоммитить. |
| Legacy CI verification | Высокая | Среднее | **[CODE]** workflow больше не вызывает `VisualVerification.Run`; вместо него `game-ci/unity-test-runner@v4` (EditMode+PlayMode). **[NOT]** прогон в GH Actions. |
| Mobile perf with 3D | Средняя | Высокое | Ранний профилинг, простые shaders, texture atlases, pooled VFX. |

## 7. Оценка объёма (грубо)

| Этап | Оценка |
|---|---:|
| Stage 1 | 1-2 дня |
| Stage 2 | 2-4 дня |
| Stage 3 | 2-3 дня |
| Stage 4 | 3-5 дней |
| Stage 5 | 1-2 дня |
| Stage 6 | 2-4 дня |
| Stage 7 | 2-4 дня |
| Stage 8 | 1-2 дня |

Итого: ~14-26 рабочих дней, зависит от наличия 3D assets и доступа к Unity Editor/device testing.
