<#
.SYNOPSIS
    Сборка пакетов обновления smetana для установленной программы (без переустановки).

.DESCRIPTION
    Готовит zip-пакеты, применяемые к уже установленной smetana заменой файлов
    (deploy\windows\apply-update.ps1) — без запуска полного установщика Inno Setup.

    Гибридная стратегия (docs/release.md §8.1):
      * полный пакет (собирается всегда, отключается -NoFull): весь набор файлов
        среза — универсальный фолбэк для любой установленной версии;
      * дельта-пакет (при -Baseline): только добавленные/изменённые файлы и список
        удалений относительно базовой версии — меньше размер, но применим только
        к базовой версии (сверяются версия и SHA256 заменяемых файлов до записи).

    Базовая версия (-Baseline) — старый срез artifacts\publish\<rid> или каталог
    установленной программы (раскладка та же). Служебные файлы окружения
    (unins*, crash-*.log, SHA256SUMS.txt) в дельту и список удалений не попадают.

    Состав пакета: update-manifest.json (состав и контрольные суммы, formatVersion 1),
    payload\ (файлы для замены), apply-update.ps1 + update.cmd (применение),
    SHA256SUMS.txt (целостность пакета), CHANGES.md (опц.). Контрольные суммы
    zip-пакетов — в SHA256SUMS-win64-updates.txt и <пакет>.zip.sha256.

    Пакет собирается из среза deploy\publish.ps1; для релиза срез подписывается
    ДО сборки пакета (порядок чек-листа docs/release.md §10).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\windows\build-update-package.ps1 -Version 1.1.0
    powershell -ExecutionPolicy Bypass -File deploy\windows\build-update-package.ps1 -Version 1.1.0 -Baseline artifacts\publish\win-x64
    powershell -ExecutionPolicy Bypass -File deploy\windows\build-update-package.ps1 -Version 1.1.0 -Baseline C:\Programs\smetana -BaselineVersion 1.0.0
