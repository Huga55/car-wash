# WINDOWS_BUILD_GUIDE

Дата: 2026-11-24  
Цель: подготовить локальную Windows-сборку Unity-проекта **без Unity Hub**. На этом этапе сборка не запускалась; Unity Editor в sandbox не установлен.

## 1. Что проверено в проекте

| Проверка | Результат | Источник |
|---|---|---|
| Unity version | 2022.3.61f1 | `ProjectSettings/ProjectVersion.txt` |
| Build script | Есть `Assets/Editor/BuildScript.cs` | файл присутствует |
| Menu/CLI entry | `LittleGarage/Build Windows`, `BuildScript.BuildWindows` | `BuildScript.cs` |
| Windows output | `Builds/Windows/LittleCarWorkshop.exe` по умолчанию | `BuildScript.cs`, `build-windows.ps1` |
| Build logs | `build/logs/windows-build.log`, `build/logs/unity-batch-build.log` | `BuildScript.cs`, `build-windows.ps1` |
| Scenes | Только `Assets/Scenes/Game.unity` enabled | `EditorBuildSettings.asset` |
| CI | GameCI Windows build есть, но verify привязан к legacy VisualVerification | `.github/workflows/build.yml` |
| Unity Hub required | Нет, можно вызывать Unity.exe напрямую | batchmode CLI |

## 2. Точная версия редактора

Нужен **Unity 2022.3.61f1** с модулем **Windows Build Support (IL2CPP или Mono)**. Для Windows-сборки Android modules **не нужны**.

## 3. Минимальная установка без Unity Hub

> Конкретный установщик Unity зависит от лицензии/аккаунта и доступного offline installer. Ниже — технически безопасная последовательность; команды скачивания не выполнялись в sandbox.

1. Скачать Unity Editor **2022.3.61f1** installer/offline installer из официального Unity release archive.
2. Установить Editor в путь вида `C:\Program Files\Unity\Editor\2022.3.61f1\Editor\Unity.exe` или стандартный Unity Hub editor path.
3. Установить модуль **Windows Build Support** для той же версии. Проверить наличие:
   - `...\Editor\Data\PlaybackEngines\WindowsStandaloneSupport\`
4. Активировать лицензию Unity на машине (GUI login один раз, либо существующая license/serial по политике команды).
5. Не устанавливать Android Build Support/SDK/NDK/OpenJDK для Windows-only сборки.

## 4. Проверка готовности

```powershell
Test-Path "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe"
Test-Path "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Data\PlaybackEngines\WindowsStandaloneSupport"
```

Проверить версию проекта:

```powershell
Get-Content .\ProjectSettings\ProjectVersion.txt
```

Ожидается `m_EditorVersion: 2022.3.61f1`.

## 5. Команда сборки (PowerShell script)

Из корня репозитория:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe" `
  -ProjectPath "E:\my\wash-cars" `
  -OutputDir "Builds/Windows" `
  -LogFile "build/logs/unity-batch-build.log"
```

Script делает:
- поиск Unity.exe (или берёт `-UnityPath` / `$env:UNITY_PATH`);
- проверку WindowsStandaloneSupport;
- запуск Unity batchmode;
- проверку наличия `.exe` в output.

## 6. Прямая Unity CLI команда

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe" `
  -quit -batchmode -nographics `
  -projectPath "E:\my\wash-cars" `
  -executeMethod BuildScript.BuildWindows `
  -buildOutputDir "Builds/Windows" `
  -logFile "build/logs/unity-batch-build.log"
```

## 7. Ожидаемые артефакты

| Артефакт | Путь |
|---|---|
| Windows executable | `Builds/Windows/LittleCarWorkshop.exe` |
| Unity batch log | `build/logs/unity-batch-build.log` |
| BuildScript detailed log | `build/logs/windows-build.log` |

## 8. Типовые проблемы

| Симптом | Причина | Что делать |
|---|---|---|
| `Unity Editor was not found` | Неверный путь/нет Editor | Указать `-UnityPath` или установить 2022.3.61f1. |
| `Windows Build Support module appears missing` | Нет модуля WindowsStandaloneSupport | Установить Windows Build Support для 2022.3.61f1. |
| `No enabled scenes found` | В Build Settings нет сцен | Включить `Assets/Scenes/Game.unity`. |
| License error в логе | Не активирована лицензия Unity | Активировать Unity на машине, повторить. |
| Build failed, exe not found | Ошибка компиляции/ассетов/настроек | Смотреть `build/logs/unity-batch-build.log` и `build/logs/windows-build.log`. |
| CI verify fails on legacy VisualVerification | Старый verification сценарий | Обновить workflow/verification под новый gameplay перед релизом. |

## 9. Ограничения текущего состояния

- Сборка **не выполнена** в этой среде (нет Unity Editor).
- ProjectSettings.asset отсутствует, поэтому воспроизводимость player settings не полностью подтверждена.
- В репозитории есть параллельный legacy/LittleGarage код; перед релизной сборкой нужно зафиксировать один entry point и проверить compile.
