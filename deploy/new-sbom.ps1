<#
.SYNOPSIS
    SBOM и инвентаризация лицензий зависимостей SmetaNA (CycloneDX + политика лицензий).

.DESCRIPTION
    Собирает фактический состав зависимостей решения (NuGet, включая транзитивные),
    сверяет лицензии с политикой «только MIT/Apache/BSD/Boost» (спека §4, риск R7)
    и формирует:

      artifacts\sbom\sbom.cdx.json    — SBOM в формате CycloneDX 1.5;
      artifacts\sbom\licenses.csv     — таблица: пакет, версия, лицензия, статус;
      artifacts\sbom\licenses.md      — человекочитаемый отчёт с нарушениями.

    Лицензия берётся из nuspec (<license expression> или <license file/url>);
    для file:-записей текст лицензии классифицируется автоматически (MIT /
    Apache-2.0 / BSD-2 / BSD-3 / Boost), иначе статус NOASSERTION — предупреждение
    с требованием ручной проверки. Ненуветные компоненты (Manifold, oneTBB, lib3mf,
    рантайм-пакеты self-contained-сборки) описываются вручную в $ManualComponents.

    Коды выхода: 0 — политика соблюдена; 2 — есть нарушения политики.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy\new-sbom.ps1
    powershell -ExecutionPolicy Bypass -File deploy\new-sbom.ps1 -IncludeTests
