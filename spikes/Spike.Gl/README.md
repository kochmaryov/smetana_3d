# Spike 0.3 — OpenGL-вьюпорт в Avalonia

**Статус: 🟩 ЗЕЛЁНЫЙ.** Окно Avalonia 12.1.4 с OpenGL-вьюпортом на Silk.NET работает на Windows:
вращаемый куб рендерится в GL-вьюпорте, FPS обновляется в реальном времени (заголовок окна + оверлей).
Linux-валидация отложена (см. раздел «Linux»).

Спик самодостаточный, в решение `SmetaNA.slnx` не добавлен — собирается напрямую по csproj.

## Как запустить

```bash
# сборка (SDK 10.0.402; в этом окружении dotnet не в PATH — полный путь)
C:\Users\mnk\.dotnet\dotnet.exe build spikes\Spike.Gl\Spike.Gl.csproj

# интерактивный запуск (закрытие — крестиком)
spikes\Spike.Gl\bin\Debug\net10.0\Spike.Gl.exe

# GUI-смоук: окно открывается, работает N секунд и закрывается само;
# в stdout печатается сводка [SMOKE] (frames / avgFps / minFps / GL-контекст / result)
spikes\Spike.Gl\bin\Debug\net10.0\Spike.Gl.exe --smoke 10

# то же, но принудительно нативный Windows OpenGL (WGL) вместо дефолтного ANGLE
spikes\Spike.Gl\bin\Debug\net10.0\Spike.Gl.exe --wgl --smoke 10
```

Файлы: [Program.cs](./Program.cs) (запуск, опции, форс WGL), [GlViewport.cs](./GlViewport.cs)
(GL-вьюпорт, шейдеры, статистика), [Mat4.cs](./Mat4.cs) (матрицы), [MainWindow.axaml](./MainWindow.axaml)
(оверлей FPS), [App.axaml](./App.axaml).

## Версии пакетов

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| Avalonia | 12.1.4 | MIT | окно/композитинг (та же версия, что в `src/SmetaNA.EditorUI`) |
| Avalonia.Desktop | 12.1.4 | MIT | Win/Linux платформы |
| Avalonia.Themes.Fluent / Avalonia.Fonts.Inter | 12.1.4 | MIT | тема/шрифт |
| Silk.NET.OpenGL | 2.23.0 | MIT | GL-биндинги для рендерера |

## Как интегрирован GL-контекст и почему

**Путь: гибридный — контекст от Avalonia (`OpenGlControlBase`), вызовы GL через Silk.NET.**

* `GlViewport : Avalonia.OpenGL.Controls.OpenGlControlBase` — Avalonia сама создаёт GL-контекст,
  совместимый со своим композитором (`TryCreateCompatibleGlContextAsync`), FBO и цикл кадров.
* Все GL-вызовы идут через Silk.NET: `GL.GetApi(name => glInterface.GetProcAddress(name))` —
  готовый overload `GL.GetApi(Func<string, IntPtr>)` превращает `GlInterface.GetProcAddress` в полный API Silk.NET.
* Кадровый цикл: `RequestNextFrameRendering()` в конце каждого `OnOpenGlRender` → непрерывная перерисовка.
* Шейдеры: куб рисуется GLSL-парами под профиль контекста — `#version 330 core` (desktop),
  `#version 300 es` (GLES3), `#version 100` (GLES2), fallback `#version 110`. Профиль определяется по строке `GL_VERSION`.
* Z-буфер: FBO Avalonia уже содержит depth-renderbuffer (см. `OpenGlControlResources.UpdateDepthRenderbuffer`
  в исходниках Avalonia) — глубина работает «из коробки», свои буферы приклеивать не нужно.

**Почему не отдельное окно Silk.NET.Windowing/GLFW и не child-window хак:**
нужен вьюпорт *внутри* Avalonia-сцены (панели, оверлеи, один window message loop, DPI-scaling).
Отдельное нативное окно даёт airspace-проблемы и ломает композицию UI. Silk.NET взят именно как
биндинги (MIT, полный GL 4.x API), а не как оконная система.

## Результаты прогона на Windows (23.10.2026)

Машина спика: Intel Arc 140V (16 ГБ), Windows, окно 1024×720.

| Режим | GL-контекст | Кадров / время | avg FPS | min FPS (1-сек бакеты) | Итог |
|---|---|---|---|---|---|
| default (`AngleEgl`) | OpenGL ES 3.0 (ANGLE 2.1.1 → D3D11 32.0.101.8831) | 1165 / 10,17 с | 122,3 | 112,0 | `result=OK` |
| `--wgl` | OpenGL 4.0 desktop (GLSL 4.00, Intel Arc 140V) | 1059 / 10,32 с | 109,7 | 105,9 | `result=OK` |

Первый (холодный) прогон default: 547 кадров, avg 77,3 FPS, min 37,7 — просадка на прогреве
(JIT + компиляция шейдеров + скриншот). После прогрева стабильно >100 FPS.

Наблюдения по скриншотам (делались во время прогона):
* куб с цветными гранями и корректной глубиной — грани перекрывают друг друга правильно;
* заголовок: «Spike.Gl — OpenGL-вьюпорт — FPS 95.2», оверлей: «FPS: 95.2 10.51 ms кадров: 326» + строка GL-инфо;
* два кадра через 1,5 с (кадры 326 → 439): ракурс куба изменился — вращение и кадровый цикл подтверждены;
* окно закрывается само по `--smoke`, процессов-зомби не остаётся.

## FPS и бюджет производительности (DoD §5.6: ≥ 30 FPS на 500k треугольников)

