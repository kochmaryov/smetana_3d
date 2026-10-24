<#
.SYNOPSIS
    Аудит офлайн-режима smetana: подтверждение отсутствия обязательных сетевых вызовов.

.DESCRIPTION
    Требования: спека §4 (офлайн-режим приложения), R4 (санкции — работа без сети),
    DoD §5.8 (активация и работа ≥ 30 дней без сети). Приложение обязано работать
    полностью офлайн: никакой телеметрии, автообновлений или обязательной сети в v1.

    Проверки:
      1. Статический аудит исходников src/**: сетевые API (HttpClient, WebRequest,
         Sockets, Dns, gRPC, WebSocket, загрузка файлов, телеметрия) — комментарии
         отбрасываются, срабатывания = ошибка аудита (код выхода 2).
      2. Опционально (-PublishedDir): сканирование опубликованного среза на строки
         http(s):// в сборках — информационный отчёт (лицензионные тексты и ссылки
         на документацию допустимы, см. allowlist).

    Результат: artifacts\offline-audit\offline-report.md.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\verify-offline.ps1
    powershell -ExecutionPolicy Bypass -File deploy\verify-offline.ps1 -PublishedDir artifacts\publish\win-x64
#>
[CmdletBinding()]
param(
    # Сканировать и тестовые проекты (в продукт не поставляются; для полноты аудита).
    [switch]$IncludeTests,

    # Опционально: каталог опубликованного среза для строкового сканирования сборок.
    [string]$PublishedDir = "",

    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
if ($OutputDir -eq "") { $OutputDir = Join-Path $repoRoot "artifacts\offline-audit" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Сетевые API и телеметрия — их не должно быть в src/ (комментарии отбрасываются до проверки).
$ForbiddenPatterns = @(
    @{ Name = "HttpClient/HttpWebRequest"; Pattern = "\bHttpClient\b|\bHttpWebRequest\b|\bHttpRequestMessage\b|\bSocketsHttpHandler\b" }
    @{ Name = "WebClient/WebRequest"; Pattern = "\bWebClient\b|\bWebRequest\b|\bDownloadString\b|\bDownloadFile\b|\bUploadString\b|\bUploadFile(Async)?\b" }
    @{ Name = "Сокеты/DNS"; Pattern = "\bTcpClient\b|\bUdpClient\b|\bTcpListener\b|\bSocket\s*\(|\bDns\.\b|\bWebSocket\b|\bWebSocketSharp\b" }
    @{ Name = "Почта/мессенджеры"; Pattern = "\bSmtpClient\b|\bMailMessage\b|\bMailKit\b" }
    @{ Name = "gRPC/REST-клиенты"; Pattern = "\bGrpc\b|\bRestSharp\b|\bFlurl\b|\bRefit\b" }
    @{ Name = "WinHTTP/WinINet/curl"; Pattern = "\bWinHttp\w*|\bInternetOpen\w*|\bcurl_easy\w*|\blibcurl\b" }
    @{ Name = "Телеметрия/аналитика"; Pattern = "\bTelemetry\b|\bAnalytics\b|\bAppCenter\b|\bSentry\b|\bYandex\.Metrica\b|\bmetrika\b" }
)

# Допустимые URL в бинарных сборках: лицензионные тексты, исходники, документация.
$UrlAllowlist = @(
    "github.com", "opensource.org", "apache.org", "dotnet.microsoft.com", "go.microsoft.com",
    "nuget.org", "avaloniaui.net", "3mfconsortium", "elalish", "manifoldcad", "silkip", "dotnetfoundation",
    "schemas.", "w3.org", "microsoft.com", "foundation.", "mit-license", "boost.org"
)

function Remove-Comments {
    param([string]$Text)
    $noBlock = [regex]::Replace($Text, "(?s)/\*.*?\*/", "")
    return [regex]::Replace($noBlock, "(?m)//.*$", "")
}

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Аудит офлайн-режима smetana")
$report.Add("")
$report.Add("Сгенерировано: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')) (deploy/verify-offline.ps1).")
$report.Add("")
$report.Add("Требования: спека §4 (офлайн), R4 (санкции), DoD §5.8 (работа ≥ 30 дней без сети).")
$report.Add("")

