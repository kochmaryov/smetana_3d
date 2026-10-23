<#
.SYNOPSIS
    Загрузка нативных библиотек lib3mf для SmetaNA.Formats (формат 3MF, задача 4.2).

.DESCRIPTION
    Официальный путь подключения lib3mf (спека §4, BSD-2-Clause): prebuilt-SDK
    с GitHub-релизов 3MF Consortium + официальный автогенеренный C#-биндинг
    (ThreeMf/Lib3MF/Lib3MF.cs, тот же релиз). NuGet-пакета lib3mf не существует
    (nuget.org, поиск "lib3mf" — 0 пакетов), поэтому нативная часть доставляется
    этим скриптом с закреплённой версией и проверкой SHA-256.

    Скрипт кладёт бинарники в каталоги rid рядом со скриптом:
        native/win-x64/lib3mf.dll        (Windows 10/11 x64)
        native/linux-x64/lib3mf.so       (Linux x64, glibc)
    Бинарники не коммитятся (см. .gitignore рядом со скриптом); csproj копирует их
    в выходной каталог в раскладку runtimes/<rid>/native/.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File fetch-lib3mf.ps1
    powershell -ExecutionPolicy Bypass -File fetch-lib3mf.ps1 -Runtime all
#>
[CmdletBinding()]
param(
    # Закреплённая версия lib3mf (BSD-2-Clause); для новой версии добавьте SHA-256 в таблицу $Artifacts.
    [string]$Version = "2.5.0",

    # Какие библиотеки загрузить.
    [ValidateSet("win-x64", "linux-x64", "all")]
    [string]$Runtime = "win-x64",

    # Каталог назначения; по умолчанию — каталог скрипта.
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"

if ($Destination -eq "") {
    $Destination = $PSScriptRoot
}

# SHA-256 официальных артефактов релиза (сверяются перед распаковкой).
$Artifacts = @{
    "2.5.0|win-x64" = @{
        Archive = "lib3mf-2.5.0-Windows.zip"
        Sha256 = "8E185BC19E5D544C3C1E489FD8F8FCECFB0530DEF36E5C8BCC81BEBF2AC96AA1"
        Member = "lib3mf-2.5.0-Windows/bin/lib3mf.dll"
        TargetName = "lib3mf.dll"
    }
    "2.5.0|linux-x64" = @{
        Archive = "lib3mf-2.5.0-Linux.zip"
        Sha256 = "658B40F83201DD172C8B3FE6BB4F21A047AA7E4125061399B6DA9EA635EBDA48"
        Member = "lib3mf-2.5.0-Linux/lib/lib3mf.so.2.5.0.0"
        TargetName = "lib3mf.so"
    }
}

$runtimes = @()
if ($Runtime -eq "win-x64" -or $Runtime -eq "all") { $runtimes += "win-x64" }
if ($Runtime -eq "linux-x64" -or $Runtime -eq "all") { $runtimes += "linux-x64" }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

$workDir = Join-Path ([IO.Path]::GetTempPath()) ("lib3mf-fetch-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workDir | Out-Null

try {
    foreach ($rid in $runtimes) {
        $key = "$Version|$rid"
        if (-not $Artifacts.ContainsKey($key)) {
            throw "Версия $Version для $rid не закреплена по SHA-256. Добавьте запись в таблицу `$Artifacts (официальный релиз: https://github.com/3MFConsortium/lib3mf/releases)."
        }

        $artifact = $Artifacts[$key]
        $archivePath = Join-Path $workDir $artifact.Archive
        $url = "https://github.com/3MFConsortium/lib3mf/releases/download/v$Version/$($artifact.Archive)"
        Write-Host "lib3mf $Version ($rid): загрузка $url"
        Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing

        $hash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash
        if ($hash -ne $artifact.Sha256) {
            throw "SHA-256 архива $($artifact.Archive) не совпадает: ожидался $($artifact.Sha256), получен $hash."
        }

        $extractDir = Join-Path $workDir $rid
        [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $extractDir)

        $memberPath = Join-Path $extractDir $artifact.Member
        if (-not (Test-Path $memberPath)) {
            throw "В архиве $($artifact.Archive) нет ожидаемого файла $($artifact.Member)."
        }

        $targetDir = Join-Path $Destination $rid
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        $targetPath = Join-Path $targetDir $artifact.TargetName
        Copy-Item $memberPath $targetPath -Force
        Write-Host "lib3mf $Version ($rid): готово -> $targetPath (BSD-2-Clause, 3MF Consortium)"
    }
}
finally {
    Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue
}
