<#
.SYNOPSIS
    Код-сигнинг релизных артефактов smetana (заготовка, Windows и Linux).

.DESCRIPTION
    Windows: подпись signtool (квалифицированный сертификат УЦ, входящий в программу
    Майкрософт/расширенную проверку — иначе SmartScreen будет ругаться на новом
    издателе; допустим и обычный code signing) + RFC3161-timestamp (подпись должна
    действовать после истечения сертификата). Подписываются исполняемый файл и
    установщик.

    Linux: отсоединённая GPG-подпись (.asc) + SHA256SUMS — публикация на сайте в РФ.

    Секреты НИКОГДА не хранятся в репозитории — только переменные окружения:
      SMETANA_SIGN_THUMBPRINT  — SHA1-отпечаток сертификата в хранилище Windows;
      SMETANA_SIGN_PFX         — путь к PFX-файлу (альтернатива отпечатку);
      SMETANA_SIGN_PFX_PASSWORD— пароль PFX (только окружение!);
      SMETANA_SIGN_GPG_KEY     — идентификатор GPG-ключа (Linux);
      SMETANA_TIMESTAMP_URL    — URL timestamp-сервера (по умолчанию DigiCert RFC3161).

    Без настроенной подписи скрипт печатает, что именно будет сделано, и завершается
    с предупреждением (заготовка готова к подключению сертификата).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\sign.ps1 -Platform windows -Path artifacts\installers\SmetaNA-1.0.0-win-x64.exe
    powershell -ExecutionPolicy Bypass -File deploy\sign.ps1 -Platform linux -Path artifacts\installers\smetana_1.0.0_amd64.deb
#>
[CmdletBinding()]
param(
    [ValidateSet("windows", "linux")]
    [string]$Platform = "windows",

    # Файл или каталог (для каталога — все *.exe/*.dll или *.deb/*.AppImage).
    [Parameter(Mandatory = $true)]
    [string]$Path,

    # Только печатать команды.
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$timestampUrl = if ($env:SMETANA_TIMESTAMP_URL) { $env:SMETANA_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }

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

function Find-SignTool {
    $kits = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "x64" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return "signtool.exe"
}

$target = Resolve-Path $Path
$files = @()
if (Test-Path $target -PathType Container) {
    if ($Platform -eq "windows") {
        $files = Get-ChildItem $target -Recurse -Include *.exe, *.dll -File | Select-Object -ExpandProperty FullName
    } else {
        $files = Get-ChildItem $target -Recurse -Include *.deb, *.AppImage, *.tar.gz -File | Select-Object -ExpandProperty FullName
    }
} else {
    $files = @($target.Path)
}
if ($files.Count -eq 0) { throw "Не найдено файлов для подписи: $target" }

if ($Platform -eq "windows") {
    $thumbprint = $env:SMETANA_SIGN_THUMBPRINT
    $pfx = $env:SMETANA_SIGN_PFX
    $pfxPassword = $env:SMETANA_SIGN_PFX_PASSWORD

    if (-not $thumbprint -and -not $pfx) {
        Write-Warning "Подпись не настроена (SMETANA_SIGN_THUMBPRINT или SMETANA_SIGN_PFX). Заготовка: подписать $($files.Count) файл(ов) следующим образом:"
        Write-Host "signtool sign /fd SHA256 /td SHA256 /tr $timestampUrl /sha1 <thumbprint> <file>  # сертификат в хранилище"
        Write-Host "signtool sign /fd SHA256 /td SHA256 /tr $timestampUrl /f <cert.pfx> /p <pass> <file>  # PFX"
        Write-Host "signtool verify /pa /all <file>"
        return
    }

    $signtool = Find-SignTool
    foreach ($file in $files) {
        $signArgs = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $timestampUrl)
        if ($thumbprint) { $signArgs += @("/sha1", $thumbprint) }
        else { $signArgs += @("/f", $pfx, "/p", $pfxPassword) }
        $signArgs += $file
        Invoke-Step (@($signtool) + $signArgs)
        Invoke-Step (@($signtool, "verify", "/pa", $file))
    }
} else {
    $gpgKey = $env:SMETANA_SIGN_GPG_KEY
    if (-not $gpgKey) {
        Write-Warning "Подпись не настроена (SMETANA_SIGN_GPG_KEY). Заготовка: подписать $($files.Count) файл(ов) следующим образом:"
        Write-Host "gpg --batch --detach-sign --armor -u <keyid> <file>   # → <file>.asc"
        Write-Host "sha256sum <file> > SHA256SUMS.txt"
        return
    }
    foreach ($file in $files) {
        Invoke-Step (@("gpg", "--batch", "--detach-sign", "--armor", "-u", $gpgKey, $file))
    }
    $dir = Split-Path $files[0] -Parent
    Push-Location $dir
    try {
        Invoke-Step @("bash", "-lc", "sha256sum $(($files | ForEach-Object { Split-Path $_ -Leaf }) -join ' ') > SHA256SUMS.txt")
    } finally {
        Pop-Location
    }
}

Write-Host "Подпись завершена для $($files.Count) файл(ов)." -ForegroundColor Green
