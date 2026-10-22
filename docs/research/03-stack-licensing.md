> Исследовательский материал к спеке [3d-editor.spec.md](../specs/3d-editor.spec.md). Дата: 2026-10-22.

## Итоги проверки (маркетплейс фактов: C#/.NET+Avalonia, геометрия, Qt/OCCT, web-стек, продажи из РФ)

**Краткое резюме.** Лицензионный контур C#-стека безопасен и перmissive: .NET — MIT, Avalonia — MIT (актуальная версия уже 12.x, не 11), Manifold — Apache-2.0 с C API в дереве, lib3mf — **BSD-2-Clause (не LGPL и не Apache)**, geometry3Sharp — Boost. Главные риски — не лицензии, а зрелость: C#-биндинг Manifold существует только в alpha, OCCT-.NET-обёртки либо без указания лицензии, либо Windows-only (xBIM), G-Shark не обновлялся с 2023. Qt-вариант лицензионно допустим и при LGPLv3, и при коммерческой подписке за 546 €/год; Tauri/Electron-вариант имеет лишь средние прецеденты (Blockbench), серьёзных CAD на web-стеке среди десктопных не найдено. Продажи из РФ: ЮKassa/CloudPayments/Робокасса/Т-Банк подтверждены, Stripe для резидентов РФ закрыт, доступность Paddle/FastSpring — непроверено.

### 1) C#/.NET 8–10 + Avalonia
- Avalonia — MIT (файл `licence.md`, «The MIT License (MIT)», включая право «sublicense, and/or sell») — https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md
- .NET runtime/SDK — MIT (.NET Foundation) — https://raw.githubusercontent.com/dotnet/runtime/main/LICENSE.TXT
- Вопрос про «Avalonia 11» устарел: последний релиз **12.1.4 (09.10.2026)**, репозиторий активен (push 22.10.2026, ~31,7 тыс.★) — https://api.github.com/repos/AvaloniaUI/Avalonia/releases/latest
- Жизненный цикл .NET: **.NET 10 — LTS (11.2025 → 11.2028)**, .NET 8 LTS и .NET 9 STS истекают 10.11.2026 → новый продукт надо сразу строить на .NET 10 — https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- Встроенный OpenGL есть: модуль `src/Avalonia.OpenGL` (каталоги `Controls`, `Composition`, `Egl`, `Surfaces`, `Features`) — https://api.github.com/repos/AvaloniaUI/Avalonia/contents/src/Avalonia.OpenGL?ref=main
- Реальная библиотека GL-вьюпорта: **OpenTKAvalonia** (MIT) — README фиксирует ограничение: на Windows нужно принудительно включить `Win32RenderingMode.Wgl`, иначе контрол вообще не получает события Render; сама библиотека заявлена как «somewhat work-in-progress» — https://github.com/SamboyCoding/OpenTKAvalonia
- Приложение на Avalonia с OpenGL-превью 3D STEP-моделей (LCSC/LCEDA downloader, активно 10.2026) — https://github.com/seishinkouki/lceda_step_downloader
- Avalonia-IDE с полноценным 3D-вьюпортом на OpenGL (custom ECS, MIT) — https://github.com/BAndysc/WoWDatabaseEditor
- Silk.NET — MIT, Veldrid — MIT (push 03.2026); OpenTK — активен, но SPDX в GitHub = NOASSERTION → точная лицензия OpenTK «непроверено» — https://api.github.com/repos/dotnet/Silk.NET/license , https://api.github.com/repos/veldrid/veldrid , https://api.github.com/repos/opentk/opentk
- Итог по Avalonia: готовой «3D-сценографии»/CAD-виджетов нет — viewport пишется сам (OpenGL/Skia/Veldrid); крупных коммерческих CAD на Avalonia в открытых источниках не найдено (поиск GitHub «avalonia opengl» — только утилиты/редакторы/плееры) — непроверено как «прецедент масштаба»

