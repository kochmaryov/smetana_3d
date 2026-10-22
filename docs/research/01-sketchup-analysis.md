> Исследовательский материал к спеке [3d-editor.spec.md](../specs/3d-editor.spec.md). Дата: 2026-10-22.

# SketchUp как источник требований для десктопного 3D-редактора — исследовательский отчёт

**Краткое резюме.** SketchUp — polygonal-моделлер с уникальной связкой «минимум инструментов + inference-привязки + push/pull», экосистемой (Ruby API, Extension Warehouse, 3D Warehouse, Trimble Connect) и веткой версий Free/Go/Pro/Studio + Web/iPad/образование. Проверенные слабости (нет истории/параметрики, хрупкие solids, отсутствие филлетов/шелла, проблемы STL-экспорта и больших моделей, подписочная модель, недоступность покупки из РФ) формируют готовую нишу для продукта «та же простота + CAD-точность + 3D-печать-готовность + Linux + разовая лицензия». Факты сверялись по официальной справке help.sketchup.com, тарифной странице Trimble, форуму SketchUp Community и RU-обзорам; недоступные агрегаторы отзывов (G2, Capterra, TrustRadius, PCMag, All3DP — 403) помечены как разрыв.

---

## 1. Полный перечень функционала SketchUp

### 1.1 Линейка продуктов и платформ (статус 2026)

| Версия | Что входит |
|---|---|
| **SketchUp Free** | SketchUp for Web в браузере, Trimble Connect 10 ГБ (personal), 3D Warehouse; экспорт — только STL, PNG-печать с водяным знаком; без импорта файлов, без Outliner, без создания стилей/материалов, Solid Tools — только Outer Shell, колаборация 5 пользователей/1 проект |
| **SketchUp Go** | Премиум Web + SketchUp for iPad, безлимит Trimble Connect, 100 AI Credits/мес, Live Components, «advanced import/export» |
| **SketchUp Pro** | Десктоп (Windows/macOS) + LayOut (2D-документация), PreDesign, «более 1 000 расширений», IFC и DWG |
| **Pro Scan / Pro Civil Contractor / Pro Advanced Workflows** | Профессиональные аддоны (облака точек, строительная выноска, Revit-воркфлоу) |
| **SketchUp Studio** | Всё из Pro + V-Ray (только Windows), Scan Essentials (только Windows), Revit Importer, Site Contractor, 200 AI Credits/мес |
| **SketchUp for Schools** | K-12: браузер и iPad, бесплатно через аккаунты Google Workspace for Education / Microsoft Education, файлы в Google Drive/OneDrive |
| **Higher Education / Universities / Network Lab** | Платные образовательные планы, включая floating-лицензии для компьютерных классов |

Платформы десктопа: **Windows 11 и macOS (Apple silicon); Linux-версии нет**. Поддерживаются версии 2025–2027, «SketchUp 2024 и старше не поддерживаются». Web работает в любом браузере (Trimble ID), iPad требует iPadOS 17+.

### 1.2 Инструменты рисования и редактирования

Main Toolbar (12 базовых): Select, Eraser, Paint Bucket, Pencil/Line, Rectangle, Push/Pull, Move, Rotate, Scale, Tape Measure, Orbit, Pan. Expanded Toolset: Lasso, Sample Material, Circle, Polygon, 2-/3-Point Arc, Arc, Pie, Freehand, Rotated Rectangle, 3D Text, Offset, Follow Me, Flip, Dimension, Protractor, Axes, Text Label, Create Section, Tag, Zoom/Zoom Extents/Zoom Window, Position Camera, Look Around, Walk, AI Render; панель кастомизируется (слоты, разделители).

**Solid Tools**: Outer Shell, Intersect, Union, Subtract, Trim, Split (в Web Free — только Outer Shell). **Точность**: Tape Measure/Protractor создают направляющие (guides), Measurements box (VCB) — числовой ввод «почти с любым инструментом». **Прочее**: Soften/Smooth/Hide geometry, Section Planes (активный разрез на контекст, Section Fill, «Create Group from Slice», экспорт среза в DWG), аннотации — Text, Labels, Dimensions, 3D Text; группы и Outliner-иерархия; секущие планы, стили, сцены.

