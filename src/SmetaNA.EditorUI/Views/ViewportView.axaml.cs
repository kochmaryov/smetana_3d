using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SmetaNA.Core.Model;
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
    private DispatcherTimer? _hudTimer;
    private ProjectTabViewModel? _viewModel;

    public ViewportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        Viewport.SelectionChanged += OnSelectionChanged;
        Viewport.HoverChanged += OnSelectionChanged;
    }

    /// <summary>Снимок статистики вьюпорта (для смоук-прогона).</summary>
    internal ViewportStats GetStats() => Viewport.GetStats();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ProjectTabViewModel;
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
