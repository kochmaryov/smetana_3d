# Скрипт воспроизводимой сборки нативной библиотеки Manifold (C API) для spike 0.4.
# Что делает: скачивает переносимый CMake 3.31.6 и исходники Manifold 3.5.4,
# собирает manifoldc.dll + manifold.dll (MSVC x64, Release) и копирует их в native\win-x64\.
# Требования: Windows, Visual Studio 2022 Build Tools (MSVC), git (для FetchContent Clipper2), сеть.
# Повторный запуск идемпотентен (загрузки кешируются в build\).
param(
    [string]$ManifoldVersion = "3.5.4",
    [string]$CMakeVersion = "3.31.6"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$build = Join-Path $root "build"
$tools = Join-Path $build "tools"
New-Item -ItemType Directory -Force -Path $tools | Out-Null

# 1. Переносимый CMake (не требует установки в систему).
$cmakeDir = Join-Path $tools "cmake-$CMakeVersion-windows-x86_64"
$cmakeZip = Join-Path $tools "cmake-$CMakeVersion-windows-x86_64.zip"
$cmake = Join-Path $cmakeDir "bin\cmake.exe"
if (-not (Test-Path $cmake)) {
    if (-not (Test-Path $cmakeZip)) {
        $url = "https://github.com/Kitware/CMake/releases/download/v$CMakeVersion/cmake-$CMakeVersion-windows-x86_64.zip"
        Write-Host "Скачиваю CMake ${CMakeVersion}: $url"
        Invoke-WebRequest -Uri $url -OutFile $cmakeZip -UseBasicParsing
    }
    Expand-Archive -Path $cmakeZip -DestinationPath $tools -Force
}

# 2. Исходники Manifold (официальный release-тарболл).
$srcDir = Join-Path $build "manifold-$ManifoldVersion"
$tgz = Join-Path $build "manifold-$ManifoldVersion.tar.gz"
if (-not (Test-Path $srcDir)) {
    if (-not (Test-Path $tgz)) {
        $url = "https://github.com/elalish/manifold/releases/download/v$ManifoldVersion/manifold-$ManifoldVersion.tar.gz"
        Write-Host "Скачиваю Manifold ${ManifoldVersion}: $url"
        Invoke-WebRequest -Uri $url -OutFile $tgz -UseBasicParsing
    }
    tar -xf $tgz -C $build
}

# 3. Окружение MSVC (vcvars64) + configure/build в одном процессе cmd.
$vcvars = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $vcvars)) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vsRoot = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $vcvars = Join-Path $vsRoot "VC\Auxiliary\Build\vcvars64.bat"
}
if (-not (Test-Path $vcvars)) { throw "Не найден vcvars64.bat — установите VS 2022 Build Tools (MSVC)." }

$cmakeExe = "tools\cmake-$CMakeVersion-windows-x86_64\bin\cmake.exe"
$cmakeArgs = "-DMANIFOLD_CBIND=ON -DMANIFOLD_PAR=OFF -DMANIFOLD_TEST=OFF -DMANIFOLD_PYBIND=OFF -DBUILD_SHARED_LIBS=ON"

function Invoke-InMsvcEnv([string]$body) {
    $cmdLine = @("call `"$vcvars`" >nul", "cd /d $build", $body) -join " && "
    # вывод — в консоль мимо pipeline, иначе функция вернёт его вместо кода выхода
    & $env:ComSpec /c $cmdLine | Out-Host
    return $LASTEXITCODE
}

# Параллельный VS-генератор; если установленная VS неизвестна закреплённому CMake
# (например, более новая VS на раннере GitHub Actions), откатываемся на NMake
# из того же MSVC-окружения — он не зависит от версии VS.
$buildDirName = "manifold-build"
$code = Invoke-InMsvcEnv "$cmakeExe -S manifold-$ManifoldVersion -B $buildDirName -G `"Visual Studio 17 2022`" -A x64 $cmakeArgs"
if ($code -ne 0) {
    Write-Host "Генератор Visual Studio 17 2022 недоступен — откат на NMake Makefiles."
    $buildDirName = "manifold-build-nmake"
    $code = Invoke-InMsvcEnv "$cmakeExe -S manifold-$ManifoldVersion -B $buildDirName -G `"NMake Makefiles`" -DCMAKE_BUILD_TYPE=Release $cmakeArgs"
}
if ($code -ne 0) { throw "Конфигурация Manifold завершилась с ошибкой ($code)." }

$buildArgs = if ($buildDirName -eq "manifold-build") { "--config Release --target manifoldc -- /m" } else { "--target manifoldc" }
$code = Invoke-InMsvcEnv "$cmakeExe --build $buildDirName $buildArgs"
if ($code -ne 0) { throw "Сборка Manifold завершилась с ошибкой ($code)." }

$buildDir = Join-Path $build $buildDirName

# 4. Копируем нативные библиотеки в native\win-x64 (рядом с exe при запуске spike).
$outDirs = @(
    $buildDir,
    (Join-Path $buildDir "bin\Release"),
    (Join-Path $buildDir "lib\Release"),
    (Join-Path $buildDir "Release")
)
$nativeOut = Join-Path $root "native\win-x64"
New-Item -ItemType Directory -Force -Path $nativeOut | Out-Null
foreach ($dll in @("manifoldc.dll", "manifold.dll")) {
    $found = $outDirs | ForEach-Object { Join-Path $_ $dll } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        # одноконфигурационные генераторы (NMake) кладут бинарники без подкаталога Release
        $found = Get-ChildItem -Path $buildDir -Recurse -Filter $dll -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $found) { throw "Не найден $dll после сборки (проверьте $buildDir)." }
    Copy-Item $found $nativeOut -Force
    Write-Host "Скопирован: $found -> $nativeOut"
}
Write-Host "Готово. Нативные библиотеки: $nativeOut"
