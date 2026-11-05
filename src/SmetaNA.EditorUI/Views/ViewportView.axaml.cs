using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.EditorUI.Tools;
using SmetaNA.EditorUI.Tools.Drawing;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI.Views;

/// <summary>
/// Живой GL-вьюпорт: рендер сцены, управление камерой, HUD (FPS/единицы/выделение).
/// GL-контрол — <see cref="GlViewportControl"/> (SmetaNA.Rendering); панели поверх него
/// обновляются таймером, потому что GL-кадры приходят на compositor-потоке
/// (см. spikes/Spike.Gl/README.md, «Поток рендера»).
/// </summary>
public partial class ViewportView : UserControl
{
    private static readonly long ToolHoverIntervalTicks = Stopwatch.Frequency / 25;

    /// <summary>Допуск «клика» правой кнопкой (пикселей) — дальше это орбита.</summary>
    private const double ToolClickTolerancePx = 4.0;

    private DispatcherTimer? _hudTimer;
    private ProjectTabViewModel? _viewModel;
    private ToolController? _tools;
    private Vector2d? _lastPointerPx;
    private readonly HashSet<Key> _heldToolKeys = [];
    private long _lastToolHoverTicks;
    private Point? _toolRightPress;
    private bool _ctrlHeld;
    private bool _shiftHeld;

    /// <summary>Вьюпорт в визуальном дереве (иначе подписки модели не нужны).</summary>
    private bool _isAttached;

    /// <summary>Подписки на модель и инструменты уже установлены.</summary>
    private bool _isModelSubscribed;

    public ViewportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        Viewport.SelectionChanged += OnSelectionChanged;
        Viewport.SelectionChanged += OnSelectionForCursorStrip;
        Viewport.HoverChanged += OnSelectionChanged;

