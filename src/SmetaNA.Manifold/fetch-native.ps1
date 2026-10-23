# Копирует собранные нативные библиотеки Manifold из спика в раскладку проекта
# (runtimes\<rid>\native\) и добавляет игнор бинарников в .git/info/exclude,
# общий .gitignore не трогает. Бинарники в git НЕ коммитятся — воспроизводятся
# сборкой (см. README.md).
#
# Запуск: powershell -ExecutionPolicy Bypass -File src\SmetaNA.Manifold\fetch-native.ps1
param(
    # Каталог с заранее собранными библиотеками (по умолчанию — native-каталог спика).
    [string]$SourceRoot = "",
    # Режим TBB: искать библиотеки из параллельной сборки MANIFOLD_PAR=ON (продакшен-рекомендация).
    [switch]$Tbb
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $projectDir "..\..")).Path

if ($SourceRoot -eq "") {
    $rid = if ($Tbb) { "win-x64-tbb" } else { "win-x64" }
    $SourceRoot = Join-Path $repoRoot "spikes\Spike.Manifold\native\$rid"
}
if (-not (Test-Path $SourceRoot)) {
    throw "Не найден каталог нативных библиотек: $SourceRoot. Соберите их: powershell -ExecutionPolicy Bypass -File spikes\Spike.Manifold\build-native.ps1 (см. README.md про Linux и TBB)."
}

# Windows: manifoldc.dll + manifold.dll; Linux (при перекрёстном размещении): libmanifoldc.so + libmanifold.so.
$destWin = Join-Path $projectDir "runtimes\win-x64\native"
New-Item -ItemType Directory -Force -Path $destWin | Out-Null
$copied = 0
foreach ($dll in @("manifoldc.dll", "manifold.dll")) {
    $found = Join-Path $SourceRoot $dll
    if (Test-Path $found) {
        Copy-Item $found $destWin -Force
        Write-Host "Скопирован: $found -> $destWin"
        $copied++
    }
}
foreach ($so in @("libmanifoldc.so", "libmanifold.so")) {
    $found = Join-Path $SourceRoot $so
    if (Test-Path $found) {
        $destLinux = Join-Path $projectDir "runtimes\linux-x64\native"
        New-Item -ItemType Directory -Force -Path $destLinux | Out-Null
        Copy-Item $found $destLinux -Force
        Write-Host "Скопирован: $found -> $destLinux"
        $copied++
    }
}
if ($copied -eq 0) {
    throw "В $SourceRoot не найдено ни одной нативной библиотеки (manifoldc.dll / manifold.dll / *.so)."
}

# Игнор бинарников: добавляем в .git/info/exclude (локально для этого worktree,
# отслеживаемый .gitignore не изменяется).
$excludePath = (git -C $repoRoot rev-parse --git-path info/exclude)
if (-not [System.IO.Path]::IsPathRooted($excludePath)) {
    $excludePath = Join-Path $repoRoot $excludePath
}
$excludeDir = Split-Path $excludePath -Parent
New-Item -ItemType Directory -Force -Path $excludeDir | Out-Null
$pattern = "src/SmetaNA.Manifold/runtimes/"
$existing = if (Test-Path $excludePath) { Get-Content $excludePath } else { @() }
if ($existing -notcontains $pattern) {
    Add-Content -Path $excludePath -Value $pattern
    Write-Host "Добавлено в git-исключения ($excludePath): $pattern"
} else {
    Write-Host "Git-исключение уже на месте: $pattern"
}

Write-Host "Готово. Нативные библиотеки: $destWin"
