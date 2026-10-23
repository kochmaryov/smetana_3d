# Spike 0.4 — Manifold C API из C#

Проверка риска «Manifold C API из C#» (фаза 0: план `docs/plans/3d-editor.plan.md`, задача 0.4; спека §4).
Критерий: **CSG через Manifold работает из C#**.

**Статус: ЗЕЛЁНЫЙ** — 32/32 проверок: создание мешей из массивов (куб/сфера/«донат»), булевы
объединение/разность/пересечение (включая точные объёмы), чтение результата обратно в массивы,
нативная валидность (manifold/замкнутость/род поверхности), SDF-колбэк из C#.

## Сборка и запуск

Требования: Windows x64, VS 2022 Build Tools (MSVC), git, .NET SDK 10.0.4xx
(в этой машине: `C:\Users\mnk\.dotnet\dotnet.exe`, SDK 10.0.402 — не в PATH), сеть (для загрузок один раз).

1. **Нативная библиотека** (уже собрана в `native\win-x64\`; воспроизвести заново):
   ```
   powershell -ExecutionPolicy Bypass -File spikes\Spike.Manifold\build-native.ps1
   ```
   Скрипт скачивает переносимый CMake 3.31.6 и исходники Manifold 3.5.4 в `build\` (папка попадает
   под `[Bb]uild/` в .gitignore), собирает `manifoldc.dll` + `manifold.dll` (Release, x64) и копирует
   в `native\win-x64\`.
2. **Сборка spike**: `C:\Users\mnk\.dotnet\dotnet.exe build spikes\Spike.Manifold\Spike.Manifold.csproj`
3. **Запуск**: `C:\Users\mnk\.dotnet\dotnet.exe run --project spikes\Spike.Manifold\Spike.Manifold.csproj --no-build`
   (exit code 0 — все проверки зелёные)

## Как получена нативная библиотека

Выбран путь: **сборка C API из официального release-тарболла** — самый надёжный и воспроизводимый.

| Вариант | Оценка |
|---|---|
| GitHub-релизы Manifold | только исходники (`manifold-3.5.4.tar.gz`), готовых prebuilt-бинарников нет |
| conda-forge / vcpkg | требуют сторонних менеджеров и собственной сборки; готового пакета именно с C API (`manifoldc`) под Windows не найдено |
| ManifoldNET (NuGet) | alpha-статус — не используется, пишем свой тонкий слой (спека §4) |
| **Сборка из исходников (выбрано)** | тарболл с закреплённой версией, флаги детерминированы, повторяемо на Win и Linux |

Версии и лицензии:

| Компонент | Версия | Лицензия |
|---|---|---|
| Manifold (manifold + manifoldc) | 3.5.4 | Apache-2.0 |
| Clipper2 (статически в manifold.dll, backend CrossSection) | коммит `46f6391`, тянется CMake FetchContent | BSL-1.0 |
| CMake (переносимый, без установки в систему) | 3.31.6 | BSD-3-Clause |
| MSVC | 19.44 (VS 2022 BuildTools), Windows SDK 10.0.26100 | — |
| .NET SDK | 10.0.402, runtime 10.0.13 | — |

Флаги CMake: `-DMANIFOLD_CBIND=ON -DMANIFOLD_PAR=OFF -DMANIFOLD_TEST=OFF -DMANIFOLD_PYBIND=OFF -DBUILD_SHARED_LIBS=ON`
(заголовок C API: `bindings/c/include/manifold/manifoldc.h` + `types.h`; `MANIFOLD_CBIND` — CMake-опция,
включена по умолчанию). Clipper2 тянется автоматически при `MANIFOLD_DOWNLOADS=ON` (по умолчанию).

### Подключение в продакшен-сборку (Win + Linux)

- **Windows x64**: положить `manifoldc.dll` + `manifold.dll` рядом с exe или в
  `runtimes/win-x64/native/` (NuGet-раскладка). `DllImport("manifoldc")` найдёт их автоматически.
- **Linux x64**: те же флаги CMake на целевом дистрибутиве → `libmanifoldc.so` + `libmanifold.so`
  в `runtimes/linux-x64/native/`. Собирать на самом старом поддерживаемом дистрибутиве (glibc).
  Обязательно нужны обе библиотеки: `manifoldc` динамически зависит от `manifold`.
- Имя в `DllImport` — `"manifoldc"` (расширение и `lib`-префикс подставляет рантайм), вызовы — cdecl.
- Пины: версия Manifold фиксируется в скрипте сборки; обновление — только через пересборку обоих DLL.

## Результаты (машина: 8 логических ядер, Windows 10.0.26300)

Корректность (все OK): куб round-trip (12 треуг., объём 1.0 точно, V−E+F=2); сфера 16 128 треуг.
(ошибка объёма 0.10% к аналитическому); тор 4 096 треуг. (ошибка 0.80%, **genus=1**, V−E+F=0);
union непересекающихся кубов (объём 2.0 точно); difference кубов (7.5 точно); intersection (0.5 точно);
union куб+сфера (замкнут, genus=0); SDF-колбэк (75 241 вызов, ошибка объёма 0.18%);
float+double ingest (объём 2.0 точно); невалидный меш → `NotManifold` из C API (без падения).

Производительность (минимум из 5 прогонов после прогрева):

| Сценарий | Вход, треуг. | Выход, треуг. | Ingest, мс | CSG (материализация), мс | Readback, мс |
|---|---|---|---|---|---|
| union двух кубов (24+24) | 24 | 24 | 0.03 | 0.02 | 0.00 |
| union двух сфер ~100k | 99 456 | 85 672 | 148.4 | 129.9 | 9.7 |
| difference двух сфер ~100k | 99 456 | 50 264 | 140.1 | 85.4 | 6.1 |
| intersection двух сфер ~100k | 99 456 | 14 856 | 132.6 | 51.3 | 0.5 |
| union сфер ~100k (без пересечения) | 99 456 | 99 456 | 136.7 | 89.3 | 21.0 |

Readback union-сфер (85 672 треуг.): MeshGL64 (double) 9.3 мс, MeshGL (float) 5.8 мс.
Ingest — dominant-cost: построение нативного полу-структурированного представления
(`manifold_of_meshgl64`), а не копирование байтов.

Дополнительно замерена сборка `MANIFOLD_PAR=ON` (oneTBB 2022.3.0, статически, 8 логических ядер) —
те же сценарии, ускорение ≈2×: union ~100k CSG 64 мс (против 130), difference 44 (против 85),
intersection 29 (против 51), ingest пары сфер 77 (против 148). Эталонные DLL в `native\win-x64\` —
сериальные (`MANIFOLD_PAR=OFF`); для продакшена рекомендуется TBB-сборка.

## Подводные камни P/Invoke C API

1. **Память.** Все конструкторы — placement-new в буфер вызывающего (`void* mem`): сначала
   `manifold_alloc_*()` (сырое хранилище `::operator new`), затем конструктор с этим `mem`,
   освобождение — строго парой `manifold_delete_*()` (деструктор + `::operator delete`).
   «Несъедобные» пары (alloc без delete, delete чужого указателя) дают UB/утечки — в C#-слое
   инкапсулировано в `Solid : IDisposable` (+ финализатор). Массивы для чтения (`*_vert_properties`,
   `*_tri_verts`) библиотека **копирует memcpy-ом в наш буфер** (`copy_data`) — владения не передаёт.
2. **Отложенные булевы.** `manifold_union/difference/intersection` только строят CSG-дерево (≈0 мс);
   реальная работа выполняется при первой eager-операции (`manifold_volume`, `manifold_status`,
   `manifold_num_tri`, чтение меша), после чего результат кешируется в объекте. Замеры «времени CSG»
   обязаны включать материализацию, иначе дадут 0.00 мс.
3. **Колбэки.** C API передаёт нативные функциональные указатели (`ManifoldSdf` для level-set,
   `manifold_write_obj` и др.). В C# — `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate`;
   делегат обязан жить весь вызов (пиннинг/GC.KeepAlive), иначе GC может убрать thunk во время
   нативного вызова. Для level-set используйте `manifold_level_set_seq` — параллельная версия зовёт
   колбэк из нативных рабочих потоков (для рантаймов с lock'ом это опасно).
4. **Конвенция LevelSet**: положительное значение SDF — **внутри** тела (не как в классической
   SDF-конвенции «отрицательное — внутри»). Убедительно проверено на сфере.
5. **ABI.** `size_t` → `nuint`, `uint64_t` индексов меша → `ulong` (MeshGL64) или `uint` (MeshGL),
   структуры возвращаются по значению (`ManifoldVec3`, `ManifoldManifoldPair` — по 3/2 указателя);
   все вызовы cdecl. Enum-значения `ManifoldError`/`ManifoldOpType` обязаны совпадать с `types.h`.
6. **Кроссплатформенность.** Один и тот же исходник P/Invoke работает на Win/Linux при наличии
   `manifoldc`+`manifold` в путях загрузки; различаются только имена файлов (`manifoldc.dll` /
   `libmanifoldc.so`) и сборка нативной части (см. выше). Никакой разницы в marshaling нет —
   API 64-битное и кроссплатформенное.
7. **Валидность.** Нативная гарантия: `manifold_status() == NoError` — меш замкнут (2-манифолд),
   `manifold_genus()` — род поверхности, объём/площадь — для перекрёстных проверок. Ошибки входа
   не роняют процесс, а возвращаются статусом (`NotManifold`, `VertexIndexOutOfBounds`, ...).
8. **Точность.** MeshGL (float) дешевле по трафику и достаточно для рендера; MeshGL64 (double)
   обязателен для геометрического ядра — Manifold внутри всегда double.

## Рекомендации для фазы 5 (булевы) и меш-ядра

- **CSG — на Manifold, ответ подтверждён**: обёртка из `Solid`/`MeshData` кладётся в
  `SmetaNA.Core.Geometry` как `ManifoldBackend`; API фазы 5 (union/difference/intersection)
  принимает/возвращает `HalfEdgeMesh`.
- **Конвертация HalfEdgeMesh ↔ Manifold**: `HalfEdgeMesh → MeshData` (позиции double[], индексы
  треугольников, обход CCW) → `Solid.FromMesh`; обратно `Solid.ToMesh()` → повторная сборка
  полу-рёбер. Конвертация дешёвая (~10 мс на 85k треуг.); бутылочное горлышко — не она, а сам CSG
  (~100 мс на 100k треуг. в одну нить) и ingest (~70 мс на 50k треуг.).
- **Отложенность**: в UI-слое материализовать результат осознанно (на первой eager-операции), чтобы
  прогресс/отмена (ExecutionContext, есть в C API 3.5.x) коррелировали с реальной работой.
- **MANIFOLD_PAR=ON (TBB)** для продакшен-сборки ядра: замерено ускорение CSG ≈2× на 8 логических
  ядрах (union 100k: 130 → 64 мс); цифры таблицы — сериальная нижняя оценка. Перезамерить на
  реальных мешах фазы 5.
- **Валидация после CSG**: `manifold_status` + genus + объём — заложить в фузи-тесты фазы 5
  (совпадает с R2 из спеки: гарантия manifold-результатов).
- **Свойства/материалы**: C API хранит per-vertex свойства (`numProp`) — канал для material index
  при экспорте; для spike ограничились xyz.

## Структура

- `Spike.Manifold.csproj` — консольное приложение (net10.0), нативные DLL копируются из `native\win-x64\`.
- `Interop\ManifoldNative.cs` — P/Invoke-объявления (`manifoldc.h` v3.5.4), enum/делегат.
- `Geometry\MeshData.cs`, `Geometry\Solid.cs` — управляемый слой (владение памятью, операции, readback).
- `Geometry\TestMeshes.cs` — тестовые меши из массивов: куб/сфера/тор + аналитические объёмы.
- `Program.cs` — сценарии T1–T10 + бенчмарки; exit code 0 = «зелёный».
- `build-native.ps1` — воспроизводимая сборка нативной части.
- `native\win-x64\` — собранные `manifoldc.dll` + `manifold.dll` (Manifold 3.5.4, Apache-2.0).
- `build\` — загрузки/сборка CMake (кеш, под gitignore; можно удалить).
