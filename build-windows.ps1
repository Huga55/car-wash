param(
    [string]$UnityPath = $env:UNITY_PATH,
    [string]$ProjectPath = (Get-Location).Path,
    [string]$OutputDir = "Builds/Windows",
    [string]$LogFile = "build/logs/unity-batch-build.log"
)

$ErrorActionPreference = "Stop"

# This build script deliberately supports one exact editor version.
$RequiredUnityVersion = "2022.3.61f1"

function Write-Step([string]$Message) {
    Write-Host "[build-windows] $Message" -ForegroundColor Cyan
}

function Write-Fail([string]$Message) {
    Write-Host "[build-windows] ERROR: $Message" -ForegroundColor Red
}

function Read-RequiredUnityVersion {
    $versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Cannot determine the required Unity version: '$versionFile' is missing. Expected Unity $RequiredUnityVersion."
    }

    try {
        $lines = @(Get-Content -LiteralPath $versionFile -ErrorAction Stop)
    }
    catch {
        throw "Cannot read '$versionFile': $($_.Exception.Message)"
    }

    $versionLine = @($lines | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1)
    if ($versionLine.Count -eq 0) {
        throw "'$versionFile' has no m_EditorVersion entry. Cannot verify the Unity version."
    }

    $projectVersion = ($versionLine[0] -replace '^m_EditorVersion:\s*', '').Trim()
    if ([string]::IsNullOrWhiteSpace($projectVersion)) {
        throw "'$versionFile' contains an empty m_EditorVersion value."
    }
    if ($projectVersion -ne $RequiredUnityVersion) {
        throw "Project requires Unity $projectVersion, but this build script only supports Unity $RequiredUnityVersion. Refusing to build with an unverified editor."
    }

    return $projectVersion
}

function Get-UnityExeVersion {
    param([string]$Exe)

    $previousPreference = $ErrorActionPreference
    try {
        # Unity may print to stderr; do not let native stderr output terminate
        # the version probe while the global preference is "Stop".
        $ErrorActionPreference = "Continue"
        $output = (& $Exe -version 2>&1 | Out-String)
    }
    catch {
        throw "Failed to query '$Exe -version': $($_.Exception.Message)"
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    $match = [regex]::Match($output, '\b\d{4}\.\d+\.\d+[a-z]+\d+\b',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        throw "Could not parse a Unity version from '$Exe -version'. Output: $($output.Trim())"
    }
    return $match.Value
}

function Resolve-UnityPath {
    param(
        [string]$ExplicitPath,
        [string]$RequiredVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $exe = $ExplicitPath
        if (Test-Path -LiteralPath $exe -PathType Container) {
            $nested = Join-Path $exe "Unity.exe"
            if (-not (Test-Path -LiteralPath $nested -PathType Leaf)) {
                throw "Explicit Unity path is a directory without Unity.exe: $exe"
            }
            $exe = $nested
        }
        if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
            throw "Unity executable not found at UNITY_PATH/parameter: $exe"
        }

        $exe = (Resolve-Path -LiteralPath $exe).Path
        $foundVersion = Get-UnityExeVersion -Exe $exe
        if ($foundVersion -ne $RequiredVersion) {
            throw "Unity version mismatch at '$exe': found $foundVersion, required $RequiredVersion. Refusing to build with the wrong editor."
        }
        Write-Step "Verified Unity executable version: $foundVersion"
        return $exe
    }

    # Automatic discovery: only editors installed for the exact required version.
    $hubRoots = @(
        "C:\Program Files\Unity\Hub\Editor",
        "C:\Program Files\Unity\Editor",
        "D:\Unity\Hub\Editor",
        "D:\Unity\Editor"
    )

    $candidates = @()
    foreach ($root in $hubRoots) {
        $candidates += (Join-Path $root "$RequiredVersion\Editor\Unity.exe")
        if (Test-Path -LiteralPath $root -PathType Container) {
            $versionDirs = @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -eq $RequiredVersion })
            foreach ($dir in $versionDirs) {
                $candidates += (Join-Path $dir.FullName "Editor\Unity.exe")
            }
        }
    }

    # Legacy flat installs are still usable, but must pass the same version check.
    $candidates += @(
        "C:\Program Files\Unity\Editor\Unity.exe",
        "C:\Program Files (x86)\Unity\Editor\Unity.exe"
    )

    $candidates = @($candidates | Where-Object {
        $_ -and (Test-Path -LiteralPath $_ -PathType Leaf)
    } | Select-Object -Unique)

    if ($candidates.Count -eq 0) {
        throw "Unity $RequiredVersion was not found in the standard installation locations. Install that exact version or pass -UnityPath (UNITY_PATH) pointing to its Unity.exe."
    }

    $checked = @()
    foreach ($candidate in $candidates) {
        try {
            $foundVersion = Get-UnityExeVersion -Exe $candidate
        }
        catch {
            $checked += "$candidate (unreadable: $($_.Exception.Message))"
            continue
        }
        if ($foundVersion -eq $RequiredVersion) {
            Write-Step "Verified Unity executable version: $foundVersion"
            return $candidate
        }
        $checked += "$candidate (found $foundVersion)"
    }

    throw "Unity $RequiredVersion was not found. Checked: $($checked -join '; '). Pass -UnityPath (UNITY_PATH) pointing to a Unity $RequiredVersion Unity.exe."
}