### 1.3 Система inference/привязок

SketchUp «отображает несколько типов inference: point, linear, shape», комбинирует их в «complex inference»; у компонентов и Dynamic Components — свои типы. Point-типы: Origin, Endpoint, Midpoint, Intersection, On Face, On Edge, Center (центр круга/дуги/полигона), Intersection with Hidden Section; linear inference «прилипает вдоль линии/направления» с временной пунктирной подсказкой. Новое (2025+): **Snaps** — пользовательские точки с Orientation/Alignment для сборки объектов (прилипание snap-to-snap, к граням/рёбрам, режим Stamp, «tractor beam» Alt/Command) — фактически примитив сборочных соединений.

### 1.4 Группы, компоненты, Dynamic Components

Компонент = definition + instance: правка definition меняет все instances, scale/rotate/flip — нет; есть Make Unique, Replace All, вложенность в Outliner. **Dynamic Components** — атрибуты + значения (числа, формулы, функции: math/logical/text/trig/onclick), ограничение размеров, snapping к размерам; справка честно называет создание DC «одной из самых сложных задач в SketchUp». **Live Components** — конфигурируемые параметрические компоненты (подписка/веб). Правило SketchUp: «если используешь объект больше одного раза — делай компонент» (компоненты легче групп для производительности).

### 1.5 Теги, стили, сцены, материалы, свет, геолокация

**Tags** (бывшие слои): видимость в один клик, папки тегов, Tag Tool (Sample/Replace All/Tag All Instances), Color by Tag, пунктир, материал на тег. **Scenes** сохраняют камеру, скрытую геометрию, видимые теги, активные секущие планы, стиль, туман, тени, окружение, оси — и анимируются в презентацию. **Материалы/текстуры**: Paint Bucket, Sample Material, свои материалы, позиционирование текстур, PBR-материалы/окружения (новый графический движок с SketchUp 2024: DirectX 12 / Metal 2). **Солнце/геолокация**: тени по широте/долготе, ориентации осей и часовому поясу (слайдеры Time/Date, Light/Dark, On Faces/On Ground); Add Location, Site Context (подписка), импорт DEM-terrain, экспорт CRS (Trimble Geodetic Library).

### 1.6 Импорт/экспорт форматов

- **SKP** — нативный; **DWG/DXF** — импорт (arcs, faces, layers/tags, polylines, nested blocks, splines, hatches и др.; настройки тесселяции: Angular Deviation, Chordal Tolerance, Max Edge Length; «файлы >15MB могут тормозить»; типичные проблемы: «lines may not connect to form faces», потеря масштаба) и экспорт 2D/3D с выбором версии AutoCAD;
- **IFC** — импорт/экспорт IFC2x3 и IFC4 (Tessellation/Body, CRS); «некоторые SketchUp-сущности могут не экспортироваться»;
- **STL** — импорт/экспорт для 3D-печати (Binary/ASCII, единицы мм по умолчанию, Swap YZ, экспорт только выделенного);
- **OBJ** — только у Pro (+.mtl, «flat hierarchy» вместо дерева, только полигоны, без NURBS);
- **glTF/GLB** — импорт/экспорт с PBR-картами; **USD/USDZ** — все типы USD, round-trip (камеры↔Scenes, HDR/EXR-окружения, гео-координаты);
- легаси и прочее: **3DS, DAE (Collada), KMZ, FBX, XSI, VRML** (отдельные статьи справки); в образовательной версии экспорт: 3ds, usdz (iPad-only), dae (web-only), dwg/dxf, fbx (web-only), kmz (web-only), obj, pdf, stl;
- **изображения**: импорт JPEG/PNG/TIFF/BMP/PSD/PDF, экспорт тех же + EPS; PDF-вывод через LayOut («printing/exporting your document»); PNG из Web Free — с водяным знаком.

