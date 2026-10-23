using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Tools;
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

    private DispatcherTimer? _hudTimer;
    private ProjectTabViewModel? _viewModel;
    private ToolController? _tools;
    private long _lastToolHoverTicks;

    public ViewportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        Viewport.SelectionChanged += OnSelectionChanged;
        Viewport.HoverChanged += OnSelectionChanged;

        // Маршрутизация ввода в активный инструмент: туннелирующие обработчики
        // идут раньше камеры вьюпорта и потребляют события, если их взял инструмент.
        AddHandler(InputElement.PointerPressedEvent, OnToolPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnToolPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, OnToolPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnToolKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Снимок статистики вьюпорта (для смоук-прогона).</summary>
    internal ViewportStats GetStats() => Viewport.GetStats();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_tools is not null)
        {
            _tools.Changed -= OnToolStateChanged;
            _tools.Context.UndoStack.Changed -= OnSceneHistoryChanged;
        }

        _viewModel = DataContext as ProjectTabViewModel;
        _tools = _viewModel?.Tools;
        if (_tools is not null)
        {
            _tools.Changed += OnToolStateChanged;
            _tools.Context.UndoStack.Changed += OnSceneHistoryChanged;
        }

        RedrawToolOverlay();
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Viewport.Scene = _viewModel.Scene;
        Viewport.TargetFramesPerSecond = EditorOptions.TargetFps;
        Viewport.RenderMode = _viewModel.SelectedRenderMode.Mode;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _hudTimer.Tick += OnHudTick;
        _hudTimer.Start();
        UpdateHud();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_hudTimer is not null)
        {
            _hudTimer.Stop();
            _hudTimer.Tick -= OnHudTick;
            _hudTimer = null;
        }
    }

    private void OnHudTick(object? sender, EventArgs e) => UpdateHud();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateHud();

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateHud();

    private void OnToolStateChanged() => RedrawToolOverlay();

    private void OnSceneHistoryChanged() => Viewport.RefreshScene();

    private void OnToolPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_tools is not { IsActive: true } tools)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(Viewport);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Shift+ЛКМ — панорама камеры, инструменту не передаётся
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        UpdateToolViewport();
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
        }
    }

    private void OnToolPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_tools is not { IsActive: true } tools || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        Point position = e.GetPosition(Viewport);
        UpdateToolViewport();
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

    private void OnToolPointerMoved(object? sender, PointerEventArgs e)
    {
        // камера могла повернуться — перепроецировать предпросмотр
        RedrawToolOverlay();

        if (_tools is not { IsActive: true } tools)
        {
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
    }

    private void OnToolKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tools is not { IsActive: true } tools)
        {
            return;
        }

        ToolKey? key = e.Key switch
        {
            Key.Escape => ToolKey.Escape,
            Key.Enter => ToolKey.Enter,
            Key.Delete => ToolKey.Delete,
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
    }

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
            _viewModel.SelectedUnit.Name,
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

    private void OnRenderModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel?.SelectedRenderMode is { } option)
        {
            Viewport.RenderMode = option.Mode;
        }
    }

    private void OnUnitChanged(object? sender, SelectionChangedEventArgs e) => UpdateHud();

    private void OnProjectionToggle(object? sender, RoutedEventArgs e)
    {
        Viewport.Camera.Mode = OrthoToggle.IsChecked == true
            ? ProjectionMode.Orthographic
            : ProjectionMode.Perspective;
        Viewport.ZoomExtents();
    }

    private void OnGridToggle(object? sender, RoutedEventArgs e) =>
        Viewport.ShowGrid = GridToggle.IsChecked == true;

    private void OnAxesToggle(object? sender, RoutedEventArgs e) =>
        Viewport.ShowAxes = AxesToggle.IsChecked == true;

    private void OnViewIso(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Isometric);

    private void OnViewFront(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Front);

    private void OnViewBack(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Back);

    private void OnViewLeft(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Left);

    private void OnViewRight(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Right);

    private void OnViewTop(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Top);

    private void OnViewBottom(object? sender, RoutedEventArgs e) => Viewport.SetView(StandardView.Bottom);

    private void OnZoomExtents(object? sender, RoutedEventArgs e) => Viewport.ZoomExtents();
}