### 2) Геометрия/меш-ядро на C#
- **Manifold — Apache-2.0** — https://github.com/elalish/manifold/blob/master/LICENSE
- C API стабильно существует в самом дереве: опция сборки `MANIFOLD_CBIND=[<OFF>, ON]` — «Build C FFI binding» (elalish/manifold:README.md:119-121) — https://raw.githubusercontent.com/elalish/manifold/master/README.md
- Живые биндинги: Python `manifold3d` (внутренний), Java (CommonWealthRobotics), **C# `ManifoldNET` (внешний)**, Julia, плюс нативный Rust-порт `manifold-rust` (elalish/manifold:README.md:34-47) — там же
- ManifoldNET: в NuGet только версии **1.0.x-alpha (август 2024)**, т.е. стабильного C#-слоя нет — https://www.nuget.org/packages/ManifoldNET
- Производительность: параллелизм через TBB, заявление «dramatically faster» OpenSCAD (это claim автора, не бенчмарк CAD-нагрузок) (elalish/manifold:README.md:78-80) — там же; активность ядра: push 21.10.2026 — https://api.github.com/repos/elalish/manifold
- **geometry3Sharp — Boost Software License 1.0** (BSL-1.0), ~1,9 тыс.★, push 09.01.2026 (точечная активность, без релизного цикла) — https://github.com/gradientspace/geometry3Sharp/blob/master/LICENSE , https://api.github.com/repos/gradientspace/geometry3Sharp
- **GShark = репозиторий GSharker/G-Shark: MIT**, NuGet GShark 2.3.1, но последний push **13.08.2023** → проект заброшен/поддержка вручную — https://github.com/GSharker/G-Shark , https://www.nuget.org/packages/GShark
- **lib3mf — Simplified BSD (BSD-2-Clause), НЕ LGPL и НЕ Apache** — https://github.com/3MFConsortium/lib3mf/blob/master/LICENSE
- lib3mf: C++-реализация, API на C/C++/C#/Java/Python/JS, v2.4.1, Win/Linux/macOS; сторонний код: libzip, zlib, cpp-base64, fast-float (все перmissive) — https://raw.githubusercontent.com/3MFConsortium/lib3mf/master/README.md
- DXF: **IxMilia.Dxf — MIT, активен** (push 28.07.2026) — https://github.com/ixmilia/dxf ; netDxf — MIT, но upstream **в архиве с 2023**, жив форк `netDxf-devel0` — https://github.com/haplokuon/netDxf , https://www.nuget.org/packages/netDxf-devel0
- SharpGLTF — MIT, активен (glTF2) — https://github.com/vpenades/SharpGLTF ; HelixToolkit — MIT, но компоненты WPF/SharpDX, не Avalonia — https://github.com/helix-toolkit/helix-toolkit
- AssimpNet: в GitHub-метаданных **лицензии нет**, последний push 2020 → для закрытого продукта не брать — https://github.com/assimp/assimp-net
- OCCT-.NET: NuGet **Occt.NET (340 тыс. загрузок) вообще без license expression/проекта/владельца** → лицензия неясна, риск — https://www.nuget.org/packages/Occt.NET ; официальный сэмпл `Open-Cascade-SAS/OCCT-samples-csharp` — лицензия NOASSERTION, неактивен с 2023 — https://api.github.com/repos/Open-Cascade-SAS/OCCT-samples-csharp
- XbimGeometry (xBIM): C++/CLI-обёртка над OCCT, сборка Visual Studio/Win10 SDK (**только Windows**), лицензия CDDL, использование OCCT оговорено «clause 6» их лицензии — https://github.com/xBimTeam/XbimGeometry
- Вывод: живой кроссплатформенной поддерживаемой .NET-обёртки OCCT нет; xBIM — Windows-only, остальные — риск по лицензии

