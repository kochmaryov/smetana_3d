# SmetaNA.Manifold — CSG-бэкенд на Manifold (модуль M6, задача 5.3)

Булевы операции (объединение/разность/пересечение) для mesh-first ядра через
**Manifold 3.5.4** C API (`MANIFOLD_CBIND`, Apache-2.0). Код перенесён из спика 0.4
(`spikes/Spike.Manifold/` — источник истины по P/Invoke-подводным камням; сам спик не изменяется).

Гарантия R2 (спека §4, §6): Manifold гарантирует manifold-результаты; валидность,
genus, объём и V−E+F проверяются фузи-тестами `tests/SmetaNA.Manifold.Tests`.

## API бэкенда

| Тип | Роль |
|---|---|
| `ManifoldSolid` | Управляемая обёртка над `ManifoldManifold*` (`IDisposable` + финализатор): статус (`ManifoldError`), `IsEmpty`, `Genus`, `Volume`, `SurfaceArea`, счётчики V/E/F, примитивы `Cube`/`Sphere`/`FromSdf`, булевы `Union`/`Difference`/`Intersection`/`Boolean`, `Translate`, явная `Materialize` |
| `ManifoldBridge` | Мост HalfEdgeMesh/MeshData ↔ Manifold: `ToSolid` (ingest, MeshGL64), `ToMeshData` (readback), `ToHalfEdgeMesh` |
| `CsgOperations` | API фазы 5: `Union`/`Difference`/`Intersection`/`Boolean` — принимает и возвращает `HalfEdgeMesh` (`SmetaNA.Core.Geometry`); отложенные варианты `*Deferred` — строят только CSG-дерево |
| `BooleanOp`, `ManifoldError`, `ManifoldException`, `CsgStage`, `CsgProgress` | Операции, статусы C API, диагностика, прогресс конвейера |

Точность — double (MeshGL64): Manifold внутри всегда double; float-путь (MeshGL) из спика
намеренно не перенесён — он дешевле по трафику, но для геометрического ядра теряет точность.

### Отложенная материализация («отложенные булевы», spike README)

`manifold_union/difference/intersection` только строят CSG-дерево (≈ 0 мс); реальная работа —
при первой eager-операции, после чего результат кешируется нативно. Контракт:

* `ManifoldSolid.IsMaterialized == false` — результат ещё не вычислен;
* `Materialize(progress, cancellationToken)` — **явная** материализация (главная дорогая фаза);
* `ToMeshData`/`ToHalfEdgeMesh`/`Volume`/`Genus` и др. — eager-операции: материализуют автоматически;
* прогресс — `CsgProgress(Stage, Fraction)`, стадии `Ingest → BuildTree → Materialize → Readback → RebuildMesh`;
* отмена — `CancellationToken` проверяется между стадиями и в циклах readback; сам нативный
  вызов материализации — единый вызов без прерывания. Отмена *внутри* нативной фазы (и прогресс
  по чанкам) — через `ExecutionContext` C API 3.5.x; в переносе из спика не используется,
  задел на будущее (см. «Риски»).

Так UI-слою удобно материализовать результат осознанно: прогресс/отмена коррелируют с реальной работой.

## Перенос памяти и колбэков (из спика 0.4)

1. **Память.** Все конструкторы C API — placement-new в буфер вызывающего: сначала
   `manifold_alloc_*()` (сырое хранилище `::operator new`), затем конструктор с этим `mem`;
   освобождение — строго парой `manifold_delete_*()` (деструктор + `::operator delete`).
   Пары инкапсулированы в `ManifoldSolid : IDisposable` (+ финализатор) и внутри
   `ManifoldBridge` (промежуточные MeshGL64 создаются и уничтожаются в вызове).
   Массивы чтения (`*_vert_properties`, `*_tri_verts`) библиотека **копирует memcpy-ом в наш
   буфер** (`copy_data`) — владения не передаёт; буферы выделяются `NativeMemory` и освобождаются в finally.