### 1.7 Экосистема и большие модели

**Extension Warehouse** — «сотни бесплатных и платных» расширений (только десктоп); **Ruby API + Ruby Console**, developer.sketchup.com, форум разработчиков (официальной поддержки API нет); **3D Warehouse** (лимиты загрузок Free: 1 500/день, 15 000/мес; платно: безлимит/день, 25 000/мес); **Trimble Connect** (10 ГБ personal у Free, безлимит у подписок, real-time просмотр/комментарии). Для больших моделей официальные советы: «keep your model light», Fast Styles, отключать тени/туман, скрывать тегами, компоненты вместо копий, Purge Unused; геометрия далеко от начала координат ухудшает производительность.

**Источники (раздел 1):**
- SketchUp Help Center — категории: https://help.sketchup.com/en/
- SketchUp (десктоп-обзор): https://help.sketchup.com/en/sketchup/sketchup
- SketchUp for Web — инструменты: https://help.sketchup.com/en/sketchup-web/sketchup-web-main-toolbar и https://help.sketchup.com/en/sketchup-web/sketchup-web-expanded-toolbar
- Free vs подписка в Web: https://help.sketchup.com/en/sketchup-web/web-features
- Inference и рисование: https://help.sketchup.com/en/sketchup/introducing-drawing-basics-and-concepts
- Точность (guides/VCB): https://help.sketchup.com/en/sketchup/measuring-angles-and-distances-model-precisely
- Snaps: https://help.sketchup.com/en/sketchup/snaps
- Компоненты: https://help.sketchup.com/en/sketchup/components ; Dynamic Components: https://help.sketchup.com/en/sketchup/making-dynamic-component
- Tags: https://help.sketchup.com/en/sketchup/controlling-visibility-tags ; Scenes: https://help.sketchup.com/en/sketchup/creating-scenes
- Section Planes: https://help.sketchup.com/en/sketchup/slicing-model-peer-inside
- Тени: https://help.sketchup.com/en/sketchup/casting-real-world-shadows
- STL/3D-печать: https://help.sketchup.com/en/sketchup/importing-and-exporting-stl-files-3d-printing и https://help.sketchup.com/en/sketchup/3d-printing-model
- CAD: https://help.sketchup.com/en/sketchup/importing-and-exporting-cad-files ; IFC: https://help.sketchup.com/en/importing-and-exporting-ifc-files ; OBJ: https://help.sketchup.com/en/sketchup/exporting-obj-files ; glTF: https://help.sketchup.com/en/sketchup/working-gltf-files ; USD: https://help.sketchup.com/en/sketchup/working-usdz-files ; изображения: https://help.sketchup.com/en/sketchup/importing-and-exporting-image-files ; экспорт образовательной версии: https://help.sketchup.com/en/sketchup-education/exporting-model
- Ruby API: https://help.sketchup.com/en/sketchup/developing-tools-sketchup-ruby-api-and-console ; Extension Warehouse: https://help.sketchup.com/en/extension-warehouse/extension-warehouse
- Производительность: https://help.sketchup.com/en/sketchup/improving-performance ; требования: https://help.sketchup.com/en/sketchup/system-requirements
- Планы: https://help.sketchup.com/en/admin/subscriptions, https://help.sketchup.com/en/admin/sketchup-free, https://help.sketchup.com/en/admin/sketchup-go, https://help.sketchup.com/en/admin/sketchup-studio

---

## 2. Сильные и слабые стороны (SWOT S/W)

### Сильные стороны (S)