### 3) C++/Qt6 + OCCT + OpenGL и прецеденты соло-CAD
- Qt 6 доступен в трёх вариантах: commercial / **LGPLv3** / GPLv3; при этом часть модулей (включая **Qt Quick 3D**) распространяется **только под GPLv3** → для закрытого продукта эти модули требуют коммерческой лицензии — https://doc.qt.io/qt-6/licensing.html
- Актуальные цены Qt for Small Business (страница на 2026): **ADE-SB — 546 €/год ($618)** (desktop+mobile), DCP-SB — 1090 €/год; критерии: компания, выручка/финансирование ≤ 1 млн €/$, не более 3 лицензий, 5 тикетов поддержки/год — https://www.qt.io/development/qt-for-small-business
- Закрытый платный продукт на Qt **LGPLv3 допустим без покупки** (динамическая линковка/возможность релинковки, передача текста лицензии и уведомлений) — https://doc.qt.io/qt-6/licensing.html
- **OCCT — LGPL-2.1 с собственным исключением v1.0**: object-code может включать код из заголовков OCCT «under terms of your choice» при заметном уведомлении; ядро под LGPL-обязательствами — https://github.com/Open-Cascade-SAS/OCCT/blob/master/OCCT_LGPL_EXCEPTION.txt ; ядро активно (push 20.10.2026)
- Dune 3D (соло/малая команда): **C/C++ + GTK4**, ядро — **OpenCASCADE**, решатель — SolveSpace (как библиотека), свой OpenGL-вьюпорт из Horizon EDA; GPL-3.0 — https://github.com/dune3d/dune3d/blob/main/README.md
- SolveSpace: C++, GUI gtkmm3 (GTK3) или Qt6, OpenGL, **собственное геометрическое ядро и решатель**; GPL-3.0, активен (push 21.10.2026) — https://github.com/solvespace/solvespace/blob/master/README.md , https://api.github.com/repos/solvespace/solvespace
- Plasticity (закрытый платный, $175/$299 perpetual + 12 мес обновлений): ядро **Parasolid** + переводчик **HOOPS Exchange** + **xNURBS** — всё коммерческие лицензируемые библиотеки, т.е. «соло-продукт» купил ядро, а не писал — https://www.plasticity.xyz/faq
- Plasticity — готовый образец лицензирования десктопа: токен из письма **Paddle** (MoR), node-locked (2/4 машины), 10 активаций в год, ручной перенос лимита — https://www.plasticity.xyz/faq
- MoI (Moment of Inspiration, Triple Squid Software Design): NURBS-CAD, v4 (64-бит, нативный Mac); язык и устройство ядра — **непроверено** — https://moi3d.com/ , https://moi3d.com/forum/

### 4) Tauri/Rust + Three.js/React или Electron
- Tauri — Apache-2.0 (GitHub license detection), Rust-бэкенд + JS-фронтенд, системный webview, Windows/Linux из одного кода — https://api.github.com/repos/tauri-apps/tauri/license , https://tauri.app/
- three.js — MIT — https://raw.githubusercontent.com/mrdoob/three.js/dev/LICENSE
- Сильный прецедент Electron-3D-редактора: **Blockbench** (GPL-3.0, Electron + web-версия, 6 тыс.★, push 10.2026; плагины официально могут быть проприетарными) — https://github.com/JannisX11/blockbench
- Десктопный параметрический 3D CAD с live-превью на web-стеке: Torrify (GPL-3.0, Electron/TS, OpenSCAD+build123d) — https://github.com/caseyhartnett/Torrify
- Tauri+three.js: попадаются только хобби-проекты (напр. morgan-bevy — редактор уровней Tauri+React+Three.js+Rust) — https://github.com/greysquirr3l/morgan-bevy ; серьёзных CAD-прецедентов на Tauri не найдено
- Численные метрики (RAM на школьных ПК, размер инсталлятора Tauri vs Electron, FPS WebGL на слабом GPU) — **непроверено** (недостоверных цифр приводить не буду)

### 5) Продажи платного десктопа из РФ
- ЮKassa: эквайринг и для **самозанятых**, комиссия **от 0,4% + НДС**, оплата картами/SberPay/СБП/T-Pay, чеки 54-ФЗ, автоплатежи (подписки) — https://yookassa.ru/
- CloudPayments: платежи, **сервис подписок/автосписаний**, облачная касса и чеки 54-ФЗ — https://cloudpayments.ru/
- Robokassa: полноценное API (MerchantLogin, пароли, ResultURL), карты/СБП/электронные способы, фискализация 54-ФЗ — https://docs.robokassa.ru/ru/quick-start
- Т-Банк: торговый эквайринг для **ИП и ООО**; конкретные ставки на странице не извлекаются — «непроверено» — https://www.tbank.ru/business/acquiring/
- **Stripe: список поддерживаемых стран (2026) не содержит России** (проверено по полному списку: США, ЕС, UK, Япония, Бразилия, ОАЭ и др.) → прямой приём для резидента РФ недоступен — https://stripe.com/global
- Paddle как MoR для инди-десктопа работает (пример Plasticity) — https://www.plasticity.xyz/faq ; возможность открыть Paddle/FastSpring на резидента РФ — **непроверено** (страницы help/support не открылись из среды проверки)
- Практика офлайн-активации подтверждена на живом продукте: лицензионный токен + node-lock + лимит активаций в год (Plasticity) — https://www.plasticity.xyz/faq ; SaaS-сервисы ключей (Keygen, Cryptolens) — **непроверено** (домены недоступны из среды)

