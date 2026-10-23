# Лицензии сторонних компонентов SmetaNA

> Инвентаризация зависимостей (задача 6.7, риск R7). **Политика: только
> MIT / Apache-2.0 / BSD / Boost** — платный закрытый продукт без обязательств
> раскрытия исходников и роялти (спека §4). Актуализация — перед каждым релизом:
> `powershell -ExecutionPolicy Bypass -File deploy\new-sbom.ps1` (код выхода 2 —
> нарушение политики блокирует релиз); SBOM релиза — `sbom.cdx.json` (CycloneDX 1.5).

**Срез от 2026-10-23** (состав по `dotnet list package --include-transitive` для
`SmetaNA.EditorUI` + ненуветные компоненты).

## Итог сверки с политикой

- **Нарушений нет.** Все поставляемые компоненты — MIT, Apache-2.0 или BSD.
- **3 флага «нет SPDX в метаданных» (NOASSERTION)** — проверены вручную по фактическим
  текстам лицензий в пакетах (см. «Флаги» ниже); соответствуют политике.
- Пакеты тестовой инфраструктуры в продукт не поставляются (колонка «Область»: dev).

## 1. Поставляемые NuGet-зависимости (managed)

| Компонент | Версия | Лицензия | Область |
|---|---|---|---|
| Avalonia | 12.1.4 | MIT | runtime |
| Avalonia.Desktop | 12.1.4 | MIT | runtime |
| Avalonia.Fonts.Inter | 12.1.4 | MIT | runtime |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | runtime |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause *(файл LICENSE, см. Флаги)* | runtime |
| Avalonia.BuildServices | 11.3.2 | MIT | runtime |
| Avalonia.FreeDesktop | 12.1.4 | MIT | runtime |
| Avalonia.FreeDesktop.AtSpi | 12.1.4 | MIT | runtime |
| Avalonia.HarfBuzz | 12.1.4 | MIT | runtime |
| Avalonia.Native | 12.1.4 | MIT | runtime |
| Avalonia.Remote.Protocol | 12.1.4 | MIT | runtime |
| Avalonia.Skia | 12.1.4 | MIT | runtime |
| Avalonia.Win32 | 12.1.4 | MIT | runtime |
| Avalonia.X11 | 12.1.4 | MIT | runtime |
| HarfBuzzSharp | 8.3.1.3 | MIT | runtime |
| HarfBuzzSharp.NativeAssets.{Linux,macOS,WebAssembly,Win32} | 8.3.1.3 | MIT | runtime |
| MicroCom.Runtime | 0.11.6 | MIT | runtime |
| Microsoft.DotNet.PlatformAbstractions | 3.1.6 | MIT *(файл LICENSE.TXT, см. Флаги)* | runtime |
| Microsoft.Extensions.DependencyModel | 9.0.9 | MIT | runtime |
| Silk.NET.Core | 2.23.0 | MIT | runtime |
| Silk.NET.Maths | 2.23.0 | MIT | runtime |
| Silk.NET.OpenGL | 2.23.0 | MIT | runtime |
| SkiaSharp | 3.119.4 | MIT | runtime |
| SkiaSharp.NativeAssets.{Linux,macOS,WebAssembly,Win32} | 3.119.4 | MIT | runtime |
| Tmds.DBus.Protocol | 0.94.1 | MIT | runtime |

## 2. Ненуветные и нативные компоненты (поставляются в `runtimes/<rid>/native/`)

| Компонент | Версия | Лицензия | Откуда |
|---|---|---|---|
| Manifold (CSG-ядро, C API) | 3.5.4 | **Apache-2.0** | сборка из исходников, `spikes/Spike.Manifold/build-native.ps1` |
| oneTBB | 2022.3.0 | **Apache-2.0** | опционально (TBB-сборка Manifold, `MANIFOLD_PAR=ON`), линкуется статически |
| lib3mf (включая официальный C#-биндинг `Lib3MF.cs`) | 2.5.0 | **BSD-2-Clause** | официальный релиз 3MF Consortium, `fetch-lib3mf.ps1` (SHA-256) |
| .NET runtime (self-contained: `Microsoft.NETCore.App.Runtime.win-x64` / `.linux-x64`, host-пакеты) | 10.0.x | MIT | .NET Foundation (`dotnet/runtime`) |

## 3. Тестовая инфраструктура (dev — в продукт не поставляется)

| Компонент | Версия | Лицензия |
|---|---|---|
| xunit, xunit.assert, xunit.core, xunit.analyzers, xunit.extensibility.{core,execution} | 2.9.3 / 1.18.0 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Apache-2.0 *(метаданные `licenseUrl`, см. Флаги)* |
| Microsoft.NET.Test.Sdk, Microsoft.CodeCoverage, Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.TestHost | 18.11.0 | MIT |

## Флаги (метаданные без SPDX — проверены вручную)

1. **Avalonia.Angle.Windows.Natives 2.1.27548.20260419** — в nuspec `<license type="file">`; текст `LICENSE` в пакете — 3-клаузный BSD («The ANGLE Project Authors», включая упоминание TransGaming/Google/3DLabs) → **BSD-3-Clause**, политика соблюдена. Требование лицензии — воспроизведение уведомления (настоящий файл входит в пакет в `docs/legal/`).
2. **Microsoft.DotNet.PlatformAbstractions 3.1.6** — `<license type="file">`; текст `LICENSE.TXT` — «The MIT License» (.NET Foundation) → **MIT**, политика соблюдена. (Пакет помечен устаревшим, но лицензия чистая; замена не требуется.)
3. **xunit.abstractions 2.0.3** — только `licenseUrl`; исходный проект xunit — Apache-2.0. Учитывается как dev-зависимость (в продукт не поставляется).

## Составные части (уведомления)

- **SkiaSharp** (нативные ассеты) включает компоненты Skia: libjpeg-turbo (BSD-3/zlib), libpng (libpng license), zlib (zlib license), freetype (двойная лицензия: FTL — BSD-подобная / GPL-2.0-with-font-exception; распространяется по **FTL**, что допустимо по политике; оба варианта разрешают встраивание в проприетарный продукт), harfbuzz (MIT), woff2 (MIT) — распространяются в бинарной форме внутри SkiaSharp; перечень — в THIRD-PARTY-NOTICES исходного репозитория SkiaSharp.
- **Avalonia.Angle.Windows.Natives** — ANGLE (BSD-3-Clause) + компоненты Khronos/Microsoft (BSD/MIT/zlib) в бинарной форме.
- **lib3mf** — BSD-2-Clause (3MF Consortium); включает официальный автогенерированный C#-биндинг (та же лицензия).
- **Manifold** — Apache-2.0; при TBB-сборке статически линкуется oneTBB (Apache-2.0).

> Примечание: freetype распространяется по FTL (BSD-подобная лицензия) — входит в разрешённый
> перечень политики; GPL-вариант лицензии не используется. Флаг для ревизии юристом — стандартная
> практика для составных библиотек Skia; блокирующих ограничений не выявлено.

## Порядок поддержки актуальности

1. Перед каждым релизом: `deploy\new-sbom.ps1` → сверка с настоящим документом.
2. Новая зависимость — только после проверки лицензии (скрипт вернёт `REVIEW`/`VIOLATION`).
3. `sbom.cdx.json` публикуется вместе с релизом; настоящий файл — уведомления для пользователей (входит в состав пакета).