# --- 1. Статический аудит исходников ------------------------------------------------------
$searchRoots = @(Join-Path $repoRoot "src")
if ($IncludeTests) { $searchRoots += (Join-Path $repoRoot "tests") }

$findings = New-Object System.Collections.Generic.List[string]
$scannedFiles = 0
foreach ($root in $searchRoots) {
    foreach ($file in Get-ChildItem $root -Recurse -Filter *.cs -File | Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" }) {
        $scannedFiles++
        $text = Remove-Comments (Get-Content $file.FullName -Raw)
        foreach ($rule in $ForbiddenPatterns) {
            $matches2 = [regex]::Matches($text, $rule.Pattern)
            foreach ($m in $matches2) {
                $line = ($text.Substring(0, $m.Index) -split "`n").Count
                $rel = $file.FullName.Substring($repoRoot.Length + 1) -replace "\\", "/"
                $findings.Add("$($rule.Name): $rel`:$line — ``$($m.Value)``")
            }
        }
    }
}

$report.Add("## 1. Статический аудит исходников")
$report.Add("")
$report.Add("Просканировано файлов C#: $scannedFiles (каталоги: $((($searchRoots | ForEach-Object { Split-Path $_ -Leaf }) | Sort-Object -Unique) -join ', ')).")
$report.Add("")
if ($findings.Count -eq 0) {
    $report.Add("**Сетевые API не найдены — обязательных сетевых вызовов в приложении нет.**")
} else {
    $report.Add("**НАЙДЕНЫ сетевые вызовы ($($findings.Count)) — требуется разбор:**")
    $report.Add("")
    foreach ($f in $findings) { $report.Add("- $f") }
}
$report.Add("")

# --- 2. Строковое сканирование опубликованного среза ---------------------------------------
if ($PublishedDir -ne "") {
    $report.Add("## 2. Строки URL в опубликованном срезе")
    $report.Add("")
    if (-not (Test-Path $PublishedDir)) {
        $report.Add("Каталог $PublishedDir не найден — проверка пропущена.")
    } else {
        $urlFindings = New-Object System.Collections.Generic.List[string]
        foreach ($file in Get-ChildItem $PublishedDir -Recurse -Include *.dll, *.exe, *.so, *.json -File) {
            $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
            $utf16 = [System.Text.Encoding]::Unicode.GetString($bytes)
            foreach ($content in @($ascii, $utf16)) {
                foreach ($m in [regex]::Matches($content, "https?://[\x21-\x7e]{4,120}")) {
                    $url = $m.Value
                    $allowed = $false
                    foreach ($a in $UrlAllowlist) { if ($url -match [regex]::Escape($a)) { $allowed = $true; break } }
                    if (-not $allowed) {
                        $rel = $file.FullName.Substring((Resolve-Path $PublishedDir).Path.Length + 1) -replace "\\", "/"
                        $urlFindings.Add("$rel — $url")
                    }
                }
            }
        }
        $urlFindings = $urlFindings | Sort-Object -Unique
        $report.Add("Найдено URL вне allowlist: $(@($urlFindings).Count).")
        $report.Add("")
        $report.Add("Информационно: строки URL в сборках не означают сетевые вызовы (лицензионные тексты,")
        $report.Add("ссылки на документацию). Обязательные сетевые вызовы проверяются п. 1.")
        $report.Add("")
        foreach ($u in $urlFindings) { $report.Add("- $u") }
    }
    $report.Add("")
}

$report.Add("## Вывод")
$report.Add("")
if ($findings.Count -eq 0) {
    $report.Add("Аудит пройден: приложение smetana не содержит сетевых вызовов; активация и лицензирование")
    $report.Add("полностью офлайн (SmetaNA.Licensing: подписанные ключи/токены, node-lock, локальный леджер).")
} else {
    $report.Add("Аудит НЕ пройден: устраните сетевые вызовы или задокументируйте их как необязательные")
    $report.Add("и отключаемые (офлайн-режим не должен деградировать).")
}

$reportPath = Join-Path $OutputDir "offline-report.md"
$report | Set-Content -Encoding utf8 $reportPath
Write-Host "Отчёт: $reportPath"
Write-Host "Файлов C#: $scannedFiles; сетевых срабатываний: $($findings.Count)"

if ($findings.Count -gt 0) { exit 2 }
exit 0