1. **Простота входа.** 12 инструментов на главной панели, числовой ввод, inference-подсказки; пользователи: «It's relatively easy to learn if you dedicate some time» (форум, topic 20340); RU-обзор: «интуитивный интерфейс — понятный даже новичку».
2. **Inference-система.** Точность «на глаз»: комбинированные привязки (endpoint/midpoint/perpendicular-направления) + guides + VCB — скорость без CAD-дисциплины.
3. **Компоненты как ООП.** Definition/instance, наследование правок, Make Unique, вложенность, атрибуты DC — проектирование «библиотеками деталей», что напрямую ложится на 3D-печать (многократные детали) и промдизайн (семейства изделий).
4. **Универсальность.** Официально: «здания, мебель, интерьеры, ландшафты и многое другое»; Follow Me/Offset/Freehand дают органические формы, LayOut — чертежи, V-Ray/Studio — визуализация. Библиотека 3D Warehouse — крупнейшая коллекция готовых моделей (заявлено в RU-обзоре; цифры — **непроверено**).
5. **Экосистема.** Ruby API, «более 1 000 расширений» (по тарифной странице Pro), платные/бесплатные плагины, общество форума как поддержка.
6. **Мультиплатформенность и облако.** Win/Mac/Web/iPad + Trimble Connect-синхронизация, комментарии в реальном времени.

### Слабые стороны (W)

1. **Нет истории/параметрики.** Параметрика — только через Dynamic Components («одна из самых сложных задач») или плагины (форум: пользователи дорабатывают «parametric.rb» от Trimble, ищут «SU Draw Parametric Shapes»). Нет feature-tree → правки «взад» дороги.
2. **Нет нормальных филлетов/шелла.** Поток жалоб «fillet»: «tried several techniques and all failed», вопросы про филлет отверстий, сложных стыков — всё через дуги/сторонние расширения.
3. **Хрупкие boolean/солиды.** Пользователи: «Solid Inspector says the component is 'shiny clean', yet Entity Info does not show it as a solid and solid tools indicate it isn't»; «sketchup says the larger object isnt a solid even though solid inspector says its perfect». Официальный гайд по 3D-печати требует «solid group» и рекомендует **сторонний** Solid Inspector 2 для починки — фактическое признание проблемы.
4. **Качество STL-экспорта, непечатные модели.** Треды форума: ошибки в слайсере после экспорта, «missing faces on my stl file», «bottom opens up and the openings become solids», «messed up» в Meshmixer; лечится ручной чисткой internal faces.
5. **Точность и обмен с CAD.** После импорта DWG «size or scale may be inconsistent», «lines may not connect to form faces»; OBJ — плоская иерархия и только полигоны; IFC — «некоторые сущности могут не экспортироваться».
6. **Производительность на больших моделях.** Треды 2023–2024: «significant performance issues… slow rendering and frequent crashes», «very slow orbiting ultimately with a locked viewport» после обновления до 2024; официальный ответ — «keep the model light», т.е. ограничения архитектуры (всё рисуется в реальном времени).
7. **Цена/подписка и урезанные бесплатные версии.** Только подписки (named-user), Web Free без импорта, STL-only экспорт, водяной знак; Studio фактически Windows-only (V-Ray, Scan Essentials).
8. **Санкции для РФ.** «Due to US or EU Sanctions, Trimble is unable to accept orders from Russia, Ukraine, or Belarus» (цитата Trimble в посте форума, topic 200196, 03.06.2022: «My license of SketchUp Pro will be expired 14 July. After that my company will not be able to use SketchUp Pro anymore»); SketchUp for iPad «not availble» в RU store (topic 198318, 12.05.2022, «I dont want use VPN and another accounts - cheating things»). Локализация RU-версии с багами («10x» превращается в «10ч» на русской раскладке — треды форума).

**Источники (раздел 2):**
- SketchUp Community Forum, поиск «STL export 3D printing problems»: https://forums.sketchup.com/search?q=STL+export+3D+printing+problems
- Поиск «large model slow performance»: https://forums.sketchup.com/search?q=large+model+slow+performance
- Поиск «fillet»: https://forums.sketchup.com/search?q=fillet ; «Solid Inspector»: https://forums.sketchup.com/search?q=Solid%20Inspector ; «parametric»: https://forums.sketchup.com/search?q=parametric
- Санкции/РФ: https://forums.sketchup.com/t/200196 (topic 200196) и https://forums.sketchup.com/t/198318 (topic 198318)
- 3D Printing a Model (требования к solid, Solid Inspector 2): https://help.sketchup.com/en/sketchup/3d-printing-model
- Improving Performance: https://help.sketchup.com/en/sketchup/improving-performance
- vc.ru «SketchUp — где скачать и как установить» (интуитивность, Make, форматы): https://vc.ru/id5118112/2108301-skachat-i-ustanovit-sketchup-dlya-windows-i-macbook
- Wikipedia, SketchUp (история: @Last Software → Google 2006 → Trimble): https://en.wikipedia.org/wiki/SketchUp

