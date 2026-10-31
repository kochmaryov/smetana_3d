<#
.SYNOPSIS
    Применение пакета обновления smetana к установленной программе (без переустановки).

.DESCRIPTION
    Обновляет установленную smetana файлами из пакета, собранного
    deploy\windows\build-update-package.ps1, — без запуска полного установщика
    (без мастера, EULA и пересоздания ярлыков). Данные и настройки пользователя
    (%APPDATA%\SmetaNA) не затрагиваются.

    Порядок работы:
      1. Распаковка пакета (если указан .zip) и чтение update-manifest.json;
      2. Определение каталога установки: -InstallDir → запись Inno Setup в HKCU →
         {localappdata}\Programs\smetana;
      3. Предпроверки (ничего не изменяется): приложение не запущено; для
         дельта-пакета — установленная версия и SHA256 заменяемых файлов совпадают
         с базовой версией пакета (prevSha256);
      4. Резервная копия затрагиваемых файлов (включая удаляемые) —
         {localappdata}\Programs\smetana-update-backup\<дата>;
      5. Копирование payload\ с проверкой SHA256 каждого файла после записи,
         удаление файлов из delete (для mode=full — зеркальная синхронизация:
         файлы установки, отсутствующие в пакете, считаются устаревшими);
      6. Пересчёт SHA256SUMS.txt установки и обновление версии в записи
         «Установка и удаление программ» (HKCU);
      7. При сбое — автоматический откат из резервной копии.

    Ручной откат: apply-update.ps1 -Rollback <каталог резервной копии>.

    Коды выхода: 0 — успешно; 2 — предпроверка не пройдена (ничего не изменено);
    3 — сбой применения, выполнен откат; 1 — непредвиденная ошибка.

.EXAMPLE
    .\apply-update.ps1
    .\apply-update.ps1 -Package .\SmetaNA-1.1.0-win-x64-update-full.zip
    .\apply-update.ps1 -CloseApp -Package .\SmetaNA-1.1.0-win-x64-update-from-1.0.0.zip
    .\apply-update.ps1 -Rollback ..\smetana-update-backup\20261028-120000
#>
[CmdletBinding()]
param(
    # Пакет: каталог распакованного пакета или сам .zip (по умолчанию — каталог скрипта).
    [string]$Package = "",

    # Каталог установленной программы (по умолчанию — реестр или {localappdata}\Programs\smetana).
    [string]$InstallDir = "",

    # Куда складывать резервные копии (по умолчанию — рядом с каталогом установки).
    [string]$BackupRoot = "",

    # Сколько резервных копий хранить.
    [int]$KeepBackups = 3,

    # Разрешить закрытие запущенной smetana (иначе — отказ, пока приложение открыто).
    [switch]$CloseApp,

    # Применить несмотря на несовпадение версии/хэшей базовой версии.
    [switch]$Force,

    # Режим отката: восстановить файлы из указанной резервной копии.
    [string]$Rollback = "",

    # Проверки и план без изменения файлов.
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Запись деинсталляции Inno Setup (AppId из deploy\windows\smetana.iss):
# источник каталога установки и место обновления версии в
# «Установке и удалении программ».
$script:UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7E2B4C41-6D3A-4E2F-9B6C-5B1A9D2F3E41}_is1'
$script:AppExeName = "SmetaNA.EditorUI.exe"
$script:ProcessName = "SmetaNA.EditorUI"
$script:TempExtractDir = ""

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-NormalizedRelPath([string]$RelPath) {
    return ($RelPath -replace "\\", "/")
}

function Get-TargetPath([string]$Root, [string]$RelPath) {
    $rel = (ConvertTo-NormalizedRelPath $RelPath).Replace('/', [IO.Path]::DirectorySeparatorChar)
    return Join-Path $Root $rel
}

function Assert-SafeRelPath([string]$RelPath) {
    $norm = ConvertTo-NormalizedRelPath $RelPath
    if ([string]::IsNullOrWhiteSpace($norm)) { throw "Пустой путь в манифесте." }
    if ($norm.StartsWith("/") -or $norm -match "^[A-Za-z]:") { throw "Абсолютный путь в манифесте запрещён: $RelPath" }
    if ($norm -match "(^|/)\.\.(/|$)") { throw "Выход из каталога установки запрещён: $RelPath" }
    return $norm
}