* 30 FPS = 33,3 мс/кадр. Спик держит 8–10 мс/кадр на тривиальной сцене — оверхед самой интеграции
  (композитинг Avalonia + FBO-блит + ANGLE) на уровне единиц миллисекунд и не съедает бюджет.
* Это **не** закрывает требование §5.6: машина спика (Intel Arc 140V) сильно быстрее эталона
  (Intel UHD 620, 8 ГБ, 2018), сцена — 12 треугольников. Замер 500k треугольников на эталоне — задача фазы 1.
* ANGLE добавляет трансляцию GLES→D3D11; на слабом GPU возможны потери на fill-rate.
  В фазе 1 стоит замерить оба пути (`AngleEgl` и `Wgl`) на эталонной конфигурации и взять быстрый.
* FPS > 60: этот путь композитинга не привязан к вертикальной синхронизации (WinUI composition),
  т.е. рендер «на сколько возможно». Для редактора нужен собственный frame pacing (лимит кадров,
  перерисовка по событиям/инерции камеры), иначе простой сцены жжёт батарею.
* `--wgl` выдал контекст 4.0 (дефолт `WglProfiles` — 4.0 и 3.2), хотя драйвер поддерживает 4.6:
  максимальный профиль нужно запрашивать явно.

## Подводные камни интеграции OpenGL в Avalonia 12

1. **Сборка и namespace.** В Avalonia 12 GL-контрол вынесен в `Avalonia.OpenGL.dll`,
   namespace — `Avalonia.OpenGL.Controls` (в 11.x был `Avalonia.OpenGL`).
2. **Поток рендера.** `OnOpenGlRender` вызывается на compositor-потоке (`RequestCompositionUpdate`),
   не на UI. Обновлять `TextBlock`/`Title` напрямую из него нельзя — только через
   `DispatcherTimer`/`Dispatcher.UIThread.Post` (в спике — таймер 4 Гц).
3. **GL живёт только в трёх колбэках.** Контекст current только внутри `OnOpenGlInit/Render/Deinit`;
   Silk.NET `GL` создаётся в `OnOpenGlInit` и не используется снаружи (документация Avalonia прямо
   требует такого режима и указывает `GlInterface.GetProcAddress` как точку интеграции с GL-обёртками).
4. **Viewport задаёт контроль сам.** Avalonia размер FBO считает как `Bounds × RenderScaling`, но
   `glViewport` не вызывает — его нужно вызывать каждый кадр с тем же размером, иначе картинка масштабируется/режется.
5. **Дефолт на Windows — не desktop GL.** `Win32PlatformOptions.RenderingMode` по умолчанию
   `AngleEgl + Software`: контекст приходит как **OpenGL ES 3.0 через ANGLE**. Шейдеры должны быть
   двойными (или детект профиля, как в спике). Форс `Wgl` даёт нативный desktop GL.
6. **Провал инициализации тихий.** Если `OnOpenGlInit` бросает исключение, Avalonia гасит вьюпорт без
   сообщения — инициализацию нужно оборачивать в try/catch и показывать ошибку самому (сделано в спике).
7. **`InvalidateVisual()` в 12 obsolete** — для анимации только `RequestNextFrameRendering()`.
8. **FBO — собственность Avalonia.** Свои color/depth-attachment цеплять не нужно; depth уже есть.
   Свои GL-объекты (VBO/VAO/программа) удаляются в `OnOpenGlDeinit`, потеря контекста обрабатывается
   через `OnOpenGlLost`.
9. **Silk.NET-мост тривиален**, но `GL.GetApi()` без аргументов тянет системный libGL и для
   ANGLE/GLES-контекста не подходит — только `GetApi(Func<string, IntPtr>)` поверх `GlInterface.GetProcAddress`.

## Рекомендация для `src/SmetaNA.Rendering` (фаза 1)

Брать **гибридный путь спика**: `OpenGlControlBase` (контекст/FBO/кадры) + Silk.NET `GL`
через `GL.GetApi(Func<string, IntPtr>)` (весь рендерер пишем на полном API Silk.NET).

* Это единственный путь без airspace-проблем: вьюпорт — полноценный контроль в Avalonia-сцене
  (панели, HUD, DPI-scaling), работает на Win и Linux из одной кодовой базы.
* Ввести тонкую абстракцию «GL-контекст/proc-address provider» (над `GlInterface`), чтобы при
  провале фазы 1 можно было заменить биндинги/бэкенд (запасной Veldrid из спеки §4) без переписывания рендерера.
* Шейдеры сразу держать в двух диалектах (GLSL 330 core + ES 3.00) с детектом профиля;
  GLES2/110-варианты — страховка, для MVP их можно отбросить после валидации Linux.
* Frame pacing делать своим (лимит FPS + перерисовка по событиям камеры), а не полагаться
  на незатухающий `RequestNextFrameRendering`.
* Перед фазой 2 замерить 500k-треугольников на эталоне (Intel UHD 620) в обоих режимах
  (`AngleEgl` и `Wgl`) и зафиксировать выбранный режим по умолчанию.

## Linux

Код кроссплатформенный (net10.0, без Windows-специфичного кода вне `if (OperatingSystem.IsWindows())`
для флага `--wgl`; на Linux используются дефолтные опции X11). **Linux-валидация отложена:**
в этом окружении GUI запустить нельзя, а в CI (.github/workflows/ci.yml) GUI-смоук пока не настроен.
Нужен headless-прогон (xvfb + Mesa/llvmpipe или реальный X11-агент) — вынести в задачу фазы 0/1.