---

## 3. Модель лицензирования и цен (2024–2026)

**Модель.** Только подписки (named-user: «каждая подписка — за конкретного человека»); perpetual-лицензий нет (упоминание «SketchUp Make» как бесплатного десктопа в RU-статьях 2025–2026 — устаревшая информация, **непроверено** в части актуальности). Есть пробная версия Pro/Studio без карты, AI Credits как платный аддон.

**Проверенные цены** (тарифная страница Trimble, годовая оплата, USD, «не включает налоги и НДС», «оплата различается по регионам»):
- **Go** — $10,75/мес за пользователя при годовой оплате (~$129/год): Web + iPad, безлимит Trimble Connect;
- **Pro** — $33,25/мес (~$399/год): десктоп + LayOut + PreDesign + 1000+ расширений + IFC/DWG;
- **Studio** — $71,58/мес (~$859/год): всё из Pro + Revit, облака точек, V-Ray;
- **Pro Scan** и **Pro Civil Contractor** — $41,58/мес; **Pro Advanced Workflows** — $49,92/мес.
- Ежемесячные тарифы и цены в других валютах — **непроверено** (страница отдаёт только годовые значения в SSR).

**Образование.** SketchUp for Schools (K-12) — **бесплатно** для школ с Google Workspace for Education или Microsoft Education; ограничения: только браузер/iPad, хранение в Google Drive/OneDrive вместо Trimble Connect, **нет AI-инструментов, нет инструментов коллаборации, нет Live Components**, Autoshape «limited», только admin-управляемые аккаунты. Higher Education/Universities — платные: **$50 / $45 / $40 за пользователя в год** при 10–50 / 51–200 / 201–500 местах, свыше 500 — договорная цена; **сетевой лабораторный floating-пакет — $30 за пользователя в год**.

**Россия.** Проверенные факты (2022, форум): Trimble не принимает заказы из России/Украины/Беларуси «из-за санкций США и ЕС» (topic 200196); SketchUp for iPad «не доступен» в российском App Store (topic 198318); пользователи упоминают обходы через VPN и чужие аккаунты, но это «cheating things». Прямых инструкций по оплате из РФ в открытых источниках нет; из формулировки Trimble следует, что легальная покупка подписки для РФ-компаний заблокирована (следствие — **непроверено** для 2026 года). На 2026 г. сохраняется русская локализация сайта (sketchup.trimble.com/ru-ru) с ценами в USD. **Мотивация искать альтернативу:** подписка вместо владения + санкционная недоступность + отсутствие Linux-версии + обесценивание купленных подписок после их истечения.

**Источники (раздел 3):**
- Планы и цены (RU): https://sketchup.trimble.com/ru-ru/plans-and-pricing
- Subscription Plans: https://help.sketchup.com/en/admin/subscriptions ; Free: https://help.sketchup.com/en/admin/sketchup-free ; Go: https://help.sketchup.com/en/admin/sketchup-go ; Studio: https://help.sketchup.com/en/admin/sketchup-studio
- Образование: https://help.sketchup.com/en/sketchup-education и https://help.sketchup.com/en/sketchup-education/accessing-sketchup-education
- Ограничения Schools vs Web/iPad: https://help.sketchup.com/en/sketchup-education-platform-comparison
- Санкции РФ: https://forums.sketchup.com/t/200196 ; https://forums.sketchup.com/t/198318
- История лицензирования (Google→Trimble, free web-версия): https://en.wikipedia.org/wiki/SketchUp

