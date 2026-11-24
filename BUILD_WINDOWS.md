# Сборка Windows-версии Little Car Workshop (локально)

Эта инструкция описывает локальную сборку Windows `.exe` **без Unity Hub** — напрямую через установленный Unity Editor.

## 1. Требования

- Установленный **Unity Editor 2022.3.61f1** (версия берётся из `ProjectSettings/ProjectVersion.txt`).
- Установленный модуль **Windows Build Support** для этой же версии Unity:
  - `Windows Build Support (Mono)` или
  - `Windows Build Support (IL2CPP)`.
- Windows PowerShell 5.1+ или PowerShell 7+.
- Для Windows-сборки **не нужен Android SDK/NDK**.

## 2. Быстрый запуск

Из корня репозитория:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
```

Скрипт:
- читает требуемую версию Unity из `ProjectSettings/ProjectVersion.txt`;
- ищет Unity Editor в типичных путях установки;
- проверяет наличие модуля Windows Build Support;
- запускает Unity в batchmode с `-executeMethod BuildScript.BuildWindows`;
- сохраняет лог и возвращает корректный код выхода.

## 3. Явный путь к Unity / переменная окружения

Если Unity установлен в нестандартном месте, задайте путь явно.

### Параметр PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe"
```

### Переменная окружения

```powershell
$env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe"
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
```

## 4. Дополнительные параметры

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1 `
  -UnityPath "C:\...\Unity.exe" `
  -ProjectPath "E:\my\wash-cars" `
  -OutputDir "Builds/Windows" `
  -LogFile "build/logs/unity-batch-build.log"
```

- `-ProjectPath` — путь к проекту (по умолчанию текущая папка).
- `-OutputDir` — папка результата.
- `-LogFile` — лог Unity batchmode.

## 5. Ожидаемый результат

После успешной сборки:

- Исполняемый файл: `Builds/Windows/LittleCarWorkshop.exe`
- Лог сборки Unity batchmode: `build/logs/unity-batch-build.log`
- Детальный лог BuildScript: `build/logs/windows-build.log`

## 6. Ручной запуск через Unity CLI (без скрипта)

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe" `
  -quit -batchmode -nographics `
  -projectPath "E:\my\wash-cars" `
  -executeMethod BuildScript.BuildWindows `
  -buildOutputDir "Builds/Windows" `
  -logFile "build/logs/unity-batch-build.log"
```

## 7. Сборка из Unity Editor

В меню Unity:

`LittleGarage/Build Windows`

## 8. Устранение проблем

### Unity не найден

Ошибка: `Unity Editor was not found...`

Решение:
- установите Unity 2022.3.61f1;
- передайте `-UnityPath` или задайте `UNITY_PATH`.

### Отсутствует Windows Build Support

Ошибка/сообщение: `Windows Build Support module appears missing...`

Решение:
- установите модуль Windows Build Support для **той же версии** Unity 2022.3.61f1;
- повторите сборку.

### Нет включённых сцен

Ошибка: `No enabled scenes found in EditorBuildSettings...`

Решение:
- откройте `File > Build Settings`;
- добавьте `Assets/Scenes/Game.unity` и включите её.

### Ошибка лицензии Unity

Симптомы: в логе есть сообщения о лицензии/активации.

Решение:
- убедитесь, что Unity Editor активирован на этой машине;
- запустите Unity GUI один раз и войдите/активируйте лицензию;
- повторите batch-сборку.

### Сборка завершилась, но .exe не найден

- проверьте `build/logs/unity-batch-build.log` и `build/logs/windows-build.log`;
- убедитесь, что вывод не был перенаправлен через `-customBuildPath`;
- проверьте права на запись в `Builds/Windows`.

## 9. Примечания

- BuildScript автоматически берёт включённые сцены из Build Settings.
- Windows-сборка независима от Unity Hub после установки нужного Editor+модуля.
- Android-сборка — отдельный процесс/команда (`BuildScript.BuildAndroid`) и требует Android-модулей.