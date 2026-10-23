<#
.SYNOPSIS
    Сборка релизных дистрибутивов SmetaNA (win-x64 / linux-x64).

.DESCRIPTION
    Публикует SmetaNA.EditorUI self-contained (без предустановленного .NET —
    минимальная конфигурация школьных ПК, см. docs/release.md), раскладывает
    нативные зависимости (Manifold, lib3mf) по NuGet-схеме runtimes/<rid>/native/
    и собирает «срез» дистрибутива: приложение + юридические документы +
    SHA256SUMS.

    Нативные бинарники в git не коммитятся и воспроизводятся отдельно:
      * Manifold 3.5.4 — spikes\Spike.Manifold\build-native.ps1 +
        src\SmetaNA.Manifold\fetch-native.ps1 (результат — src\SmetaNA.Manifold\runtimes\<rid>\native\);
      * lib3mf 2.5.0 — src\SmetaNA.Formats\ThreeMf\native\fetch-lib3mf.ps1
        (результат — src\SmetaNA.Formats\ThreeMf\native\<rid>\).

    Выход: artifacts\publish\<rid>\ — готовый слой для установщиков
    (deploy\windows\build-installer.ps1, deploy\linux\build-deb.sh).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\publish.ps1
    powershell -ExecutionPolicy Bypass -File deploy\publish.ps1 -Rid win-x64 -Version 1.0.0
    powershell -ExecutionPolicy Bypass -File deploy\publish.ps1 -DryRun
