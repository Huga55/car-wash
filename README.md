# Little Car Workshop — STEP 2 vertical slice (STEP 3 validation status)

Прототип/пересборка **Little Car Wash → Little Car Workshop** под Unity **2022.3.61f1**, Built-in Render Pipeline. Текущая цель — **один аудиальный/визуальный vertical slice**: одна процедурная 3D-машина (Sedan), один гараж и полный шестиэтапный цикл мойки с финальной сценой.

Новая игра живёт только в `Assets/LittleGarage/*`. Legacy `Assets/Scripts/*` оставлен в репозитории как история и **не участвует в старте**: единственная сцена `Assets/Scenes/Game.unity` содержит только `LittleGarage.Bootstrap` (+ камеру), который создаёт `GameManager` и все системы.

## Статус проверки (5 категорий, STEP 3 §9)

| Категория | Что относится |
|---|---|
| **1. Реализовано в коде** | Все модули `Assets/LittleGarage/*`, сцена Bootstrap, data-driven каталог, шестиэтапный цикл мойки, финал 7+ секунд, save/load, процедурные гараж/машина/аудио, русский UI, build/CI scaffolding, EditMode/PlayMode тесты. |
| **2. Проверено offline (без Unity)** | C# compile-check runtime + тестов против Unity-stubs; поведение цикла мойки, save/load, UV-покрытия, tool lock, auto-finish; статическая проверка GUID сцены, EditorBuildSettings, meta/GUID, PowerShell/YAML. |
| **3. Проверено в Unity Editor** | **Частично.** Пользователь запустил Unity 2022.3.61f1: сгенерированы `ProjectSettings/*.asset`, получено 13 `CS1069` (не хватало built-in модулей `ParticleSystem`/`Physics`); исправлено в `Packages/manifest.json`, восстановлен scene list. Повторной компиляции не было — успех **не подтверждён**. Editor tests, Play Mode и скриншоты не выполнялись. |
| **4. Проверено в Windows build** | **Нет.** EXE не был собран. |
| **5. Всё ещё не проверено / блокировано** | Повторный import/compile после fix, shader import, Editor test suite, Play Mode + 6 скриншотов, Windows `.exe`, Android, визуальное качество и перф. |

### Ключевые блокеры STEP 3 (не придумываются workaround-ы)

- `ProjectSettings/ProjectSettings.asset` **был восстановлен пользователем через Unity** (реальный Editor output, не ручная фабрикация). Проверено: версия `2022.3.61f1 (6c53ebaf375d)`, `activeInputHandler: 0` (legacy Input), Built-in RP, ось `Mouse ScrollWheel` есть. Unity перезаписал `EditorBuildSettings.asset` в `m_Scenes: []` — запись `Game.unity` возвращена вручную. Подробности: [REPORT.md §2](REPORT.md).
- **Unity 2022.3.61f1 недоступен в sandbox** (нет бинарника/пакетов/env/образа, нет license secrets), поэтому повторный import/compile/tests/Play Mode/build здесь невозможны. Первый compile пользователя выявил 13 `CS1069` (см. [REPORT.md §4](REPORT.md)); в `Packages/manifest.json` добавлены `particlesystem`, `physics`, `screencapture` — исправление проверено только статически (JSON + offline stubs).
- Краткая процедура установки и точный ручной чеклист: [REPORT.md](REPORT.md) и [PLAYMODE_VERIFICATION_CHECKLIST.md](Documentation/Rebuild/PLAYMODE_VERIFICATION_CHECKLIST.md).
- 8 референс-скриншотов Google Play (`/tmp/opencode/ref/shot1.png`…`shot8.png`) не были визуально проанализированы; используются только текстовые факты.
- Процедурные 3D-модель машины, гараж, персонажи финала и звук — **functional placeholders**, не финальный art/audio.

## Что реализовано в коде (STEP 2 slice)

- **Bootstrap → GameManager**: `Assets/Scenes/Game.unity` ссылается на `LittleGarage.Bootstrap` GUID `9e3b1c5f4a8d2c7b6e5f4a3b2c1d0e9f`. `GameConfig.CreateDefault()` — fallback-каталог, если ассеты не авторизованы.
- **Data-driven каталог** (`Core/Definitions.cs`): машины, задачи, инструменты, цвета/диски/декали. Первая задача — ровно шесть seq-этапов: Pressure → Foam → Sponge → Rinse → Dryer → Polisher.
- **Реальное состояние поверхности**: `SurfaceStateSystem`, 256×256 RGBA state-map (R=грязь, G=влага, B=пена, A=полировка), шейдер `CarSurface.shader`, UV-атлас полностью покрывается панелями (256/256 в offline-тесте).
- **Ввод**: единый `InputRouter` (mouse + touch + scroll), UI блокирует pointer от raycast, sequential tool lock (нельзя пропустить этап).
- **Финал 7+ секунд**: gloss sweep, появление водителя и животного (процедурные placeholder-ы), «Спасибо!», уезд машины, затем result screen. Коррутина останавливается при restart/home/destroy.
- **Russian UI**: Home/Garage/Play/Pause/Complete/Settings/Customization; подсказки и названия инструментов на русском.
- **Audio**: процедурные music/SFX bus-ы (Music/Washing/UI/Characters/Ambient), loop инструментов, `PlayerPrefs` громкости; звуки не являются отдельными ассетами — генерируются в памяти (placeholder).
- **Save/load**: `ProgressManager` + `SaveSystem` (версионированный JSON, fallback на corrupt data), stars, completed jobs, appearance.
- **Build/CI**: `Assets/Editor/BuildScript.cs` (Windows/Android entry points, строгая проверка версии Unity и сцены), `build-windows.ps1`, `BUILD_WINDOWS.md`, `.github/workflows/build.yml` (EditMode+PlayMode test gate, Windows artifact `Builds/Windows/LittleCarWorkshop.exe`).