---

## 4. Выводы: что перенять и чем победить

### Обязательно перенять

1. **Push/Pull как центральная метафора** — «нарисовал контур → вытянул»; это ядро SketchUp-UX, вокруг которого строится всё остальное.
2. **Inference-привязки** (endpoint/midpoint/center/on-face/perpendicular/parallel + complex inference) **+ числовой ввод (VCB) + guides** — та же точность, что в CAD, но без диалогов.
3. **Компоненты-как-ООП**: definition/instance, наследование изменений, Make Unique, вложенность, атрибуты (аналог Dynamic Components) — сразу закрывают задачи «семейства деталей» (промдизайн) и «многократные детали» (3D-печать).
4. **Минимум инструментов — максимум задач**: 12 базовых + раскрывающийся набор + кастомизируемая панель + поиск команд.
5. **Сборочные Snaps** — точки привязки/ориентации в компонентах; для печатных и корпусных изделий это «скрытый функционал», который SketchUp добавил только недавно.
6. **Группы/контекст + теги + сцены** как способ управлять сложностью и видимостью (и заодно производительностью).
7. **Экосистема**: открытый API (аналог Ruby API) + маркетплейс расширений + облачная библиотека моделей.

### Слабости SketchUp → наше преимущество

| Боль SketchUp | Наше решение |
|---|---|
| Нет истории/параметрики | Feature tree/параметрические размеры и ограничения из коробки |
| Нет филлетов/шелла | Родные Fillet/Chamfer/Shell как операции (не плагины) |
| Хрупкие solids, «не solid» без объяснений | Гарантия manifold + встроенный диагности/авто-repair + честные boolean |
| STL «рвётся», непечатные модели | «3D-print ready» пайплайн: валидация толщин/нормалей/пересечений перед экспортом |
| Тормоза на больших моделях | Instancing/LOD, многопоточный рендер, честные лимиты |
| CAD-обмен теряет масштаб/геометрию | Импорт/экспорт с отчётом о потерях и контролем единиц |
| Подписка, санкции, нет Linux | Разовая/учебная лицензия, оплата из РФ, официальный Linux (Win+Linux — наш таргет) |

**Позиционирование для школьников/студентов:** бесплатный или разово-платный десктопный оффлайн-редактор с простотой SketchUp, но с гарантией «модель напечатается» — ниша, которую SketchUp фактически отдаёт плагинам (Solid Inspector) и сторонним слайсерам. SketchUp for Schools (бесплатно для K-12) — главный конкурент по цене; ответить можно офлайном, Linux, отсутствием привязки к Google/Microsoft-аккаунтам и более строгим CAD-ядром.

**Источники (раздел 4):** синтез источников разделов 1–3; ключевые: https://help.sketchup.com/en/sketchup/3d-printing-model, https://help.sketchup.com/en/sketchup/making-dynamic-component, https://forums.sketchup.com/search?q=Solid%20Inspector, https://sketchup.trimble.com/ru-ru/plans-and-pricing, https://forums.sketchup.com/t/200196.

### Разрывы и оговорки
- **Непроверено:** ежемесячные тарифы, цены 2024–2025 в ретроспективе (страницы истории цен недоступны), актуальность ситуации с покупкой из РФ на 2026 год (данные по санкциям — 2022 г.), количество моделей в 3D Warehouse, поддержка PDF-экспорта напрямую из десктопа (подтверждён PDF в LayOut и в edu-списке).
- **Недоступные источники** (403/412): G2, Capterra, TrustRadius, PCMag, All3DP, AlternativeTo, Softonic — обзоры агрегаторов не сверены; выводы о «хвальбе/раздражении» опираются на официальные документы, форум SketchUp Community и RU-обзор vc.ru.
- **Поисковики** (DuckDuckGo, Mojeek, searx, Bing) блокировались или отдавали мусор; основной метод — прямые fetch по известным URL и sitemap help.sketchup.com (771 статья).