### Сводная таблица

| Технология | Лицензия | Закрытый платный продукт |
|---|---|---|
| .NET 8/10 | MIT | да |
| Avalonia 12.x | MIT | да |
| Avalonia.OpenGL / Silk.NET / Veldrid | MIT | да |
| OpenTK | NOASSERTION (проверить) | условно |
| Manifold (C API `MANIFOLD_CBIND`) | Apache-2.0 | да |
| ManifoldNET (C#) | alpha, внешний | условно (нестабильно) |
| geometry3Sharp | Boost 1.0 | да |
| G-Shark (NURBS) | MIT | да, но неактивен |
| lib3mf | BSD-2-Clause | да |
| IxMilia.Dxf / SharpGLTF / HelixToolkit | MIT | да |
| netDxf | MIT, архив | да (форк) |
| AssimpNet / Occt.NET | не указана | нет/риск |
| xBIM XbimGeometry (OCCT) | CDDL + OCCT (LGPL-2.1 + exception) | условно (Windows-only) |
| OCCT | LGPL-2.1 + exception | условно (условия LGPL) |
| Qt 6 (LGPLv3-модули) | LGPLv3 | условно (линковка/релинковка) |
| Qt Small Business ADE-SB | 546 €/год | да |
| Qt Quick 3D и др. GPL-модули | GPL-3.0 only | нет без коммерческой Qt |
| SolveSpace / Dune 3D / Blockbench | GPL-3.0 | нет (нельзя включать в закрытый код) |
| Tauri | Apache-2.0 | да |
| three.js | MIT | да |
| Parasolid / HOOPS / xNURBS (Plasticity) | коммерческие | да (за деньги) |
| ЮKassa, CloudPayments, Robokassa, Т-Банк | сервис РФ | да (для РФ-продаж) |
| Stripe | — | нет для резидентов РФ |
| Paddle / FastSpring | MoR | непроверено для РФ |

### Короткий вывод: что лицензионно безопасно
1. **Безопасный набор A:** .NET 10 + Avalonia (MIT) + Manifold (Apache-2.0) + lib3mf (BSD-2) + IxMilia.Dxf/SharpGLTF (MIT) + собственный OpenGL-вьюпорт — всё перmissive, закрытый платный продукт легален; главный риск — инженерный (alpha-биндинг Manifold, свой inference/push-pull).
2. **Набор B (Qt/OCCT):** легален и без покупки Qt (LGPLv3 + динамическая линковка) и с OCCT (LGPL-2.1+exception, уведомление); либо Qt Small Business за 546 €/год. GPL-исходники (SolveSpace/Dune) в закрытый продукт включать нельзя.
3. **Набор C (Tauri+three.js):** лицензии безопасны, но прецедентов серьёзного CAD нет — риск производительности на школьных ПК не подтверждён цифрами.
4. **Продажи:** РФ-часть — ЮKassa/CloudPayments/Robokassa/Т-Банк (54-ФЗ + автоплатежи); экспорт — только через MoR/иностранную структуру (Stripe для РФ закрыт), лицензия — токен + node-lock + лимит активаций (модель Plasticity/Paddle).

**Пробелы:** не проверены Paddle/FastSpring для РФ-продавцов, тарифы Т-Банка, стек MoI, метрики RAM/размера Tauri-дистрибутива, точная лицензия OpenTK и Occt.NET; GitHub MCP-инструменты (`github/get_me`, `search_code`) в среде недоступны — использованы GitHub REST API и прямые fetch.
