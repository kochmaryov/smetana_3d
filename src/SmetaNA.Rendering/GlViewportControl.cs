using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.VisualTree;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using Silk.NET.OpenGL;

namespace SmetaNA.Rendering;

/// <summary>Снимок статистики вьюпорта для HUD и смоук-прогонов.</summary>
public sealed record ViewportStats(
    double Fps,
    double FrameMs,
    double AverageFps,
    double MinBucketFps,
    long FramesTotal,
    int TriangleCount,
    int EdgeCount,
    int ObjectCount,
    double GridCellMm,
    bool GlReady,
    bool GlEverReady,
    string GlInfo);

/// <summary>
/// GL-вьюпорт редактора: контекст и FBO даёт Avalonia (<see cref="OpenGlControlBase"/>),
/// GL-вызовы идут через Silk.NET поверх <c>GlInterface.GetProcAddress</c> — путь спика
/// (spikes/Spike.Gl/README.md). Управление мышью: ЛКМ — орбита/пик, Shift+ЛКМ или
/// средняя кнопка — панорама, колесо — зум; собственный frame pacing
/// (<see cref="TargetFramesPerSecond"/>).
/// </summary>
public sealed class GlViewportControl : OpenGlControlBase
{
    private const double OrbitSpeed = 0.01; // рад/пиксель
    private static readonly long HoverIntervalTicks = Stopwatch.Frequency / 25;

    private enum DragMode
    {
        None,
        Orbit,
        Pan,
    }

    private GL? _gl;
    private GlSceneRenderer? _renderer;
    private string _glInfo = "GL: инициализация…";
    private bool _glEverReady;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _prevFrameTime;
    private double _lastRenderTime;
    private double _firstFrameTime;
    private double _lastFrameTime;

    private long _framesTotal;
    private double _fps;
    private double _frameMs;
    private double _minBucketFps = double.MaxValue;
    private double _bucketStart;
    private int _bucketFrames;
    private bool _warmupDone;

    private Scene? _scene;
    private SceneRenderModel? _model;
    private RenderBatch? _batch;
    private float[] _gridLines = [];
    private float[] _axisLines = [];
    private double _gridCellMm = 1000;
    private bool _gpuDirty = true;
    private bool _autoZoomPending = true;

    private DragMode _dragMode = DragMode.None;
    private Point _lastPointer;
    private Point _pressPoint;
    private bool _dragMoved;
    private long _lastHoverTicks;

    public GlViewportControl()
    {
        Focusable = true;
    }

    /// <summary>Камера вьюпорта; подключена к мыши.</summary>
    public Camera Camera { get; } = new();

    /// <summary>Сцена для рендера; установка перестраивает модель сцены.</summary>
    public Scene? Scene
    {
        get => _scene;
        set
        {
            if (ReferenceEquals(_scene, value))
            {
                return;
            }

            _scene = value;
            _autoZoomPending = true;
            RefreshScene();
        }
    }

    public RenderMode RenderMode { get; set; } = RenderMode.Shaded;

    public bool ShowGrid { get; set; } = true;

    public bool ShowAxes { get; set; } = true;

    /// <summary>Лимит кадров/с (свой frame pacing); 0 — без лимита.</summary>
    public double TargetFramesPerSecond { get; set; } = 60;

    /// <summary>Выбранный объект/группа или null.</summary>
    public SceneNode? SelectedObject { get; private set; }

    /// <summary>Объект под курсором или null.</summary>
    public SceneNode? HoveredObject { get; private set; }

    /// <summary>Шаг сетки, мм (после перестройки сцены).</summary>
    public double GridCellMm => _gridCellMm;

    public event EventHandler? SelectionChanged;

    public event EventHandler? HoverChanged;