#>
[CmdletBinding()]
param(
    # Целевая версия пакета (по умолчанию — из Directory.Build.props).
    [string]$Version = "",

    # Новый срез приложения.
    [string]$PublishDir = "",

    # Базовая версия: старый срез или каталог установленной программы (для дельты).
    [string]$Baseline = "",

    # Версия базовой версии (по умолчанию — из exe базового среза).
    [string]$BaselineVersion = "",

    # Не собирать полный пакет-фолбэк.
    [switch]$NoFull,

    # Каталог выходных пакетов (в .gitignore — артефакты не коммитятся).
    [string]$OutputDir = "",

    # Текст изменений — попадает в CHANGES.md.
    [string]$Changelog = "",

    # Файл с текстом изменений (альтернатива -Changelog).
    [string]$ChangelogFile = "",

    # Использовать уже собранный срез без повторного publish.
    [switch]$SkipPublish,

    # Печатать план без выполнения.
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$windowsDir = $PSScriptRoot
$deployDir = Split-Path $windowsDir -Parent
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
if ($PublishDir -eq "") { $PublishDir = Join-Path $repoRoot "artifacts\publish\win-x64" }
if ($OutputDir -eq "") { $OutputDir = Join-Path $repoRoot "artifacts\updates" }

$script:AppExeName = "SmetaNA.EditorUI.exe"
$script:Rid = "win-x64"

if ($Version -eq "") {
    $propsText = Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw
    if ($propsText -match "<Version>([^<]+)</Version>") { $Version = $Matches[1] }
    else { $Version = "0.0.0-dev" }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TargetPath([string]$Root, [string]$RelPath) {
    $rel = ($RelPath -replace "\\", "/").Replace('/', [IO.Path]::DirectorySeparatorChar)
    return Join-Path $Root $rel
}

# Индекс файлов среза: rel-путь (прямые слэши) -> хэш, размер, источник.
# Служебные файлы окружения (unins*, crash-*.log, SHA256SUMS.txt) исключаются:
# для базы-каталога установки они не являются частью продукта.
function Get-SliceIndex([string]$Dir) {
    $root = (Resolve-Path $Dir).Path
    $index = @{}
    Get-ChildItem $root -Recurse -File | ForEach-Object {
        $name = $_.Name
        if ($name -eq "SHA256SUMS.txt" -or $name -like "unins*" -or $name -like "crash-*.log") { return }
        $rel = $_.FullName.Substring($root.Length).TrimStart("\", "/") -replace "\\", "/"
        $index[$rel] = @{
            Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Size = $_.Length
            Source = $_.FullName
        }
    }
    return $index
}

function Get-SliceVersionString([string]$Dir) {
    $exe = Join-Path $Dir $script:AppExeName
    if (-not (Test-Path $exe)) { return "" }
    $vi = (Get-Item $exe).VersionInfo
    $v = $vi.ProductVersion
    if ([string]::IsNullOrWhiteSpace($v)) { $v = $vi.FileVersion }
    if ($v) { return (($v -split '\+')[0]).Trim() }
    return ""
}

function Get-SafeName([string]$Text) {
    return ($Text -replace "[^\w\.\-]", "_")
}

# Zip-сборка с повторами: антивирусы/синхронизация диска могут кратко держать
# свежескопированные файлы (локи на чтение), это не ошибка состава пакета.
# Записи создаются вручную с прямыми слэшами (спецификация zip): Compress-Archive
# пишет обратные слэши, и не все распаковщики это переживают.
function New-PackageZipWithRetry([string]$StagingPath, [string]$ZipPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    for ($i = 1; $i -le 5; $i++) {
        try {
            if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
            $root = (Resolve-Path $StagingPath).Path
            $zip = [IO.Compression.ZipFile]::Open($ZipPath, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($file in (Get-ChildItem $root -Recurse -File)) {
                    $rel = $file.FullName.Substring($root.Length).TrimStart("\", "/") -replace "\\", "/"
                    $entry = $zip.CreateEntry($rel, [IO.Compression.CompressionLevel]::Optimal)
                    $src = [IO.File]::OpenRead($file.FullName)
                    try {
                        $dst = $entry.Open()
                        try { $src.CopyTo($dst) } finally { $dst.Dispose() }
                    } finally {
                        $src.Dispose()
                    }
                }
            } finally {
                $zip.Dispose()
            }
            return
        } catch {
            if ($i -eq 5) { throw }
            Write-Warning "Не удалось собрать zip (попытка $i/5): $($_.Exception.Message). Повторяю..."
            Start-Sleep -Seconds 2
        }
    }
}

function New-UpdatePackage {
    param(
        [string]$Mode,               # "full" | "delta"
        [hashtable]$Index,
        [hashtable]$BaselineIndex,   # может быть $null
        [string]$BaselineVersionLabel, # "" если неизвестна
        [string]$WorkDir
    )

    # Состав пакета.
    $files = New-Object System.Collections.Generic.List[object]
    $delete = New-Object System.Collections.Generic.List[string]
    if ($Mode -eq "delta") {
        foreach ($rel in ($Index.Keys | Sort-Object)) {
            $new = $Index[$rel]
            if ($BaselineIndex.ContainsKey($rel)) {
                if ($BaselineIndex[$rel].Sha256 -ne $new.Sha256) {
                    $files.Add([pscustomobject][ordered]@{ path = $rel; sha256 = $new.Sha256; size = $new.Size; prevSha256 = $BaselineIndex[$rel].Sha256 })
                }
            } else {
                $files.Add([pscustomobject][ordered]@{ path = $rel; sha256 = $new.Sha256; size = $new.Size; prevSha256 = $null })
            }
        }
        foreach ($rel in ($BaselineIndex.Keys | Sort-Object)) {
            if (-not $Index.ContainsKey($rel)) { $delete.Add($rel) }
        }
    } else {
        foreach ($rel in ($Index.Keys | Sort-Object)) {
            $prev = $null
            if ($BaselineIndex -and $BaselineIndex.ContainsKey($rel)) { $prev = $BaselineIndex[$rel].Sha256 }
            $files.Add([pscustomobject][ordered]@{ path = $rel; sha256 = $Index[$rel].Sha256; size = $Index[$rel].Size; prevSha256 = $prev })
        }
    }

    # Имя пакета. Дельта привязана к базовой версии; полный применим к любой.
    $versionFrom = $null
    if ($Mode -eq "delta") {
        if ($BaselineVersionLabel -ne "") { $versionFrom = $BaselineVersionLabel }
        $fromName = if ($BaselineVersionLabel -ne "") { $BaselineVersionLabel } else { "unknown" }
        $zipName = "SmetaNA-$(Get-SafeName $Version)-$($script:Rid)-update-from-$(Get-SafeName $fromName).zip"
    } else {
        $zipName = "SmetaNA-$(Get-SafeName $Version)-$($script:Rid)-update-full.zip"
    }

    Write-Host ""
    Write-Host "=== Пакет $Mode : $zipName ===" -ForegroundColor Cyan
    Write-Host "Файлов в пакете: $($files.Count), удалений: $($delete.Count)."
    if ($Mode -eq "delta" -and $files.Count -eq 0 -and $delete.Count -eq 0) {
        Write-Warning "Дельта пуста: базовая версия уже совпадает с новой — обновлять нечего (пакет будет служебным)."
    }

    if ($DryRun) {
        foreach ($f in $files) { Write-Host "  [payload] $($f.path)" }
        foreach ($rel in $delete) { Write-Host "  [delete]  $rel" }
        return
    }

    # Сборка staging-каталога.
    $staging = Join-Path $WorkDir (Get-SafeName $Mode)
    New-Item -ItemType Directory -Force -Path (Join-Path $staging "payload") | Out-Null
    Copy-Item (Join-Path $windowsDir "apply-update.ps1") $staging -Force
    Copy-Item (Join-Path $windowsDir "update.cmd") $staging -Force

    foreach ($f in $files) {
        $dst = Get-TargetPath (Join-Path $staging "payload") $f.path
        New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
        Copy-Item $Index[$f.path].Source $dst -Force
    }

    $changelogText = ""
    if ($ChangelogFile -ne "") { $changelogText = Get-Content $ChangelogFile -Raw }
    elseif ($Changelog -ne "") { $changelogText = $Changelog }
    if ($changelogText -ne "") {
        Set-Content -Path (Join-Path $staging "CHANGES.md") -Value $changelogText -Encoding UTF8
    }

    $manifest = [ordered]@{
        formatVersion = 1
        product = "smetana"
        rid = $script:Rid
        mode = $Mode
        versionTo = $Version
        versionFrom = $versionFrom
        createdUtc = (Get-Date).ToUniversalTime().ToString("o")
        appExe = $script:AppExeName
        files = $files
        delete = $delete
    }
    $json = $manifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $staging "update-manifest.json"), $json, (New-Object System.Text.UTF8Encoding($false)))

    # SHA256SUMS самого пакета (все файлы staging, кроме этой суммы).
    $lines = Get-ChildItem $staging -Recurse -File |
        Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
        ForEach-Object {
            $rel = $_.FullName.Substring($staging.Length).TrimStart("\", "/") -replace "\\", "/"
            "$(Get-Sha256 $_.FullName)  $rel"
        }
    $lines | Sort-Object | Set-Content -Encoding ascii (Join-Path $staging "SHA256SUMS.txt")

    # Zip-пакет.
    $zipPath = Join-Path $OutputDir $zipName
    New-PackageZipWithRetry $staging $zipPath
    $hash = Get-Sha256 $zipPath
    "$hash  $zipName" | Set-Content -Encoding ascii "$zipPath.sha256"

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "Готово: $zipPath ($sizeMb МБ)" -ForegroundColor Green
    Write-Host "SHA256: $hash"
}

# 1. Срез приложения (как в build-installer.ps1).
if (-not $SkipPublish -and -not (Test-Path $PublishDir)) {
    Write-Host "Срез $PublishDir не найден — запускаю publish.ps1..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $deployDir "publish.ps1") -Rid $script:Rid -Version $Version
    if ($LASTEXITCODE -ne 0) { throw "publish.ps1 завершился с кодом $LASTEXITCODE" }
}
if (-not (Test-Path $PublishDir)) { throw "Не найден срез приложения: $PublishDir" }

# 2. Базовая версия (дельта).
$baselineIndex = $null
$baselineVersionLabel = ""
if ($Baseline -ne "") {
    if (-not (Test-Path $Baseline)) { throw "Базовая версия не найдена: $Baseline" }
    Write-Host "Индексирую базовую версию: $Baseline"
    $baselineIndex = Get-SliceIndex $Baseline
    $baselineVersionLabel = $BaselineVersion
    if ($baselineVersionLabel -eq "") { $baselineVersionLabel = Get-SliceVersionString $Baseline }
    Write-Host "Базовая версия: $(if ($baselineVersionLabel -ne '') { $baselineVersionLabel } else { 'неизвестна' }) (файлов: $($baselineIndex.Count))"
}

Write-Host "Индексирую новый срез: $PublishDir"
$index = Get-SliceIndex $PublishDir
Write-Host "Новая версия: $Version (файлов: $($index.Count))"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$workDir = Join-Path $OutputDir ".work"
if (-not $DryRun) {
    if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null
}

try {
    if ($Baseline -ne "") {
        New-UpdatePackage -Mode "delta" -Index $index -BaselineIndex $baselineIndex -BaselineVersionLabel $baselineVersionLabel -WorkDir $workDir
    }
    if (-not $NoFull) {
        New-UpdatePackage -Mode "full" -Index $index -BaselineIndex $baselineIndex -BaselineVersionLabel $baselineVersionLabel -WorkDir $workDir
    }
} finally {
    if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue }
}

if (-not $DryRun) {
    # Сводная контрольная сумма всех пакетов каталога.
    $sums = Get-ChildItem $OutputDir -Filter "*.zip" |
        ForEach-Object { "$(Get-Sha256 $_.FullName)  $($_.Name)" } |
        Sort-Object
    $sums | Set-Content -Encoding ascii (Join-Path $OutputDir "SHA256SUMS-win64-updates.txt")

    Write-Host ""
    Write-Host "Пакеты обновления: $OutputDir" -ForegroundColor Green
    Write-Host "Контрольные суммы: SHA256SUMS-win64-updates.txt"
    Write-Host "Применение: распаковать пакет и запустить update.cmd (или apply-update.ps1, см. docs/release.md §8.1)."
} else {
    Write-Host ""
    Write-Host "[dry-run] Пакеты не собраны."
}