#>
[CmdletBinding()]
param(
    # Целевые платформы.
    [ValidateSet("win-x64", "linux-x64", "all")]
    [string]$Rid = "all",

    # Версия продукта (по умолчанию — из Directory.Build.props).
    [string]$Version = "",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    # Каталог выходных срезов (в .gitignore — артефакты не коммитятся).
    [string]$OutputRoot = "",

    # Отключить ReadyToRun (по умолчанию он включён ради холодного старт ≤ 5 с, DoD §5.1).
    [switch]$NoReadyToRun,

    # Строгий режим: отсутствие нативных библиотек — ошибка, а не предупреждение.
    [switch]$RequireNatives,

    # Печатать команды без выполнения.
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
if ($OutputRoot -eq "") { $OutputRoot = Join-Path $repoRoot "artifacts\publish" }

if ($Version -eq "") {
    $propsText = Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw
    if ($propsText -match "<Version>([^<]+)</Version>") { $Version = $Matches[1] }
    else { $Version = "0.0.0-dev" }
}

$dotnet = if ($env:SMETANA_DOTNET) { $env:SMETANA_DOTNET } else {
    $local = "C:\Users\mnk\.dotnet\dotnet.exe"
    if (Test-Path $local) { $local } else { "dotnet" }
}

$targets = if ($Rid -eq "all") { @("win-x64", "linux-x64") } else { @($Rid) }
$readyToRun = -not $NoReadyToRun

Write-Host "SmetaNA publish: version=$Version configuration=$Configuration rids=$($targets -join ',') readyToRun=$readyToRun"
Write-Host "dotnet: $dotnet"

function Invoke-Step {
    param([string[]]$CommandLine)
    if ($DryRun) {
        Write-Host "[dry-run] $($CommandLine -join ' ')" -ForegroundColor Yellow
    } else {
        Write-Host "> $($CommandLine -join ' ')"
        & $CommandLine[0] $CommandLine[1..($CommandLine.Count - 1)]
        if ($LASTEXITCODE -ne 0) { throw "Команда завершилась с кодом $LASTEXITCODE : $($CommandLine -join ' ')" }
    }
}

# Нативные зависимости: откуда берём (раскладка runtimes/<rid>/native + корень выходного каталога).
function Get-NativePlan {
    param([string]$TargetRid)

    $manifoldDir = Join-Path $repoRoot "src\SmetaNA.Manifold\runtimes\$TargetRid\native"
    $lib3mfDir = Join-Path $repoRoot "src\SmetaNA.Formats\ThreeMf\native\$TargetRid"

    $plan = @()
    if ($TargetRid -eq "win-x64") {
        foreach ($f in @("manifoldc.dll", "manifold.dll")) { $plan += @{ File = $f; Source = (Join-Path $manifoldDir $f) } }
        $plan += @{ File = "lib3mf.dll"; Source = (Join-Path $lib3mfDir "lib3mf.dll") }
    } else {
        foreach ($f in @("libmanifoldc.so", "libmanifold.so")) { $plan += @{ File = $f; Source = (Join-Path $manifoldDir $f) } }
        $plan += @{ File = "lib3mf.so"; Source = (Join-Path $lib3mfDir "lib3mf.so") }
    }
    return $plan
}

$missing = New-Object System.Collections.Generic.List[string]

foreach ($target in $targets) {
    $outDir = Join-Path $OutputRoot $target
    Write-Host ""
    Write-Host "=== $target -> $outDir ===" -ForegroundColor Cyan

    $publishArgs = @(
        "publish", (Join-Path $repoRoot "src\SmetaNA.EditorUI\SmetaNA.EditorUI.csproj"),
        "-c", $Configuration,
        "-r", $target,
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:PublishReadyToRun=$(if ($readyToRun) { 'true' } else { 'false' })",
        "-p:Version=$Version",
        "-o", $outDir,
        "-v", "minimal"
    )
    Invoke-Step (@($dotnet) + $publishArgs)

    # Нативные библиотеки: корень (DllImport("manifoldc") и резолвер lib3mf) и runtimes/<rid>/native.
    $nativeRoot = Join-Path $outDir "runtimes\$target\native"
    foreach ($item in Get-NativePlan -TargetRid $target) {
        if (Test-Path $item.Source) {
            if ($DryRun) {
                Write-Host "[dry-run] нативный: $($item.File) -> корень и runtimes\$target\native" -ForegroundColor Yellow
            } else {
                foreach ($dest in @($outDir, $nativeRoot)) {
                    New-Item -ItemType Directory -Force -Path $dest | Out-Null
                    Copy-Item $item.Source $dest -Force
                }
                Write-Host "нативный: $($item.File) -> корень и runtimes\$target\native"
            }
        } else {
            $missing.Add("$target : $($item.Source)")
            Write-Warning "Не найден нативный файл: $($item.Source). Восстановление — см. docs/release.md."
        }
    }

    if (-not $DryRun) {
        # Юридические документы и уведомления сторонних лицензий — в пакет.
        $legalDir = Join-Path $outDir "docs\legal"
        New-Item -ItemType Directory -Force -Path $legalDir | Out-Null
        Copy-Item (Join-Path $repoRoot "docs\legal\*.md") $legalDir -Force

        # SHA256 по файлам среза (контроль целостности при публикации и в установщике).
        $outRoot = $outDir.TrimEnd("\", "/")
        Get-ChildItem $outRoot -Recurse -File |
            Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
            ForEach-Object {
                $rel = $_.FullName.Substring($outRoot.Length + 1) -replace "\\", "/"
                $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$hash  $rel"
            } |
            Sort-Object |
            Set-Content -Encoding ascii (Join-Path $outRoot "SHA256SUMS.txt")

        $files = Get-ChildItem $outRoot -Recurse -File
        $sizeMb = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
        Write-Host "Срез $target : $($files.Count) файлов, $sizeMb МБ"
    }
}

Write-Host ""
if ($missing.Count -gt 0) {
    Write-Host "Отсутствуют нативные библиотеки ($($missing.Count)):" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    if ($RequireNatives) { throw "Строгий режим -RequireNatives: пакет без нативных библиотек неприемлем." }
    Write-Host "Продолжено без них (для строгого режима — -RequireNatives). Порядок восстановления: docs/release.md." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Готово. Срезы: $OutputRoot (артефакты в git не коммитятся)."
Write-Host "Дальше: deploy\new-sbom.ps1, deploy\verify-offline.ps1, deploy\windows\build-installer.ps1, deploy\linux\build-deb.sh."