    /// <summary>
    /// Перестроить модель сцены (CPU-батчи) из <see cref="Scene"/>.
    /// Вызывать с UI-потока: модель используется и мышиной обработкой.
    /// </summary>
    public void RefreshScene()
    {
        SceneRenderModel? model = _scene is null ? null : SceneRenderModel.Build(_scene);
        _model = model;
        _batch = model is null ? null : SceneBatcher.Build(model.Objects);

        if (model is not null)
        {
            _gridLines = GridFactory.CreateGridLines(model.Bounds, out _gridCellMm);
            _axisLines = GridFactory.CreateAxisLines(model.Bounds, out _);
            if (_autoZoomPending && !model.Bounds.IsEmpty)
            {
                Camera.ZoomExtents(model.Bounds, 1.1);
                _autoZoomPending = false;
            }
        }
        else
        {
            _gridLines = [];
            _axisLines = [];
        }

        if (SelectedObject is not null && !IsInModel(SelectedObject))
        {
            SelectedObject = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (HoveredObject is not null && !IsInModel(HoveredObject))
        {
            HoveredObject = null;
            HoverChanged?.Invoke(this, EventArgs.Empty);
        }

        _gpuDirty = true;
        RequestNextFrameRendering();
    }

    /// <summary>Вписать модель в кадр.</summary>
    public void ZoomExtents(double margin = 1.1)
    {
        if (_model is { Bounds.IsEmpty: false })
        {
            Camera.ZoomExtents(_model.Bounds, margin);
        }
    }

    /// <summary>Стандартный вид (ориентация камеры; масштаб сохраняется).</summary>
    public void SetView(StandardView view) => Camera.SetView(view);

    public void ClearSelection()
    {
        if (SelectedObject is null)
        {
            return;
        }

        SelectedObject = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Снимок статистики для HUD и отчётов.</summary>
    public ViewportStats GetStats() => new(
        _fps,
        _frameMs,
        AverageFps,
        MinFps,
        _framesTotal,
        _model?.TriangleCount ?? 0,
        _model?.EdgeCount ?? 0,
        _model?.Objects.Count ?? 0,
        _gridCellMm,
        _gl is not null,
        _glEverReady,
        _glInfo);

    /// <summary>Средний FPS с начала прогона (без первого кадра).</summary>
    public double AverageFps
    {
        get
        {
            double span = _lastFrameTime - _firstFrameTime;
            return span > 0 && _framesTotal > 1 ? (_framesTotal - 1) / span : 0;
        }
    }

    /// <summary>Минимальный FPS по односекундным бакетам после прогрева.</summary>
    public double MinFps => _warmupDone && _minBucketFps < double.MaxValue ? _minBucketFps : 0;

    protected override void OnOpenGlInit(GlInterface glInterface)
    {
        try
        {
            GL api = GL.GetApi(name => glInterface.GetProcAddress(name));
            _gl = api;

            string version = GlSceneRenderer.GetStringSafe(api, StringName.Version);
            string rendererName = GlSceneRenderer.GetStringSafe(api, StringName.Renderer);
            string glsl = GlSceneRenderer.GetStringSafe(api, StringName.ShadingLanguageVersion);
            (bool isEs, int major, int minor) = GlSceneRenderer.ParseVersion(version);

            _renderer = new GlSceneRenderer(api, isEs, major, minor, out bool usedFallback);
            _glInfo = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{rendererName} | {version} | GLSL {glsl} | {(isEs ? "GLES" : "GL")}{major}.{minor}{(usedFallback ? " | fallback shaders" : string.Empty)}");
            _gpuDirty = true;
            _glEverReady = true;
        }
        catch (Exception ex)
        {
            _glInfo = "GL ERROR: " + ex.Message;
            _renderer = null;
            _gl = null;
        }
    }

    protected override void OnOpenGlRender(GlInterface glInterface, int fb)
    {
        if (_renderer is not { } renderer)
        {
            return;
        }

        double now = _clock.Elapsed.TotalSeconds;
        if (TargetFramesPerSecond > 0 && _framesTotal > 0)
        {
            // Свой frame pacing: дожидаемся кадрового интервала перед отрисовкой.
            // Thread.Sleep на коротких интервалах спит по системному тику (~15.6 мс)
            // и сбивает лимит (замерено: ~38 FPS при цели 60), поэтому крупную часть
            // интервала держим yield-циклом, хвост — спином.
            double deadline = _lastRenderTime + (1.0 / TargetFramesPerSecond);
            while (true)
            {
                double remaining = deadline - _clock.Elapsed.TotalSeconds;
                if (remaining <= 0)
                {
                    break;
                }

                if (remaining > 0.004)
                {
                    Thread.Sleep(0);
                }
                else
                {
                    Thread.SpinWait(64);
                }
            }

            now = _clock.Elapsed.TotalSeconds;
        }

        double dt = _prevFrameTime <= 0 ? 1.0 / 60.0 : Math.Clamp(now - _prevFrameTime, 0.0, 0.25);
        _prevFrameTime = now;
        _lastRenderTime = now;
        UpdateStats(now, dt);

        // Размер FBO = Bounds × RenderScaling — та же формула, что у OpenGlControlBase.
        double scaling = this.GetPresentationSource()?.RenderScaling ?? 1.0;
        int width = Math.Max(1, (int)(Bounds.Width * scaling));
        int height = Math.Max(1, (int)(Bounds.Height * scaling));
        Camera.AspectRatio = (double)width / height;

        if (_gpuDirty)
        {
            if (_batch is not null)
            {
                renderer.UploadMesh(_batch);
            }

            renderer.UploadGrid(_gridLines);
            renderer.UploadAxes(_axisLines);
            _gpuDirty = false;
        }

        float[] mvp = GlMatrix.ToGlMatrix(Camera.ViewProjection);
        float[] mv = GlMatrix.ToGlMatrix(Camera.ViewMatrix);
        renderer.Draw(
            width,
            height,
            mvp,
            mv,
            RenderMode,
            _batch,
            HoveredObject,
            SelectedObject,
            ShowGrid,
            ShowAxes);

        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface glInterface)
    {
        _renderer?.Dispose();
        _renderer = null;
        if (_gl is { } api)
        {
            api.Dispose();
        }

        _gl = null;
        _gpuDirty = true;
        base.OnOpenGlDeinit(glInterface);
    }

    protected override void OnOpenGlLost()
    {
        _gl = null;
        _renderer = null;
        _gpuDirty = true;
        _glInfo = "GL: контекст потерян";
        base.OnOpenGlLost();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPoint point = e.GetCurrentPoint(this);
        _pressPoint = point.Position;
        _lastPointer = point.Position;
        _dragMoved = false;

        if (point.Properties.IsMiddleButtonPressed ||
            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _dragMode = DragMode.Pan;
        }
        else if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
        {
            _dragMode = DragMode.Orbit;
        }
        else
        {
            return;
        }

        Focus();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode == DragMode.Orbit && !_dragMoved && e.InitialPressMouseButton == MouseButton.Left)
        {
            SelectAt(e.GetPosition(this));
        }

        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        Vector delta = position - _lastPointer;
        _lastPointer = position;

        switch (_dragMode)
        {
            case DragMode.Orbit:
                MarkDragMoved(position);
                Camera.Orbit(-delta.X * OrbitSpeed, delta.Y * OrbitSpeed);
                e.Handled = true;
                break;
            case DragMode.Pan:
                MarkDragMoved(position);
                double worldPerPixel = WorldPerPixel();
                Camera.Pan(-delta.X * worldPerPixel, delta.Y * worldPerPixel);
                e.Handled = true;
                break;
            default:
                HoverAt(position);
                break;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Платформы отдают дельту либо «зубьями» (±1), либо в долях WHEEL_DELTA (±120).
        double steps = Math.Abs(e.Delta.Y) > 5 ? e.Delta.Y / 120.0 : e.Delta.Y;
        Camera.Zoom(Math.Pow(1.1, -steps));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            ClearSelection();
            e.Handled = true;
        }
    }

    private void MarkDragMoved(Point position)
    {
        if (Math.Abs(position.X - _pressPoint.X) > 2 || Math.Abs(position.Y - _pressPoint.Y) > 2)
        {
            _dragMoved = true;
        }
    }

    private double WorldPerPixel()
    {
        double height = Math.Max(1.0, Bounds.Height);
        return Camera.Mode == ProjectionMode.Perspective
            ? (2.0 * Camera.Distance * Math.Tan(Camera.FieldOfViewY * 0.5)) / height
            : Camera.OrthoHeight / height;
    }

    private void SelectAt(Point position)
    {
        if (_model is null)
        {
            return;
        }

        Ray3d ray = CameraRay.FromViewportPoint(Camera, position.X, position.Y, Bounds.Width, Bounds.Height);
        PickResult? hit = ScenePicker.Pick(_model.Objects, ray);
        SceneNode? node = hit?.Object.SelectionNode;
        if (ReferenceEquals(SelectedObject, node))
        {
            return;
        }

        SelectedObject = node;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HoverAt(Point position)
    {
        long now = Stopwatch.GetTimestamp();
        if (now - _lastHoverTicks < HoverIntervalTicks)
        {
            return;
        }

        _lastHoverTicks = now;
        if (_model is null)
        {
            return;
        }

        Ray3d ray = CameraRay.FromViewportPoint(Camera, position.X, position.Y, Bounds.Width, Bounds.Height);
        PickResult? hit = ScenePicker.Pick(_model.Objects, ray);
        SceneNode? node = hit?.Object.SelectionNode;
        if (ReferenceEquals(HoveredObject, node))
        {
            return;
        }

        HoveredObject = node;
        HoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsInModel(SceneNode node) =>
        _model is not null && _model.Objects.Any(obj => ReferenceEquals(obj.SelectionNode, node));

    private void UpdateStats(double now, double dt)
    {
        _framesTotal++;
        if (_firstFrameTime <= 0)
        {
            _firstFrameTime = now;
        }

        _lastFrameTime = now;
        double instantaneous = 1.0 / dt;
        _fps = _fps <= 0 ? instantaneous : (_fps * 0.92) + (instantaneous * 0.08);
        _frameMs = dt * 1000.0;

        if (_bucketStart <= 0)
        {
            _bucketStart = now;
            _bucketFrames = 0;
        }

        _bucketFrames++;
        double bucketDuration = now - _bucketStart;
        if (bucketDuration >= 1.0)
        {
            if (now > 2.0)
            {
                _warmupDone = true;
                double bucketFps = _bucketFrames / bucketDuration;
                if (bucketFps < _minBucketFps)
                {
                    _minBucketFps = bucketFps;
                }
            }

            _bucketStart = now;
            _bucketFrames = 0;
        }
    }
}