2. **Колбэки.** `ManifoldSdf` (level-set) — `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate`;
   делегат обязан жить весь вызов (`GC.KeepAlive` в finally), иначе GC может убрать thunk.
   Используется только `manifold_level_set_seq` — параллельная версия зовёт колбэк из нативных
   рабочих потоков (для рантаймов с lock'ом опасно). Конвенция LevelSet: **положительное**
   значение SDF — внутри тела.
3. **ABI.** `size_t` → `nuint`, индексы MeshGL64 → `ulong`, структуры по значению, всё — cdecl.
   Enum-значения `ManifoldError`/`ManifoldOpType` совпадают с `types.h`.
4. **Валидность.** `manifold_status() == NoError` — меш замкнут (2-манифолд); ошибки входа не
   роняют процесс, а возвращаются статусом (`NotManifold`, `VertexIndexOutOfBounds`, …).
   Пустой меш (0 вершин, 0 треугольников) — валидный *пустой* манифолд (NoError);
   < 4 треугольников — `NotManifold`. Пустой результат булевой операции (например, разность
   совпадающих тел) — корректный пустой `HalfEdgeMesh`.
5. **Точность.** MeshGL64 (double) обязателен для геометрического ядра; обход треугольников — CCW снаружи.

## Нативные библиотеки

Раскладка — NuGet-style `runtimes/<rid>/native/` (сборка копирует файлы в корень выходного
каталога рядом с assembly: `DllImport("manifoldc")` находит их автоматически):

| Платформа | Файлы | Откуда |
|---|---|---|
| `runtimes/win-x64/native/` | `manifoldc.dll` + `manifold.dll` | `fetch-native.ps1` копирует из `spikes/Spike.Manifold/native/win-x64` |
| `runtimes/linux-x64/native/` | `libmanifoldc.so` + `libmanifold.so` | собираются на Linux (см. ниже), кладутся сюда |

**Бинарники в git не коммитятся** (это же правило в README/скрипте; отслеживаемый `.gitignore` не трогаем).
`fetch-native.ps1` добавляет `src/SmetaNA.Manifold/runtimes/` в `.git/info/exclude` (локальное
git-исключение). Первая сборка:

```
powershell -ExecutionPolicy Bypass -File spikes\Spike.Manifold\build-native.ps1
powershell -ExecutionPolicy Bypass -File src\SmetaNA.Manifold\fetch-native.ps1
```

### Сериальная и TBB-сборка

* **Эталонные DLL** (`runtimes/win-x64/native`, скопированы из спика) — **сериальные**
  (`MANIFOLD_PAR=OFF`): детерминированная воспроизводимая база тестов и замеров.
* **TBB (`MANIFOLD_PAR=ON`, oneTBB) — рекомендация для продакшена**: замерено в спике ускорение
  CSG ≈ 2× на 8 логических ядрах (union ~100k треуг.: 130 → 64 мс). Сборка — те же флаги CMake,
  плюс `-DMANIFOLD_PAR=ON` (oneTBB 2022.3.0 тянется/линкуется статически); положить результат
  в `runtimes/win-x64/native/` (или `native/win-x64-tbb` спика + `fetch-native.ps1 -Tbb`).
  Имя в `DllImport` не меняется. После переключения — перезамерить на реальных мешах фазы 5.

### Linux x64 — путь сборки

На целевом дистрибутиве (собирать на самом старом поддерживаемом — glibc):

```
cmake -S manifold-3.5.4 -B build -DMANIFOLD_CBIND=ON -DMANIFOLD_PAR=OFF \
      -DMANIFOLD_TEST=OFF -DMANIFOLD_PYBIND=OFF -DBUILD_SHARED_LIBS=ON
cmake --build build --config Release --target manifoldc
```

Источник — официальный release-тарболл `manifold-3.5.4.tar.gz` (как в
`spikes/Spike.Manifold/build-native.ps1`). Результат — `libmanifoldc.so` + `libmanifold.so`
(обязательно обе: `manifoldc` динамически зависит от `manifold`) → `runtimes/linux-x64/native/`.
P/Invoke-код кроссплатформенен, различаются только имена файлов (`manifoldc` / `libmanifoldc`
подставляет рантайм).

## Сборка и тесты

```
C:\Users\mnk\.dotnet\dotnet.exe build src\SmetaNA.Manifold\SmetaNA.Manifold.csproj
C:\Users\mnk\.dotnet\dotnet.exe test tests\SmetaNA.Manifold.Tests\SmetaNA.Manifold.Tests.csproj
```

Проекты намеренно не добавлены в `SmetaNA.slnx` — это делает координатор волны.

## Фузи-тесты (митигация R2)

`tests/SmetaNA.Manifold.Tests` (xunit): фиксированные сценарии (кубы: round-trip, объёмы 2.0 /
7.5 / 0.5 точно; сфера+куб: замкнутость, genus=0; тор: genus=1) и фузи-прогоны с фиксированным
seed (случайные размеры/сдвиги кубов, возмущения вершин, сфера+куб при разных смещениях).
Инварианты каждого результата: `manifold_status == NoError`, watertight, отсутствие дефектов
(`MeshValidator`), V−E+F = 2−2·genus, объём (включая A∪B + A∩B = A + B, A∖B = A − A∩B) и
согласованность объёма с формулой дивергенций по треугольникам. Плюс диагностика невалидного
входа (`NotManifold`, без падения), отложенная материализация (прогресс/отмена) и замеры
ingest/CSG/readback на сферах ~50k треугольников.

## Риски / задел

* Отмена и прогресс *внутри* нативной фазы — через `ExecutionContext` C API 3.5.x
  (`IsCancelled(ctx)` есть в 3.5.4): требует дополнительного P/Invoke-переноса и тестов.
* `manifold_status()`/`manifold_num_tri()` на отложенном дереве сами его материализуют (lazy eval
  C API) — поэтому `IsMaterialized`/`Materialize` — единственная точка явного контроля.
* Обновление Manifold — только с пересборкой обоих нативных файлов и прогоном фузи-тестов.