        // Маршрутизация ввода в активный инструмент: туннелирующие обработчики
        // идут раньше камеры вьюпорта и потребляют события, если их взял инструмент.
        AddHandler(InputElement.PointerPressedEvent, OnToolPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnToolPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, OnToolPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnToolKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnToolKeyUp, RoutingStrategies.Tunnel);
    }

    /// <summary>Снимок статистики вьюпорта (для смоук-прогона).</summary>
    internal ViewportStats GetStats() => Viewport.GetStats();

    /// <summary>
    /// Смоук-проверка рисования: два клика и Enter идут тем же путём, что и ввод
    /// мыши (лучи камеры → маршрут указателя → инструмент), и должны добавить узел
    /// в сцену. Ловит разрывы между активацией инструмента и вводом из вьюпорта.
    /// </summary>
    internal bool RunToolDrawingSmoke()
    {
        if (_viewModel is not { } viewModel || _tools is not { } tools)
        {
            Console.WriteLine("[DRAWING] view=unwired result=FAIL");
            return false;
        }

        if (!RegisterDrawingTools.TryActivate(tools, LineTool.ToolId))
        {
            Console.WriteLine("[DRAWING] activate=FAIL result=FAIL");
            return false;
        }

        int nodesBefore = viewModel.Scene.EnumerateNodes().Count();
        long verticesBefore = VerticesInScene(viewModel);
        UpdateToolViewport();

        double width = Math.Max(1.0, Bounds.Width);
        double height = Math.Max(1.0, Bounds.Height);

        Stopwatch clock = Stopwatch.StartNew();
        Stopwatch step = Stopwatch.StartNew();
        bool pressed = tools.RoutePointer(PointerInputFactory.Create(
            width * 0.4, height * 0.55, PointerEventKind.Press, PointerButton.Left, false, false, false));
        long firstPressMs = step.ElapsedMilliseconds;
        step.Restart();
        pressed = pressed && tools.RoutePointer(PointerInputFactory.Create(
            width * 0.6, height * 0.55, PointerEventKind.Press, PointerButton.Left, false, false, false));
        long secondPressMs = step.ElapsedMilliseconds;
        step.Restart();
        bool committed = pressed
            && tools.RouteKey(PointerInputFactory.Key(ToolKey.Enter, false, false, false));
        long commitMs = step.ElapsedMilliseconds;

        int nodesAfter = viewModel.Scene.EnumerateNodes().Count();
        long verticesAfter = VerticesInScene(viewModel);
        tools.Deactivate();

        // GL-контрол должен участвовать в hit-тесте: без собственной отрисовки
        // композитор его пропускает и мышь (камера и инструменты) уходит мимо.
        bool hitTestable = ReferenceEquals(
            this.InputHitTest(new Point(Bounds.Width * 0.5, Bounds.Height * 0.5)),
            Viewport);

        bool ok = committed && verticesAfter > verticesBefore && hitTestable;
        Console.WriteLine(
            $"[DRAWING] pressed={pressed} committed={committed} hitTestable={hitTestable} pressMs={firstPressMs}/{secondPressMs} commitMs={commitMs} totalMs={clock.ElapsedMilliseconds} nodes={nodesBefore}->{nodesAfter} vertices={verticesBefore}->{verticesAfter} result={(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    /// <summary>Сумма вершин всех мешей сцены (рисование пополняет меш или добавляет узел).</summary>
    private static long VerticesInScene(ProjectTabViewModel viewModel)
        => viewModel.Scene.EnumerateNodes()
            .Sum(node => node is SmetaNA.Core.Model.MeshNode meshNode ? meshNode.Mesh.Vertices.Count : 0);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeModel();

        _viewModel = DataContext as ProjectTabViewModel;
        _tools = _viewModel?.Tools;

        SubscribeModel();
        RedrawToolOverlay();
        UpdateCursorCoordinates();
        BindViewport();
    }

    /// <summary>Подписка на модель и инструменты вкладки (идемпотентна).</summary>
    private void SubscribeModel()
    {
        if (_isModelSubscribed || !_isAttached || _viewModel is null)
        {
            return;
        }

        _isModelSubscribed = true;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (_tools is not null)
        {
            _tools.Changed += OnToolStateChanged;
            _tools.Context.UndoStack.Changed += OnSceneHistoryChanged;
        }
    }

    /// <summary>
    /// Снять подписки при откреплении вкладки: иначе неактивные вкладки
    /// продолжают получать события модели и держат GL-вьюпорт в памяти.
    /// </summary>
    private void UnsubscribeModel()
    {
        if (!_isModelSubscribed)
        {
            return;
        }

        _isModelSubscribed = false;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_tools is not null)
        {
            _tools.Changed -= OnToolStateChanged;
            _tools.Context.UndoStack.Changed -= OnSceneHistoryChanged;
        }
    }

    /// <summary>Подключить к GL-контролу сцену и режимы активной вкладки.</summary>
    private void BindViewport()
    {
        if (_viewModel is null)
        {
            return;
        }

        Viewport.Scene = _viewModel.Scene;
        Viewport.TargetFramesPerSecond = EditorOptions.TargetFps;
        SyncViewportFromModel();
    }

    /// <summary>
    /// Применить режимы вьюпорта из модели (шапка окна) к GL-контролу.
    /// Смена проекции пересчитывает вписывание камеры — как это делал
    /// прежний тумблер панели режимов.
    /// </summary>
    private void SyncViewportFromModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        Viewport.RenderMode = _viewModel.SelectedRenderMode.Mode;
        Viewport.ShowGrid = _viewModel.ShowGrid;
        Viewport.ShowAxes = _viewModel.ShowAxes;
        Viewport.IncludeHidden = _viewModel.ShowHiddenGeometry;

        ProjectionMode projection = _viewModel.IsOrthographic
            ? ProjectionMode.Orthographic
            : ProjectionMode.Perspective;
        if (Viewport.Camera.Mode != projection)
        {
            Viewport.Camera.Mode = projection;
            Viewport.ZoomExtents();
        }
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _hudTimer.Tick += OnHudTick;
        _hudTimer.Start();
        SubscribeModel();
        BindViewport();
        ApplyViewportTheme();
        EditorServices.Theme.Changed += ApplyViewportTheme;
        UpdateHud();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        EditorServices.Theme.Changed -= ApplyViewportTheme;
        UnsubscribeModel();
        if (_hudTimer is not null)
        {
            _hudTimer.Stop();
            _hudTimer.Tick -= OnHudTick;
            _hudTimer = null;
        }
    }

    /// <summary>Фон GL-вьюпорта под тему оформления (светлая тема не должна оставлять тёмную рабочую область).</summary>
    private void ApplyViewportTheme()
    {
        if (Application.Current?.TryFindResource("ThemeViewportBackground", out object? value) == true
            && value is Color color)
        {
            Viewport.SetBackground(0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B);
        }
    }

    /// <summary>Ширина вьюпорта задаёт состояние оверлея режимов (адаптив).</summary>
    private void OnHudTick(object? sender, EventArgs e) => UpdateHud();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SyncViewportFromModel();
        UpdateHud();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateHud();

    /// <summary>
    /// Контекстная полоса у курсора (T4): показывается при выбранном объекте,
    /// закрывается при снятии выделения; выбор действия активирует инструмент.
    /// </summary>
    private void OnSelectionForCursorStrip(object? sender, EventArgs e)
    {
        if (CursorStrip is null)
        {
            return;
        }

        // Полоса — только в покое: с активным инструментом она перекрывает
        // ввод (light-dismiss ловит указатель раньше вьюпорта).
        CursorStrip.IsOpen = Viewport.SelectedObject is not null
            && _tools is not { IsActive: true };
    }

    private void OnCursorStripTool(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string commandId })
        {
            _viewModel?.ActivateTool(commandId);
        }

        CursorStrip.IsOpen = false;
    }

    private void OnToolStateChanged()
    {
        // Активация инструмента закрывает контекстную полосу: она не должна
        // мешать вводу инструмента (выдавливание, рисование и т.п.).
        if (CursorStrip is { IsOpen: true } && _tools is { IsActive: true })
        {
            CursorStrip.IsOpen = false;
        }

        RedrawToolOverlay();
        UpdateCursorCoordinates();
    }

    private void OnSceneHistoryChanged() => Viewport.RefreshScene();

    private void OnToolPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_tools is not { IsActive: true } tools || !IsViewportSource(e))
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(Viewport);
        if (!point.Properties.IsLeftButtonPressed)
        {
            // Правый клик (без перетаскивания) завершает фигуру инструмента;
            // перетаскивание правой кнопкой — орбита камеры, см. OnToolPointerReleased.
            if (point.Properties.IsRightButtonPressed)
            {
                _toolRightPress = point.Position;
            }

            return;
        }

        // Shift+ЛКМ — панорама камеры, инструменту не передаётся.
        // Shift+Alt (фиксация плоскости) и Ctrl+Shift (скрытая геометрия) — модификаторы
        // инструмента, панораму не включают.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        UpdateToolViewport();
        SyncHeldModifiers(e.KeyModifiers);
        ApplyTemporaryHiddenGeometry(tools);
        bool handled = tools.RoutePointer(PointerInputFactory.Create(
            point.Position.X,
            point.Position.Y,
            PointerEventKind.Press,
            PointerButton.Left,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt)));
        if (handled)
        {
            e.Pointer.Capture(Viewport);
            e.Handled = true;
            // Инструмент потребил нажатие раньше GlViewportControl, поэтому тот
            // не получит событие и не заберёт клавиатурный фокус себе; без фокуса
            // вьюпорта Enter/Delete уходят в последнюю кнопку палитры, чья
            // переактивация инструмента стирает незавершённую фигуру.
            Viewport.Focus();
        }
    }

    /// <summary>
    /// Событие указателя пришло из самой области GL-вьюпорта, а не из оверлеев
    /// поверх него (панель режимов, «⚙», кнопки видов): оверлеи должны оставаться
    /// кликабельными, пока активен инструмент.
    /// </summary>
    private bool IsViewportSource(PointerEventArgs e)
    {
        Visual? current = e.Source as Visual;
        while (current is not null)
        {
            if (ReferenceEquals(current, Viewport))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private void OnToolPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Point position = e.GetPosition(Viewport);
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            RouteToolRightClick(position);
            return;
        }

        // правый клик уже обработан выше; сюда доходят только отпускания ЛКМ
        if (_tools is not { IsActive: true } tools
            || e.InitialPressMouseButton != MouseButton.Left
            || !IsViewportSource(e))
        {
            return;
        }

        UpdateToolViewport();
        SyncHeldModifiers(e.KeyModifiers);
        ApplyTemporaryHiddenGeometry(_tools);
        bool handled = tools.RoutePointer(PointerInputFactory.Create(
            position.X,
            position.Y,
            PointerEventKind.Release,
            PointerButton.Left,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt)));
        if (handled)
        {
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Правый клик (без перетаскивания) завершает фигуру инструмента — как
    /// задокументировано в инструментах рисования («правый клик завершает»).
    /// Правое перетаскивание остаётся орбитой камеры: клик и драг различаются
    /// по смещению курсора. Событие намеренно не помечается завершённым — камера
    /// должна отпустить захват указателя своим обработчиком.
    /// </summary>
    private void RouteToolRightClick(Point position)
    {
        if (_toolRightPress is not { } press)
        {
            return;
        }

        _toolRightPress = null;
        if (_tools is not { IsActive: true } tools)
        {
            return;
        }

        double dx = position.X - press.X;
        double dy = position.Y - press.Y;
        if ((dx * dx) + (dy * dy) > ToolClickTolerancePx * ToolClickTolerancePx)
        {
            return;
        }

        UpdateToolViewport();
        tools.RoutePointer(PointerInputFactory.Create(
            position.X,
            position.Y,
            PointerEventKind.Press,
            PointerButton.Right,
            false,
            false,
            false));
    }

    private void OnToolPointerMoved(object? sender, PointerEventArgs e)
    {
        Point cursor = e.GetPosition(Viewport);
        _lastPointerPx = new Vector2d(cursor.X, cursor.Y);

        // камера могла повернуться — перепроецировать предпросмотр
        RedrawToolOverlay();

        if (_tools is not { IsActive: true } tools)
        {
            UpdateCursorCoordinates();
            return;
        }

        if (tools.ActiveTool is not { IsInteracting: true })
        {
            long now = Stopwatch.GetTimestamp();
            if (now - _lastToolHoverTicks < ToolHoverIntervalTicks)
            {
                return;
            }

            _lastToolHoverTicks = now;
        }

        Point position = e.GetPosition(Viewport);
        UpdateToolViewport();
        SyncHeldModifiers(e.KeyModifiers);
        ApplyTemporaryHiddenGeometry(tools);
        bool handled = tools.RoutePointer(PointerInputFactory.Create(
            position.X,
            position.Y,
            PointerEventKind.Move,
            PointerButton.None,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt)));
        if (handled)
        {
            e.Handled = true;
        }

        // Подсказки привязок могли измениться без смены предпросмотра.
        RedrawToolOverlay();
        UpdateCursorCoordinates();
    }

    /// <summary>
    /// Обновить координатную строку статус-бара: точка привязки, а без неё —
    /// пересечение луча курсора с плоскостью z = 0.
    /// </summary>
    private void UpdateCursorCoordinates()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_tools?.Snap.Best is { } best)
        {
            _viewModel.SetCursorPoint(best.Point);
            return;
        }

        _viewModel.SetCursorPoint(TryGroundPoint());
    }

    private Vector3d? TryGroundPoint()
    {
        if (_lastPointerPx is not { } pointer || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return null;
        }

        SmetaNA.Rendering.Ray3d ray = CameraRay.FromViewportPoint(
            Viewport.Camera, pointer.X, pointer.Y, Bounds.Width, Bounds.Height);
        if (Math.Abs(ray.Direction.Z) <= 1e-9)
        {
            return null;
        }

        double distance = -ray.Origin.Z / ray.Direction.Z;
        return distance <= 0 ? null : ray.GetPoint(distance);
    }

    private void OnToolKeyDown(object? sender, KeyEventArgs e)
    {
        // Модификаторы отслеживаются и без активного инструмента: Ctrl+Shift —
        // временный показ скрытой геометрии (R-10).
        TrackModifierKey(e.Key, down: true);
        ApplyTemporaryHiddenGeometry(_tools);

        if (_tools is not { IsActive: true } tools)
        {
            return;
        }

        // Автоповтор стрелок/Alt не должен переключать фиксацию много раз.
        if (IsRepeatableToolKey(e.Key) && !_heldToolKeys.Add(e.Key))
        {
            e.Handled = true;
            return;
        }

        ToolKey? key = e.Key switch
        {
            Key.Escape => ToolKey.Escape,
            Key.Enter => ToolKey.Enter,
            Key.Delete => ToolKey.Delete,
            Key.Left => ToolKey.Left,
            Key.Right => ToolKey.Right,
            Key.Up => ToolKey.Up,
            Key.Down => ToolKey.Down,
            Key.LeftAlt or Key.RightAlt => ToolKey.Alt,
            Key.Tab => ToolKey.Tab,
            _ => null,
        };
        if (key is null)
        {
            return;
        }

        bool handled = tools.RouteKey(PointerInputFactory.Key(
            key.Value,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt)));
        if (handled)
        {
            e.Handled = true;
        }
    }

    private void OnToolKeyUp(object? sender, KeyEventArgs e)
    {
        _heldToolKeys.Remove(e.Key);
        TrackModifierKey(e.Key, down: false);
        ApplyTemporaryHiddenGeometry(_tools);
    }

    private void SyncHeldModifiers(KeyModifiers modifiers)
    {
        _ctrlHeld = modifiers.HasFlag(KeyModifiers.Control);
        _shiftHeld = modifiers.HasFlag(KeyModifiers.Shift);
    }

    private void TrackModifierKey(Key key, bool down)
    {
        switch (key)
        {
            case Key.LeftShift or Key.RightShift:
                _shiftHeld = down;
                break;
            case Key.LeftCtrl or Key.RightCtrl:
                _ctrlHeld = down;
                break;
        }
    }

    /// <summary>
    /// Удержание Ctrl+Shift временно включает скрытую геометрию в привязках и
    /// выборе граней; после смены режима привязка под курсором пересчитывается.
    /// </summary>
    private void ApplyTemporaryHiddenGeometry(ToolController? tools)
    {
        if (tools is null)
        {
            return;
        }

        bool hidden = _ctrlHeld && _shiftHeld;
        if (tools.Context.TemporaryIncludeHiddenGeometry == hidden)
        {
            return;
        }

        tools.Context.TemporaryIncludeHiddenGeometry = hidden;
        if (_viewModel is not null)
        {
            _viewModel.IsHiddenGeometryPreview = hidden;
        }

        if (_lastPointerPx is not { } pointer)
        {
            return;
        }

        UpdateToolViewport();
        tools.RoutePointer(PointerInputFactory.Create(
            pointer.X,
            pointer.Y,
            PointerEventKind.Move,
            PointerButton.None,
            _shiftHeld,
            _ctrlHeld,
            false));
        RedrawToolOverlay();
        UpdateCursorCoordinates();
    }

    private static bool IsRepeatableToolKey(Key key)
        => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.LeftAlt or Key.RightAlt;

    /// <summary>Обновить вьюпорт инструментов (лучи/проекции) из камеры GL-вьюпорта.</summary>
    private void UpdateToolViewport()
    {
        if (_tools is not { } tools)
        {
            return;
        }

        Camera camera = Viewport.Camera;
        double width = Math.Max(1.0, Bounds.Width);
        double height = Math.Max(1.0, Bounds.Height);
        tools.Context.Viewport = camera.Mode == ProjectionMode.Perspective
            ? InferenceViewport.CreatePerspective(
                camera.EyePosition, camera.Forward, camera.Up, camera.FieldOfViewY, width, height)
            : InferenceViewport.CreateOrthographic(
                camera.EyePosition, camera.Forward, camera.Up, camera.OrthoHeight, width, height);
    }

    /// <summary>Отрисовка предпросмотра инструмента поверх GL-кадра.</summary>
    private void RedrawToolOverlay()
    {
        ToolOverlay.Children.Clear();
        DrawUserSnaps();

        if (_tools is not { } tools)
        {
            return;
        }

        ToolPreview preview = tools.Preview;
        foreach (ToolPreviewLine line in preview.Lines)
        {
            if (!TryProject(line.Start, out Point start) || !TryProject(line.End, out Point end))
            {
                continue;
            }

            ToolOverlay.Children.Add(new Line
            {
                StartPoint = start,
                EndPoint = end,
                Stroke = BrushFor(line.Style),
                StrokeThickness = line.Style == ToolPreviewStyle.Guide ? 1.5 : 2,
            });
        }

        foreach (ToolPreviewPoint point in preview.Points)
        {
            if (!TryProject(point.Position, out Point pixel))
            {
                continue;
            }

            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = BrushFor(point.Style),
                Stroke = Brushes.White,
                StrokeThickness = 1,
            };
            ToolOverlay.Children.Add(marker);
            Canvas.SetLeft(marker, pixel.X - 4);
            Canvas.SetTop(marker, pixel.Y - 4);
        }

        foreach (ToolPreviewLabel label in preview.Labels)
        {
            if (!TryProject(label.Anchor, out Point pixel))
            {
                continue;
            }

            var text = new TextBlock
            {
                Text = label.Text,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Padding = new Thickness(4, 2),
            };
            ToolOverlay.Children.Add(text);
            Canvas.SetLeft(text, pixel.X + 8);
            Canvas.SetTop(text, pixel.Y + 8);
        }

        DrawInferenceOverlay(tools);
    }

    /// <summary>
    /// Подсказки привязок (W2): не более двух одновременно — лучший кандидат
    /// (маркер + подпись + подсветка ребра) и одно линейное ограничение.
    /// </summary>
    private void DrawInferenceOverlay(ToolController tools)
    {
        SnapResult snap = tools.Snap;
        if (snap.IsEmpty)
        {
            return;
        }

        SnapCandidate? best = snap.Best;
        if (best is not null)
        {
            DrawSnapHighlight(best);
            DrawConstraintLine(best);
        }

        SnapCandidate? linear = snap.Candidates.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, best)
            && candidate.Direction is not null
            && !candidate.IsCombined);
        if (linear is not null)
        {
            DrawConstraintLine(linear);
        }

        if (best is not null)
        {
            DrawSnapMarker(best);
            if (_viewModel?.ShowSnapLabels ?? true)
            {
                DrawSnapLabel(best);
            }
        }
    }

    private void DrawSnapHighlight(SnapCandidate candidate)
    {
        if (candidate.HighlightFace is { } face)
        {
            var ring = new AvaloniaList<Point>();
            foreach (Vector3d vertex in face.Ring)
            {
                if (!TryProject(vertex, out Point vertexPx))
                {
                    return;
                }

                ring.Add(vertexPx);
            }

            if (ring.Count < 3)
            {
                return;
            }

            ring.Add(ring[0]);
            ToolOverlay.Children.Add(new Polyline
            {
                Points = ring,
                Stroke = new SolidColorBrush(SnapColor(candidate.Type)),
                StrokeThickness = 1.8,
                Opacity = 0.8,
            });
            return;
        }

        if (candidate.HighlightSegment is not { } segment)
        {
            return;
        }

        if (!TryProject(segment.Start, out Point start) || !TryProject(segment.End, out Point end))
        {
            return;
        }

        ToolOverlay.Children.Add(new Line
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = new SolidColorBrush(SnapColor(candidate.Type)),
            StrokeThickness = 2.5,
            Opacity = 0.75,
        });
    }

    private void DrawConstraintLine(SnapCandidate candidate)
    {
        if (candidate.Direction is not Vector3d direction)
        {
            return;
        }

        Vector3d origin = candidate.SecondaryPoint ?? candidate.ConstraintLine?.Origin ?? candidate.Point;
        if (!TryProject(origin, out Point originPx)
            || !TryProject(origin + direction.Normalized(), out Point unitPx))
        {
            return;
        }

        var delta = new Vector2d(unitPx.X - originPx.X, unitPx.Y - originPx.Y);
        if (delta.LengthSquared() <= 1e-6)
        {
            return;
        }

        Vector2d screenDirection = delta * (1.0 / delta.Length());
        const double halfLength = 2000;
        var line = new Line
        {
            StartPoint = new Point(originPx.X - screenDirection.X * halfLength, originPx.Y - screenDirection.Y * halfLength),
            EndPoint = new Point(originPx.X + screenDirection.X * halfLength, originPx.Y + screenDirection.Y * halfLength),
            Stroke = new SolidColorBrush(SnapColor(candidate.Type)),
            StrokeThickness = 1.4,
            Opacity = 0.9,
        };

        if (!IsAxis(candidate.Type))
        {
            line.StrokeDashArray = new AvaloniaList<double> { 4, 4 };
        }

        ToolOverlay.Children.Add(line);
    }

    private void DrawSnapMarker(SnapCandidate candidate)
    {
        if (!TryProject(candidate.Point, out Point pixel))
        {
            return;
        }

        IBrush stroke = new SolidColorBrush(SnapColor(candidate.Type));
        switch (candidate.Marker)
        {
            case SnapMarkerKind.Cube:
                var square = new Rectangle
                {
                    Width = 10,
                    Height = 10,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                };
                ToolOverlay.Children.Add(square);
                Canvas.SetLeft(square, pixel.X - 5);
                Canvas.SetTop(square, pixel.Y - 5);
                break;

            case SnapMarkerKind.Circle:
            case SnapMarkerKind.Ring:
            case SnapMarkerKind.LineAndPoint:
                var circle = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                };
                ToolOverlay.Children.Add(circle);
                Canvas.SetLeft(circle, pixel.X - 6);
                Canvas.SetTop(circle, pixel.Y - 6);
                break;

            case SnapMarkerKind.Cross:
                ToolOverlay.Children.Add(new Line
                {
                    StartPoint = new Point(pixel.X - 6, pixel.Y - 6),
                    EndPoint = new Point(pixel.X + 6, pixel.Y + 6),
                    Stroke = stroke,
                    StrokeThickness = 2,
                });
                ToolOverlay.Children.Add(new Line
                {
                    StartPoint = new Point(pixel.X + 6, pixel.Y - 6),
                    EndPoint = new Point(pixel.X - 6, pixel.Y + 6),
                    Stroke = stroke,
                    StrokeThickness = 2,
                });
                break;

            case SnapMarkerKind.Dot:
                var dot = new Ellipse { Width = 7, Height = 7, Fill = stroke };
                ToolOverlay.Children.Add(dot);
                Canvas.SetLeft(dot, pixel.X - 3.5);
                Canvas.SetTop(dot, pixel.Y - 3.5);
                break;

            default:
                // Линейное ограничение рисуется линией; отдельного маркера нет.
                break;
        }
    }

    private void DrawSnapLabel(SnapCandidate candidate)
    {
        if (!TryProject(candidate.Point, out Point pixel))
        {
            return;
        }

        string label = _viewModel?.Strings.GetSnapLabel(candidate.Type) ?? candidate.Type.ToString();
        if (!string.IsNullOrWhiteSpace(candidate.Entity.DisplayName))
        {
            label += Environment.NewLine + candidate.Entity.DisplayName;
        }

        var text = new TextBlock
        {
            Text = label,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(170, 20, 20, 20)),
            Padding = new Thickness(5, 2),
            FontSize = 12,
        };
        ToolOverlay.Children.Add(text);
        Canvas.SetLeft(text, pixel.X + 12);
        Canvas.SetTop(text, pixel.Y - 26);
    }

    /// <summary>
    /// Пользовательские точки сборки (Snap, W5): диск с центральной точкой —
    /// лицевая сторона белая, обратная синяя (видна по знаку скалярного
    /// произведения нормали и направления камеры). Диаметр — постоянный на экране.
    /// </summary>
    private void DrawUserSnaps()
    {
        if (_viewModel is null)
        {
            return;
        }

        const double radiusPx = 11.0;
        foreach (SnapNode snap in SnapQuery.FindInScene(_viewModel.Scene, _viewModel.IsHiddenGeometryPreview))
        {
            SnapFrame frame = snap.WorldFrame(WorldTransformOf(snap));
            if (!TryProject(frame.Position, out Point center)
                || !TryProject(frame.Position + frame.Right, out Point rightPx)
                || !TryProject(frame.Position + frame.Up, out Point upPx))
            {
                continue;
            }

            double rx = rightPx.X - center.X, ry = rightPx.Y - center.Y;
            double ux = upPx.X - center.X, uy = upPx.Y - center.Y;
            double rightLen = Math.Sqrt(rx * rx + ry * ry);
            double upLen = Math.Sqrt(ux * ux + uy * uy);
            if (rightLen < 1e-6 || upLen < 1e-6)
            {
                continue;
            }

            rx *= radiusPx / rightLen;
            ry *= radiusPx / rightLen;
            ux *= radiusPx / upLen;
            uy *= radiusPx / upLen;

            var points = new AvaloniaList<Point>();
            const int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);
                points.Add(new Point(
                    center.X + rx * cos + ux * sin,
                    center.Y + ry * cos + uy * sin));
            }

            bool frontFacing = Vector3d.Dot(frame.Direction, Viewport.Camera.Forward) < 0;
            var disk = new Polygon
            {
                Points = points,
                Fill = new SolidColorBrush(frontFacing
                    ? Color.FromArgb(235, 245, 245, 245)
                    : Color.FromArgb(200, 40, 90, 200)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 25, 40, 60)),
                StrokeThickness = 1.4,
            };
            ToolOverlay.Children.Add(disk);

            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(Color.FromArgb(235, 25, 40, 60)),
            };
            ToolOverlay.Children.Add(dot);
            Canvas.SetLeft(dot, center.X - 2);
            Canvas.SetTop(dot, center.Y - 2);
        }
    }

    /// <summary>Мировая трансформация узла: произведение локальных трансформаций предков.</summary>
    private static Transform3d WorldTransformOf(SceneNode node)
    {
        Transform3d world = node.LocalTransform;
        for (SceneNode? parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            world = Transform3d.Multiply(world, parent.LocalTransform);
        }

        return world;
    }

    private static bool IsAxis(SnapType type)
        => type is SnapType.AxisX or SnapType.AxisY or SnapType.AxisZ
            or SnapType.AxisInstanceX or SnapType.AxisInstanceY or SnapType.AxisInstanceZ;

    private static Color SnapColor(SnapType type) => type switch
    {
        SnapType.Endpoint => Color.FromRgb(76, 175, 80),
        SnapType.Midpoint or SnapType.ArcMidpoint or SnapType.Center => Color.FromRgb(79, 195, 247),
        SnapType.Intersection => Color.FromRgb(255, 82, 82),
        SnapType.OnFace => Color.FromRgb(66, 165, 245),
        SnapType.OnEdge => Color.FromRgb(239, 83, 80),
        SnapType.Origin or SnapType.InstanceOrigin or SnapType.SnapPoint => Color.FromRgb(0, 229, 255),
        SnapType.AxisX or SnapType.AxisInstanceX => Color.FromRgb(229, 57, 53),
        SnapType.AxisY or SnapType.AxisInstanceY => Color.FromRgb(67, 160, 71),
        SnapType.AxisZ or SnapType.AxisInstanceZ => Color.FromRgb(30, 136, 229),
        SnapType.Perpendicular or SnapType.PerpendicularToFace
            or SnapType.Parallel or SnapType.Tangent or SnapType.TangentAtVertex => Color.FromRgb(224, 64, 251),
        SnapType.Grid => Color.FromRgb(158, 158, 158),
        SnapType.ExtendEdge or SnapType.FromPoint => Color.FromRgb(176, 190, 197),
        _ => Color.FromRgb(255, 255, 255),
    };

    /// <summary>Проекция мировой точки в пиксели вьюпорта; <c>false</c> — за камерой.</summary>
    private bool TryProject(Vector3d worldPoint, out Point pixel)
    {
        pixel = default;
        Camera camera = Viewport.Camera;
        if (Vector3d.Dot(worldPoint - camera.EyePosition, camera.Forward) <= 0)
        {
            return false;
        }

        Vector3d ndc = camera.Project(worldPoint);
        if (!double.IsFinite(ndc.X) || !double.IsFinite(ndc.Y))
        {
            return false;
        }

        pixel = new Point((ndc.X + 1.0) * 0.5 * Bounds.Width, (1.0 - ndc.Y) * 0.5 * Bounds.Height);
        return true;
    }

    private static IBrush BrushFor(ToolPreviewStyle style) => style switch
    {
        ToolPreviewStyle.Highlight => new SolidColorBrush(Color.FromRgb(255, 183, 77)),
        ToolPreviewStyle.Guide => new SolidColorBrush(Color.FromRgb(79, 195, 247)),
        ToolPreviewStyle.Warning => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
        _ => new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
    };

    private void UpdateHud()
    {
        if (_viewModel is null)
        {
            return;
        }

        ViewportStats stats = Viewport.GetStats();
        StatsText.Text = string.Format(
            CultureInfo.CurrentCulture,
            _viewModel.Strings.ViewportHudStats,
            stats.Fps,
            stats.FrameMs,
            stats.TriangleCount);

        double gridStep = UnitConverter.ToUnits(stats.GridCellMm, _viewModel.Scene.Unit);
        UnitsText.Text = string.Format(
            CultureInfo.CurrentCulture,
            _viewModel.Strings.ViewportHudUnits,
            _viewModel.SelectedUnit?.Name ?? string.Empty,
            FormatLength(gridStep));

        SelectionText.Text = Viewport.SelectedObject is { } node
            ? string.Format(
                CultureInfo.CurrentCulture,
                _viewModel.Strings.ViewportHudSelection,
                string.IsNullOrWhiteSpace(node.Name) ? node.Id.ToString() : node.Name)
            : _viewModel.Strings.ViewportHudSelectionNone;
    }

    private static string FormatLength(double value) => value switch
    {
        >= 100 => value.ToString("F0", CultureInfo.CurrentCulture),
        >= 1 => value.ToString("F1", CultureInfo.CurrentCulture),
        _ => value.ToString("F2", CultureInfo.CurrentCulture),
    };

    private void OnViewIso(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Isometric);

    private void OnViewFront(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Front);

    private void OnViewBack(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Back);

    private void OnViewLeft(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Left);

    private void OnViewRight(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Right);

    private void OnViewTop(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Top);

    private void OnViewBottom(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Bottom);

    private void OnZoomExtents(object? sender, RoutedEventArgs e) => Viewport.ZoomExtents();
}