try {
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
        throw "Project path not found: $ProjectPath"
    }
    $ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $LogFile = if ([System.IO.Path]::IsPathRooted($LogFile)) { $LogFile } else { Join-Path $ProjectPath $LogFile }
    $OutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $ProjectPath $OutputDir }
    $logDir = Split-Path -Parent $LogFile
    if ($logDir -and -not (Test-Path -LiteralPath $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    $requiredVersion = Read-RequiredUnityVersion

    Write-Step "Project: $ProjectPath"
    Write-Step "Required Unity version: $requiredVersion"

    $unityExe = Resolve-UnityPath -ExplicitPath $UnityPath -RequiredVersion $requiredVersion
    Write-Step "Unity executable: $unityExe"

    # Validate Windows build support module presence near editor.
    $editorDir = Split-Path -Parent $unityExe
    $playbackEngines = Join-Path $editorDir "Data\PlaybackEngines\windowsstandalonesupport"
    $playbackEnginesAlt = Join-Path $editorDir "Data\PlaybackEngines\WindowsStandaloneSupport"
    if (-not (Test-Path -LiteralPath $playbackEngines) -and -not (Test-Path -LiteralPath $playbackEnginesAlt)) {
        Write-Fail "Windows Build Support module appears missing for this Unity installation."
        Write-Host "Install 'Windows Build Support (Mono/IL2CPP)' for Unity $requiredVersion and rerun." -ForegroundColor Yellow
        Write-Host "Checked: $playbackEngines" -ForegroundColor Yellow
        exit 2
    }

    $unityArgs = @(
        "-quit",
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-executeMethod", "BuildScript.BuildWindows",
        "-buildOutputDir", $OutputDir,
        "-logFile", $LogFile
    )

    Write-Step "Starting Unity batch build..."
    Write-Step "Log file: $LogFile"

    $process = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    $exitCode = $process.ExitCode

    if ($exitCode -ne 0) {
        Write-Fail "Unity exited with code $exitCode. See log: $LogFile"
        if (Test-Path -LiteralPath $LogFile) {
            Write-Host "--- last log lines ---" -ForegroundColor DarkYellow
            Get-Content -LiteralPath $LogFile -Tail 60 | ForEach-Object { Write-Host $_ }
        }
        exit $exitCode
    }

    $exePath = Join-Path $OutputDir "LittleCarWorkshop.exe"
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        Write-Fail "Build completed but '$exePath' was not found as a file. See log: $LogFile"
        if (Test-Path -LiteralPath $exePath -PathType Container) {
            Write-Host "A directory exists at the executable path; remove it and rerun." -ForegroundColor Yellow
        }
        exit 3
    }

    Write-Step "Build succeeded."
    Write-Host "Executable: $exePath" -ForegroundColor Green
    Write-Host "Log: $LogFile" -ForegroundColor Green
    exit 0
}
catch {
    Write-Fail $_.Exception.Message
    exit 1
}