## Основные файлы

| Путь | Назначение |
|---|---|
| `ARCHITECTURE.md` | Контракт модулей и публичных API (обновлён под slice). |
| `docs/REFERENCE_SPEC.md`, `docs/PROJECT_AUDIT.md` | Факты референса и аудит legacy (дубли в `Documentation/Rebuild/`). |
| `Assets/Scenes/Game.unity` | Единственная сцена: Bootstrap + Camera. |
| `Assets/LittleGarage/Bootstrap.cs`, `GameManager.cs` | Точка входа и оркестрация. |
| `Assets/LittleGarage/Core/Definitions.cs` | Data-driven каталог (10 записей для будущего контента; slice — job01/sedan). |
| `Assets/LittleGarage/Systems/*` | Surface/Washing/Repair/Customization/Input/Tool/Progress/Level/Save. |
| `Assets/LittleGarage/Vehicles/*`, `Garage/*`, `Effects/*` | Процедурная 3D-машина, гараж, частицы. |
| `Assets/LittleGarage/UI/UIManager.cs`, `Audio/AudioManager.cs` | Runtime UI и процедурное аудио. |
| `Assets/LittleGarage/Tests/*` | EditMode (включая `WashCycleTests`) и PlayMode smoke. |
| `Assets/Editor/BuildScript.cs`, `build-windows.ps1`, `BUILD_WINDOWS.md` | Windows-сборка без Hub. |

## Offline-проверки, выполненные в sandbox

Compile-check runtime (Unity-stubs, **не заменяет Unity import/Play Mode**):

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /tmp/opencode/dotnet/dotnet build /tmp/opencode/compilecheck/CompileCheck.csproj -v q
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /tmp/opencode/dotnet/dotnet run --project /tmp/opencode/compilecheck/CompileCheck.csproj --no-build
```

Compile-check тестов (NUnit/Unity-stubs):

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /tmp/opencode/dotnet/dotnet build /tmp/opencode/compilecheck/TestsCompileCheck.csproj -v q
```

Результаты STEP 2 (не перезапускались в STEP 3, чтобы не подменять Unity-тесты): runtime build — **0 Warning(s), 0 Error(s)**; behavior checks — **ALL OFFLINE CHECKS PASSED**; tests build — **0 Warning(s), 0 Error(s)**. Stubs находятся вне репозитория (`/tmp/opencode/compilecheck`). **STEP 3 §5 прямо запрещает считать это заменой Unity Editor tests** — результаты ниже по-прежнему «не проверено в Unity».

## Windows-сборка (только на машине с Unity)

См. [BUILD_WINDOWS.md](BUILD_WINDOWS.md). Ожидаемый артефакт: `Builds/Windows/LittleCarWorkshop.exe`. Скрипты **строго требуют** `2022.3.61f1`; несовпадение версии = failed build (не silent fallback).

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
```

## Что осталось вне этой среды

1. **Сейчас у пользователя:** перекомпилировать после исправления `Packages/manifest.json` и прислать новый Editor log (13 `CS1069` должны исчезнуть).
2. Подтвердить в `File → Build Settings`, что `Assets/Scenes/Game.unity` одна и включена (запись восстановлена в `EditorBuildSettings.asset`).
3. Проверить импорт/компиляцию, убедиться, что Console без ошибок.
4. Запустить Unity Test Runner (EditMode+PlayMode) и сохранить результаты.
5. Пройти [PLAYMODE_VERIFICATION_CHECKLIST.md](Documentation/Rebuild/PLAYMODE_VERIFICATION_CHECKLIST.md) и получить 6 скриншотов.
6. Подтвердить модуль Windows Build Support + license, собрать Windows `.exe`, проверить EXE + data-папку, Player.log, звук/ввод.
7. Закоммитить сгенерированные `ProjectSettings/*.asset` отдельным изменением (сейчас untracked; коммит не выполнялся).
8. Лишь после этого обновить статусы 3/4 в таблице выше фактическими результатами.
