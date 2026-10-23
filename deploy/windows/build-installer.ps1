<#
.SYNOPSIS
    Сборка установщика Windows (Inno Setup) из среза deploy\publish.ps1.

.DESCRIPTION
    1. Проверяет срез artifacts\publish\win-x64 (или собирает его через publish.ps1);
    2. Компилирует deploy\windows\smetana.iss через ISCC.exe;
    3. При настроенной подписи (см. deploy\sign.ps1) подписывает exe-файлы среза
       до компиляции и сам установщик после (ISCC получает /DSign и /Ssmetana=...);
    4. Печатает SHA256 установщика для публикации.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\windows\build-installer.ps1 -Version 1.0.0
    powershell -ExecutionPolicy Bypass -File deploy\windows\build-installer.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    # Путь к ISCC.exe (Inno Setup 6); по умолчанию — стандартная установка.
    [string]$IsccPath = "",
    # Использовать уже собранный срез без повторного publish.
    [switch]$SkipPublish,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$deployDir = Split-Path $PSScriptRoot -Parent
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
if ($PublishDir -eq "") { $PublishDir = Join-Path $repoRoot "artifacts\publish\win-x64" }
if ($OutputDir -eq "") { $OutputDir = Join-Path $repoRoot "artifacts\installers" }

if ($Version -eq "") {
    $propsText = Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw
    if ($propsText -match "<Version>([^<]+)</Version>") { $Version = $Matches[1] }
    else { $Version = "0.0.0-dev" }
}
$versionQuad = ($Version -split "-")[0] -replace "[^\d.]", ""
if ($versionQuad -notmatch "^\d+\.\d+\.\d+") { $versionQuad = "0.1.0" }
$versionQuad = "$versionQuad.0"

if ($IsccPath -eq "") {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6.3\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { $IsccPath = $c; break } }
}

function Invoke-Step {
    param([string[]]$CommandLine)
    if ($DryRun) {
        Write-Host "[dry-run] $($CommandLine -join ' ')" -ForegroundColor Yellow
    } else {
        Write-Host "> $($CommandLine -join ' ')"
        & $CommandLine[0] $CommandLine[1..($CommandLine.Count - 1)]
        if ($LASTEXITCODE -ne 0) { throw "Команда завершилась с кодом $LASTEXITCODE" }
    }
}

# 1. Срез приложения.
if (-not $SkipPublish -and -not (Test-Path $PublishDir)) {
    Write-Host "Срез $PublishDir не найден — запускаю publish.ps1..."
    Invoke-Step (@("powershell", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $deployDir "publish.ps1"), "-Rid", "win-x64", "-Version", $Version))
}
if (-not $DryRun -and -not (Test-Path $PublishDir)) { throw "Не найден срез приложения: $PublishDir" }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 2. Подпись (заготовка): при настроенных SMETANA_SIGN_* подписываем бинарники среза.
$signEnv = ($env:SMETANA_SIGN_THUMBPRINT -or $env:SMETANA_SIGN_PFX -or $env:SMETANA_SIGN_GPG_KEY)
if ($signEnv -and (Test-Path $PublishDir)) {
    Invoke-Step (@("powershell", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $deployDir "sign.ps1"), "-Platform", "windows", "-Path", $PublishDir))
} else {
    Write-Host "Код-сигнинг не настроен (env SMETANA_SIGN_*) — шаг пропущен, см. docs/release.md." -ForegroundColor Yellow
}

# 3. Компиляция Inno Setup.
if (-not $IsccPath -and -not $DryRun) {
    throw "ISCC.exe не найден. Установите Inno Setup 6 (https://jrsoftware.org/isinfo.php) или укажите -IsccPath."
}

$issPath = Join-Path $deployDir "windows\smetana.iss"
$isccArgs = @(
    "/DAppVersion=$Version",
    "/DAppVersionQuad=$versionQuad",
    "/DPublishDir=$PublishDir",
    "/O$OutputDir"
)
if ($signEnv) { $isccArgs += @("/DSign") }
$isccArgs += $issPath

if ($signEnv) {
    $signtoolHint = "signtool.exe sign /fd SHA256 /td SHA256 /tr %SMETANA_TIMESTAMP_URL% /sha1 %SMETANA_SIGN_THUMBPRINT% `$f"
    $isccArgs = @("/Ssmetana=$signtoolHint") + $isccArgs
}

Invoke-Step (@(if ($IsccPath) { $IsccPath } else { "ISCC.exe" }) + $isccArgs)

# 4. Контрольная сумма установщика.
$installer = Join-Path $OutputDir "SmetaNA-$Version-win-x64.exe"
if (-not $DryRun) {
    if (-not (Test-Path $installer)) { throw "Установщик не создан: $installer" }
    $hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  SmetaNA-$Version-win-x64.exe" | Set-Content -Encoding ascii (Join-Path $OutputDir "SHA256SUMS-win64.txt")
    Write-Host "Установщик: $installer" -ForegroundColor Green
    Write-Host "SHA256: $hash"
    Write-Host "Подпись установщика (если ещё не выполнена): deploy\sign.ps1 -Platform windows -Path $installer"
} else {
    Write-Host "[dry-run] ожидаемый результат: $installer + SHA256SUMS-win64.txt"
}