#>
[CmdletBinding()]
param(
    # Учитывать тестовые проекты (dev-зависимости; в продукт не поставляются).
    [switch]$IncludeTests,

    # Каталог результатов.
    [string]$OutputDir = "",

    # Каталог глобального NuGet-кэша с nuspec-файлами.
    [string]$NuGetPackagesDir = ""
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
if ($OutputDir -eq "") { $OutputDir = Join-Path $repoRoot "artifacts\sbom" }
if ($NuGetPackagesDir -eq "") { $NuGetPackagesDir = Join-Path $env:USERPROFILE ".nuget\packages" }

$dotnet = if ($env:SMETANA_DOTNET) { $env:SMETANA_DOTNET } else {
    $local = "C:\Users\mnk\.dotnet\dotnet.exe"
    if (Test-Path $local) { $local } else { "dotnet" }
}

# Политика лицензий (спека §4 / R7): только permissive-набор без обязательств раскрытия исходников.
$PolicyAllow = @("MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "BSL-1.0", "Boost-1.0")
$PolicyDenyPattern = "GPL|LGPL|AGPL|SSPL|CPAL|OSL|MPL|EPL|CECILL|WTFPL|Proprietary|Commercial|Unlicense"

# Ненуветные компоненты продукта: проверены вручную (лицензии по официальным репозиториям/релизам).
$ManualComponents = @(
    @{ Id = "manifold"; Version = "3.5.4"; License = "Apache-2.0"; Scope = "runtime"; Type = "library";
       Url = "https://github.com/elalish/manifold"; Note = "CSG-ядро (C API MANIFOLD_CBIND); собирается из исходников, см. spikes/Spike.Manifold/build-native.ps1" }
    @{ Id = "oneTBB"; Version = "2022.3.0"; License = "Apache-2.0"; Scope = "runtime"; Type = "library";
       Url = "https://github.com/uxlfoundation/oneTBB"; Note = "Опционально: TBB-сборка Manifold (MANIFOLD_PAR=ON), линкуется статически" }
    @{ Id = "lib3mf"; Version = "2.5.0"; License = "BSD-2-Clause"; Scope = "runtime"; Type = "library";
       Url = "https://github.com/3MFConsortium/lib3mf"; Note = "Включая официальный автогенерённый C#-биндинг (ThreeMf/Lib3MF/Lib3MF.cs) — тот же релиз и лицензия" }
    @{ Id = "Microsoft.NETCore.App.Runtime.win-x64"; Version = "10.0.x"; License = "MIT"; Scope = "runtime"; Type = "operating-system";
       Url = "https://github.com/dotnet/runtime"; Note = "Рантайм self-contained-сборки win-x64 (MIT, .NET Foundation)" }
    @{ Id = "Microsoft.NETCore.App.Runtime.linux-x64"; Version = "10.0.x"; License = "MIT"; Scope = "runtime"; Type = "operating-system";
       Url = "https://github.com/dotnet/runtime"; Note = "Рантайм self-contained-сборки linux-x64 (MIT, .NET Foundation)" }
)

function Get-NuspecLicense {
    param([string]$PackageId, [string]$PackageVersion)

    $dir = Join-Path $NuGetPackagesDir "$($PackageId.ToLowerInvariant())\$PackageVersion"
    $nuspecPath = Join-Path $dir "$($PackageId.ToLowerInvariant()).nuspec"
    if (-not (Test-Path $nuspecPath)) {
        return @{ License = "NOASSERTION"; Kind = "nuspec-not-found"; Text = "" }
    }
    [xml]$xml = Get-Content $nuspecPath
    $md = $xml.package.metadata
    $licenseNode = $md.license
    if ($licenseNode) {
        $kind = $licenseNode.type
        $value = $licenseNode.'#text'
        if ($kind -eq "expression") { return @{ License = $value.Trim(); Kind = "expression"; Text = "" } }
        # file: — классифицируем по фактическому тексту лицензии в пакете.
        $file = Join-Path $dir $value
        if (Test-Path $file) {
            $text = Get-Content $file -Raw
            return @{ License = (Get-LicenseFromText $text); Kind = "file:$value"; Text = $text }
        }
        return @{ License = "NOASSERTION"; Kind = "file-missing:$value"; Text = "" }
    }
    if ($md.licenseUrl) {
        foreach ($candidate in @("LICENSE.txt", "LICENSE", "LICENSE.TXT", "license.txt", "Licenses/LICENSE")) {
            $file = Join-Path $dir $candidate
            if (Test-Path $file) {
                $text = Get-Content $file -Raw
                return @{ License = (Get-LicenseFromText $text); Kind = "url+$candidate"; Text = $text }
            }
        }
        return @{ License = "NOASSERTION"; Kind = "licenseUrl-only"; Text = "" }
    }
    return @{ License = "NOASSERTION"; Kind = "no-license-metadata"; Text = "" }
}

function Get-LicenseFromText {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return "NOASSERTION" }
    if ($Text -match "Apache License" -and $Text -match "Version 2\.0") { return "Apache-2.0" }
    if ($Text -match "Permission is hereby granted, free of charge") { return "MIT" }
    if ($Text -match "Boost Software License") { return "BSL-1.0" }
    if ($Text -match "Redistribution and use in source and binary forms") {
        if ($Text -match "Neither the name") { return "BSD-3-Clause" }
        return "BSD-2-Clause"
    }
    return "NOASSERTION"
}

# --- 1. Фактический состав NuGet-зависимостей -------------------------------------------
$jsonPath = Join-Path $OutputDir "_dotnet-list.json"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& $dotnet "list" (Join-Path $repoRoot "SmetaNA.slnx") "package" "--include-transitive" "--format" "json" 2>$null |
    Out-File -Encoding utf8 $jsonPath
if ($LASTEXITCODE -ne 0) { throw "dotnet list package завершился с ошибкой $LASTEXITCODE" }

$listJson = Get-Content $jsonPath -Raw | ConvertFrom-Json
$rows = @{}   # id|version -> @{ Id; Version; Kinds; Projects }
foreach ($proj in $listJson.projects) {
    $isTest = $proj.path -match "[\\/]tests[\\/]" -or $proj.path -match "[\\/]spikes[\\/]"
    if ($isTest -and -not $IncludeTests) { continue }
    foreach ($fw in $proj.frameworks) {
        $entries = @($fw.topLevelPackages) + @($fw.transitivePackages)
        foreach ($pkg in $entries) {
            if (-not $pkg.id) { continue }
            $ver = if ($pkg.resolvedVersion) { $pkg.resolvedVersion } else { $pkg.requestedVersion }
            $key = "$($pkg.id)|$ver"
            if (-not $rows.ContainsKey($key)) {
                $rows[$key] = @{ Id = $pkg.id; Version = $ver; Kinds = New-Object System.Collections.Generic.List[string]; Projects = New-Object System.Collections.Generic.List[string] }
            }
            $kind = if ($pkg.requestedVersion -and -not $pkg.resolvedVersion) { "top" } elseif ($pkg.requestedVersion) { "top" } else { "transitive" }
            if (-not $rows[$key].Kinds.Contains($kind)) { $rows[$key].Kinds.Add($kind) }
            $projTag = if ($isTest) { "tests" } else { "runtime" }
            if (-not $rows[$key].Projects.Contains($projTag)) { $rows[$key].Projects.Add($projTag) }
        }
    }
}

# --- 2. Классификация лицензий и сверка с политикой --------------------------------------
$results = New-Object System.Collections.Generic.List[object]
foreach ($key in ($rows.Keys | Sort-Object)) {
    $r = $rows[$key]
    $info = Get-NuspecLicense -PackageId $r.Id -PackageVersion $r.Version
    $scope = if ($r.Projects -contains "tests") { "dev" } else { "runtime" }
    $status = if ($info.License -eq "NOASSERTION") { "REVIEW" }
        elseif ($info.License -match $PolicyDenyPattern) { "VIOLATION" }
        elseif ($PolicyAllow -contains $info.License) { "OK" }
        else { "REVIEW" }
    $results.Add([pscustomobject]@{
        Id = $r.Id; Version = $r.Version; License = $info.License; LicenseKind = $info.Kind
        Scope = $scope; Source = "nuget"; Status = $status; Note = ""
    })
}

foreach ($m in $ManualComponents) {
    $results.Add([pscustomobject]@{
        Id = $m.Id; Version = $m.Version; License = $m.License; LicenseKind = "manual-verified"
        Scope = $m.Scope; Source = "native/bundled"; Status = "OK"; Note = $m.Note
    })
}

# --- 3. Отчёты --------------------------------------------------------------------------
$violations = @($results | Where-Object { $_.Status -eq "VIOLATION" })
$review = @($results | Where-Object { $_.Status -eq "REVIEW" })
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$csvPath = Join-Path $OutputDir "licenses.csv"
$results |
    Select-Object Id, Version, License, LicenseKind, Scope, Source, Status, Note |
    Export-Csv -NoTypeInformation -Encoding utf8 $csvPath

$mdPath = Join-Path $OutputDir "licenses.md"
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# SBOM / инвентаризация лицензий SmetaNA")
$md.Add("")
$md.Add("Сгенерировано: $timestamp (deploy/new-sbom.ps1). Политика: только MIT/Apache-2.0/BSD/Boost (спека §4, R7).")
$md.Add("")
$md.Add("**Итог:** компонентов — $($results.Count); нарушений — $($violations.Count); требуют ручной проверки — $($review.Count).")
$md.Add("")
$md.Add("| Пакет/компонент | Версия | Лицензия | Область | Статус |")
$md.Add("|---|---|---|---|---|")
foreach ($r in ($results | Sort-Object Status, Id)) {
    $md.Add("| $($r.Id) | $($r.Version) | $($r.License) ($($r.LicenseKind)) | $($r.Scope) | $($r.Status) |")
}
if ($violations.Count -gt 0) {
    $md.Add("")
    $md.Add("## Нарушения политики")
    foreach ($v in $violations) { $md.Add("- **$($v.Id) $($v.Version)** — $($v.License) ($($v.Scope))") }
}
if ($review.Count -gt 0) {
    $md.Add("")
    $md.Add("## Требуют ручной проверки (NOASSERTION)")
    foreach ($v in $review) { $md.Add("- $($v.Id) $($v.Version) — источник: $($v.LicenseKind)") }
}
$md | Set-Content -Encoding utf8 $mdPath

# CycloneDX 1.5
function ConvertTo-CdxComponent {
    param($R)
    $lic = if ($R.License -eq "NOASSERTION") {
        @(@{ expression = "NOASSERTION" })
    } elseif ($R.License -match "OR|AND|\(|\)") {
        @(@{ expression = $R.License })
    } else {
        @(@{ license = @{ id = $R.License; name = $R.License } })
    }
    $purlType = if ($R.Source -eq "nuget") { "nuget" } else { "generic" }
    $component = @{
        type = "library"
        name = $R.Id
        version = $R.Version
        purl = "pkg:$purlType/$($R.Id)@$($R.Version)"
        licenses = [object[]]$lic
        properties = @(
            @{ name = "smetana:scope"; value = $R.Scope }
            @{ name = "smetana:license-status"; value = $R.Status }
        )
    }
    if ($R.Note) { $component.description = $R.Note }
    return $component
}

$cdx = @{
    bomFormat = "CycloneDX"
    specVersion = "1.5"
    version = 1
    metadata = @{
        timestamp = $timestamp
        tools = [object[]]@(@{ vendor = "SmetaNA"; name = "deploy/new-sbom.ps1" })
        component = @{ type = "application"; name = "SmetaNA"; version = (Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw | Select-String -Pattern "<Version>([^<]+)</Version>").Matches[0].Groups[1].Value }
    }
    components = [object[]]@($results | ForEach-Object { ConvertTo-CdxComponent $_ })
}
$cdxPath = Join-Path $OutputDir "sbom.cdx.json"
$cdx | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $cdxPath

Write-Host "SBOM: $($results.Count) компонентов; нарушений: $($violations.Count); на ручную проверку: $($review.Count)"
Write-Host "Файлы: $cdxPath, $csvPath, $mdPath"

if ($violations.Count -gt 0) {
    Write-Host "НАРУШЕНИЯ ПОЛИТИКИ ЛИЦЕНЗИЙ:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  - $($_.Id) $($_.Version): $($_.License)" -ForegroundColor Red }
    exit 2
}
exit 0