function Find-ManifestPath([string]$Root) {
    # Манифест — у корня пакета; допускается вложенность от распаковки (до 2 уровней).
    foreach ($dir in @($Root) + @(Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }) + @(Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue | ForEach-Object { Get-ChildItem $_.FullName -Directory -ErrorAction SilentlyContinue } | ForEach-Object { $_.FullName })) {
        $candidate = Join-Path $dir "update-manifest.json"
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

function Stop-WithCode([string]$Message, [int]$Code) {
    Write-Host ""
    Write-Host "[smetana] $Message" -ForegroundColor Red
    exit $Code
}

function Test-ExcludedFileName([string]$Name) {
    # Служебное окружение установки — не трогаем при обновлении и не включаем в SHA256SUMS.
    return ($Name -eq "SHA256SUMS.txt") -or ($Name -like "unins*") -or ($Name -like "crash-*.log")
}

function Resolve-InstallDir([string]$Requested) {
    if ($Requested -ne "") {
        if (-not (Test-Path $Requested)) { Stop-WithCode "Каталог установки не найден: $Requested" 2 }
        return (Resolve-Path $Requested).Path
    }
    if (Test-Path $script:UninstallKey) {
        $loc = (Get-ItemProperty -Path $script:UninstallKey -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
        if ($loc -and (Test-Path (Join-Path $loc $script:AppExeName))) { return (Resolve-Path $loc).Path }
    }
    $default = Join-Path $env:LOCALAPPDATA "Programs\smetana"
    if (Test-Path (Join-Path $default $script:AppExeName)) { return (Resolve-Path $default).Path }
    Stop-WithCode "Установленная программа не найдена (нет $script:AppExeName). Для новой установки используйте полный установщик." 2
}

function Assert-AppNotRunning([bool]$AllowClose) {
    $proc = @(Get-Process -Name $script:ProcessName -ErrorAction SilentlyContinue)
    if ($proc.Count -eq 0) { return }
    if (-not $AllowClose) {
        Stop-WithCode "Приложение smetana запущено — закройте его и повторите запуск (или используйте -CloseApp)." 2
    }
    Write-Host "Закрываю запущенную smetana ($($proc.Count) процесс(ов))..."
    foreach ($p in $proc) { $null = $p.CloseMainWindow() }
    Start-Sleep -Seconds 5
    $proc = @(Get-Process -Name $script:ProcessName -ErrorAction SilentlyContinue)
    foreach ($p in $proc) {
        Write-Warning "Процесс не завершился штатно, останавливаю принудительно (PID $($p.Id))."
        Stop-Process -Id $p.Id -Force
    }
}

function Get-InstalledVersion([string]$Dir) {
    $exe = Join-Path $Dir $script:AppExeName
    if (-not (Test-Path $exe)) { return "" }
    $vi = (Get-Item $exe).VersionInfo
    $v = $vi.ProductVersion
    if ([string]::IsNullOrWhiteSpace($v)) { $v = $vi.FileVersion }
    if ($v) { $v = (($v -split '\+')[0]).Trim() }
    return $v
}

function Write-SumsFile([string]$Root, [string]$ExcludeRoot) {
    $lines = New-Object System.Collections.Generic.List[string]
    Get-ChildItem $Root -Recurse -File | ForEach-Object {
        $skip = (Test-ExcludedFileName $_.Name)
        if (-not $skip -and -not [string]::IsNullOrEmpty($ExcludeRoot)) {
            if ($_.FullName.StartsWith($ExcludeRoot, [StringComparison]::OrdinalIgnoreCase)) { $skip = $true }
        }
        if (-not $skip) {
            $rel = $_.FullName.Substring($Root.Length).TrimStart("\", "/") -replace "\\", "/"
            $lines.Add("$(Get-Sha256 $_.FullName)  $rel")
        }
    }
    $lines | Sort-Object | Set-Content -Encoding ascii (Join-Path $Root "SHA256SUMS.txt")
}

function Set-AddRemoveProgramsVersion([string]$InstallDir, [string]$Version) {
    if ([string]::IsNullOrWhiteSpace($Version)) { return }
    if (-not (Test-Path $script:UninstallKey)) {
        Write-Host "Запись «Установка и удаление программ» не найдена — версия в реестр не записана."
        return
    }
    $loc = (Get-ItemProperty -Path $script:UninstallKey -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
    if ($loc -and -not $loc.TrimEnd("\").Equals($InstallDir.TrimEnd("\"), [StringComparison]::OrdinalIgnoreCase)) {
        Write-Warning "Каталог установки не совпадает с записью в реестре ($loc) — версия в реестр не записана."
        return
    }
    try {
        Set-ItemProperty -Path $script:UninstallKey -Name DisplayVersion -Value $Version -Type String
        Write-Host "Версия в «Установке и удалении программ» обновлена: $Version"
    } catch {
        Write-Warning "Не удалось обновить версию в реестре: $($_.Exception.Message)"
    }
}

function Get-InstallFileRels([string]$Root, [string]$ExcludeRoot) {
    $rels = New-Object System.Collections.Generic.List[string]
    Get-ChildItem $Root -Recurse -File | ForEach-Object {
        $skip = (Test-ExcludedFileName $_.Name)
        if (-not $skip -and -not [string]::IsNullOrEmpty($ExcludeRoot)) {
            if ($_.FullName.StartsWith($ExcludeRoot, [StringComparison]::OrdinalIgnoreCase)) { $skip = $true }
        }
        if (-not $skip) {
            $rels.Add(($_.FullName.Substring($Root.Length).TrimStart("\", "/") -replace "\\", "/"))
        }
    }
    return $rels
}

function Invoke-RestoreBackup([string]$BackupDir, [string]$InstallDir) {
    $bmPath = Join-Path $BackupDir "backup-manifest.json"
    if (-not (Test-Path $bmPath)) { throw "Не найден backup-manifest.json в $BackupDir" }
    $bm = Get-Content $bmPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $restored = 0
    foreach ($e in @($bm.files)) {
        $rel = Assert-SafeRelPath $e.path
        $dst = Get-TargetPath $InstallDir $rel
        if ($e.existed) {
            $src = Get-TargetPath (Join-Path $BackupDir "files") $rel
            if (-not (Test-Path $src)) { throw "Резервная копия повреждена, нет файла: $rel" }
            New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
            Copy-Item $src $dst -Force
            $restored++
        } else {
            if (Test-Path $dst) { Remove-Item $dst -Force; $restored++ }
        }
    }
    Write-SumsFile $InstallDir $BackupDir
    Set-AddRemoveProgramsVersion $InstallDir $bm.versionFrom
    return $restored
}

function Get-BackupDirPath([string]$Root) {
    $dir = Join-Path $Root (Get-Date -Format "yyyyMMdd-HHmmss")
    $n = 1
    while (Test-Path $dir) {
        $dir = Join-Path $Root ("{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $n)
        $n++
    }
    return $dir
}

function Remove-OldBackups([string]$Root, [int]$Keep) {
    $keep = [Math]::Max(1, $Keep)
    $existing = @(Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
    for ($i = $keep; $i -lt $existing.Count; $i++) {
        Write-Host "Удаляю старую резервную копию: $($existing[$i].Name)"
        Remove-Item $existing[$i].FullName -Recurse -Force
    }
}

try {
    # ---------- Режим отката (пакет обновления не требуется) ----------
    if ($Rollback -ne "") {
        if (-not (Test-Path $Rollback)) { Stop-WithCode "Резервная копия не найдена: $Rollback" 2 }
        $backupDir = (Resolve-Path $Rollback).Path
        $installRoot = Resolve-InstallDir $InstallDir
        Assert-AppNotRunning ([bool]$CloseApp)
        if ($DryRun) {
            Write-Host "[dry-run] Откат из $backupDir в $installRoot — файлы будут восстановлены по backup-manifest.json."
            exit 0
        }
        Write-Host "Откат обновления: $backupDir -> $installRoot"
        $restored = Invoke-RestoreBackup $backupDir $installRoot
        Write-Host ""
        Write-Host "[smetana] Откат выполнен, восстановлено файлов: $restored." -ForegroundColor Green
        exit 0
    }

    # ---------- 0. Пакет ----------
    $packageRoot = $Package
    if ($packageRoot -eq "") { $packageRoot = $PSScriptRoot }
    if (Test-Path $packageRoot -PathType Leaf) {
        $script:TempExtractDir = Join-Path ([IO.Path]::GetTempPath()) ("smetana-update-" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $script:TempExtractDir | Out-Null
        Write-Host "Распаковка пакета: $packageRoot"
        Expand-Archive -Path $packageRoot -DestinationPath $script:TempExtractDir -Force
        $packageRoot = $script:TempExtractDir
    } elseif (Test-Path $packageRoot -PathType Container) {
        $packageRoot = (Resolve-Path $packageRoot).Path
    } else {
        Stop-WithCode "Пакет не найден: $Package" 2
    }

    $manifestPath = Find-ManifestPath $packageRoot
    if ($null -eq $manifestPath) {
        Stop-WithCode ("Не найден update-manifest.json (проверено: $packageRoot). Это не пакет обновления smetana." +
            " Распакуйте SmetaNA-*-update-*.zip целиком и запускайте update.cmd из каталога пакета" +
            " (или apply-update.ps1 -Package <пакет.zip или его каталог>)." +
            " Если скрипт запущен из deploy\windows репозитория — это шаблон: пакет собирается" +
            " deploy\windows\build-update-package.ps1 (см. docs/release.md §8.1).") 2
    }
    $packageRoot = Split-Path $manifestPath -Parent

    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $manifest.formatVersion -or $manifest.formatVersion -ne 1) {
        Stop-WithCode "Неподдерживаемая версия манифеста пакета: $($manifest.formatVersion)" 2
    }
    if ($manifest.product -ne "smetana") { Stop-WithCode "Пакет другого продукта: $($manifest.product)" 2 }
    if ($manifest.mode -ne "full" -and $manifest.mode -ne "delta") { Stop-WithCode "Неизвестный режим пакета: $($manifest.mode)" 2 }
    $payloadDir = Join-Path $packageRoot "payload"
    if (-not (Test-Path $payloadDir)) { Stop-WithCode "В пакете нет каталога payload\." 2 }
    $manifestFiles = @($manifest.files)
    $manifestDelete = @($manifest.delete)

    # ---------- 1. Каталог установки ----------
    $installRoot = Resolve-InstallDir $InstallDir
    Write-Host "Каталог установки: $installRoot"
    $fromLabel = if ($manifest.versionFrom) { $manifest.versionFrom } else { "текущей версии" }
    Write-Host "Обновление: $fromLabel -> $($manifest.versionTo) (режим: $($manifest.mode))"

    Assert-AppNotRunning ([bool]$CloseApp)

    # ---------- 2. Предпроверки (ничего не изменяется) ----------
    $installedVersion = Get-InstalledVersion $installRoot
    if ($manifest.mode -eq "delta" -and $manifest.versionFrom) {
        if ($installedVersion -eq "") {
            if (-not $Force) {
                Stop-WithCode "Не удалось определить версию установленной программы. Дельта-пакет применим только к версии $($manifest.versionFrom). Используйте полный пакет обновления или -Force." 2
            }
            Write-Warning "Версия установленной программы неизвестна; применяю дельта-пакет из-за -Force."
        } elseif ($installedVersion -ne $manifest.versionFrom) {
            if (-not $Force) {
                Stop-WithCode "Дельта-пакет для версии $($manifest.versionFrom), а установлена $installedVersion. Используйте полный пакет обновления или -Force." 2
            }
            Write-Warning "Версии не совпадают ($installedVersion != $($manifest.versionFrom)); применяю из-за -Force."
        }
    }

    $mismatches = New-Object System.Collections.Generic.List[string]
    foreach ($f in $manifestFiles) {
        $rel = Assert-SafeRelPath $f.path
        if ($null -eq $f.prevSha256) { continue }
        $dst = Get-TargetPath $installRoot $rel
        if (-not (Test-Path $dst)) { $mismatches.Add("$rel (файл отсутствует)"); continue }
        $cur = Get-Sha256 $dst
        if ($cur -ne $f.prevSha256.ToLowerInvariant()) { $mismatches.Add("$rel (хэш $cur != $($f.prevSha256))") }
    }
    if ($mismatches.Count -gt 0) {
        if ($manifest.mode -eq "delta" -and -not $Force) {
            $mismatches | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
            Stop-WithCode "Установка отличается от базовой версии пакета ($($mismatches.Count) файл(ов)). Используйте полный пакет обновления или -Force." 2
        }
        Write-Warning "Файлы отличаются от ожидаемых до обновления ($($mismatches.Count)) — будут перезаписаны:"
        $mismatches | ForEach-Object { Write-Warning "  $_" }
    }

    # Список удалений: для mode=full — зеркальная синхронизация (устаревшими считаются
    # файлы установки, отсутствующие в пакете; служебные файлы не трогаем).
    $deleteList = New-Object System.Collections.Generic.List[string]
    foreach ($rel in $manifestDelete) { $deleteList.Add((Assert-SafeRelPath $rel)) }
    if ($manifest.mode -eq "full") {
        $newSet = @{}
        foreach ($f in $manifestFiles) { $newSet[(Assert-SafeRelPath $f.path)] = $true }
        $excludeRoot = ""
        if ($BackupRoot -ne "") {
            $resolved = Resolve-Path $BackupRoot -ErrorAction SilentlyContinue
            if ($resolved) { $excludeRoot = $resolved.Path } else { $excludeRoot = $BackupRoot }
        }
        foreach ($rel in (Get-InstallFileRels $installRoot $excludeRoot)) {
            if (-not $newSet.ContainsKey($rel) -and -not $deleteList.Contains($rel)) { $deleteList.Add($rel) }
        }
    }

    $toWrite = $manifestFiles.Count
    $toDelete = $deleteList.Count
    Write-Host "План: записать/заменить файлов — $toWrite, удалить — $toDelete."

    if ($DryRun) {
        foreach ($f in $manifestFiles) { Write-Host "  [запись]  $((Assert-SafeRelPath $f.path))" }
        foreach ($rel in $deleteList) { Write-Host "  [удаление] $rel" }
        Write-Host "[dry-run] Изменений не внесено."
        exit 0
    }

    # ---------- 3. Резервная копия ----------
    if ($BackupRoot -eq "") { $BackupRoot = Join-Path (Split-Path $installRoot -Parent) "smetana-update-backup" }
    New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
    $backupDir = Get-BackupDirPath $BackupRoot
    $backupFiles = Join-Path $backupDir "files"
    Write-Host "Резервная копия: $backupDir"

    $entries = New-Object System.Collections.Generic.List[object]
    $touched = New-Object System.Collections.Generic.List[string]
    foreach ($f in $manifestFiles) { $touched.Add((Assert-SafeRelPath $f.path)) }
    foreach ($rel in $deleteList) { if (-not $touched.Contains($rel)) { $touched.Add($rel) } }

    foreach ($rel in $touched) {
        $dst = Get-TargetPath $installRoot $rel
        $existed = Test-Path $dst
        $entry = [ordered]@{ path = $rel; existed = [bool]$existed; sha256 = $null }
        if ($existed) {
            $entry.sha256 = Get-Sha256 $dst
            $copy = Get-TargetPath $backupFiles $rel
            New-Item -ItemType Directory -Force -Path (Split-Path $copy -Parent) | Out-Null
            Copy-Item $dst $copy -Force
        }
        $entries.Add([pscustomobject]$entry)
    }
    $backupManifest = [ordered]@{
        createdUtc = (Get-Date).ToUniversalTime().ToString("o")
        installDir = $installRoot
        versionFrom = $manifest.versionFrom
        versionTo = $manifest.versionTo
        files = $entries
    }
    $backupManifest | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $backupDir "backup-manifest.json")

    # ---------- 4. Применение ----------
    try {
        $written = 0
        foreach ($f in $manifestFiles) {
            $rel = Assert-SafeRelPath $f.path
            $src = Get-TargetPath $payloadDir $rel
            $dst = Get-TargetPath $installRoot $rel
            if (-not (Test-Path $src)) { throw "В пакете нет файла payload\$rel" }
            New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
            Copy-Item $src $dst -Force
            $cur = Get-Sha256 $dst
            if ($cur -ne $f.sha256.ToLowerInvariant()) {
                throw "Хэш записанного файла не совпал с манифестом: $rel"
            }
            $written++
        }
        $deleted = 0
        foreach ($rel in $deleteList) {
            $dst = Get-TargetPath $installRoot $rel
            if (Test-Path $dst) { Remove-Item $dst -Force; $deleted++ }
        }
        Write-SumsFile $installRoot $BackupRoot
        Set-AddRemoveProgramsVersion $installRoot $manifest.versionTo
    } catch {
        Write-Warning "Сбой применения: $($_.Exception.Message)"
        Write-Warning "Выполняется откат из резервной копии $backupDir ..."
        try {
            $restored = Invoke-RestoreBackup $backupDir $installRoot
            Write-Warning "Откат выполнен, восстановлено файлов: $restored."
        } catch {
            Write-Warning "Автоматический откат не удался: $($_.Exception.Message). Восстановление вручную: apply-update.ps1 -Rollback `"$backupDir`""
        }
        exit 3
    }

    Remove-OldBackups $BackupRoot $KeepBackups

    Write-Host ""
    Write-Host "[smetana] Обновление до $($manifest.versionTo) применено: записано $written, удалено $deleted." -ForegroundColor Green
    Write-Host "Резервная копия: $backupDir"
    Write-Host "Откат (если потребуется): powershell -ExecutionPolicy Bypass -File apply-update.ps1 -Rollback `"$backupDir`""
    Write-Host "Можно запускать приложение."
    exit 0
} finally {
    if ($script:TempExtractDir -ne "") {
        Remove-Item $script:TempExtractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
