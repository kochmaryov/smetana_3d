using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SmetaNA.EditorUI.FloatingGroups;

/// <summary>
/// Плавающее окно группы инструментов (T3): перетаскивание за шапку,
/// подсветка зоны прикрепления у края рабочей области и лотковое
/// прикрепление/возврат в палитру.
/// </summary>
public partial class FloatingGroupWindow : Window
{
    /// <summary>Зона края, в которой окно прикрепляется лотком (пиксели).</summary>
    private const double DockZonePx = 48;

    private readonly FloatingGroupsViewModel _manager;
    private readonly FloatingGroupViewModel _floating;
    private Point _dragOffset;
    private bool _isDragging;

    /// <summary>Конструктор для дизайнера/загрузчика XAML (не используется в рантайме).</summary>
    public FloatingGroupWindow()
        : this(new FloatingGroupsViewModel(new JsonFloatingGroupsStore(), []), null!)
    {
    }

    public FloatingGroupWindow(FloatingGroupsViewModel manager, FloatingGroupViewModel floating)
    {
        ArgumentNullException.ThrowIfNull(manager);
        InitializeComponent();
        _manager = manager;
        _floating = floating;
        if (floating is null)
        {
            return;
        }

        DataContext = floating;
        Position = new PixelPoint((int)floating.X, (int)floating.Y);
        DragHandle.PointerPressed += OnDragHandlePressed;
        DragHandle.PointerMoved += OnDragHandleMoved;
        DragHandle.PointerReleased += OnDragHandleReleased;
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        PixelPoint screen = this.PointToScreen(e.GetPosition(this));
        _dragOffset = new Point(screen.X - Position.X, screen.Y - Position.Y);
        _isDragging = true;
        e.Pointer.Capture(DragHandle);
        e.Handled = true;
    }

    private void OnDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        PixelPoint screen = this.PointToScreen(e.GetPosition(this));
        double x = screen.X - _dragOffset.X;
        double y = screen.Y - _dragOffset.Y;
        Position = new PixelPoint((int)x, (int)y);
        _manager.Move(_floating, x, y, DetectDockHint(screen.X, screen.Y));
        e.Handled = true;
    }

    private void OnDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
        PixelPoint screen = this.PointToScreen(e.GetPosition(this));
        if (DetectDockHint(screen.X, screen.Y) is not null)
        {
            // Бросок у дока палитры — группа прикрепляется обратно в аккордеон.
            // Краевые лотки не отрисованы в главном окне: промежуточные
            // состояния «у края» приводили к исчезновению группы из интерфейса.
            _manager.EndDrag(_floating, null);
            _floating.ReturnToPaletteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        _manager.EndDrag(_floating, null);
        e.Handled = true;
    }

    /// <summary>
    /// Зона прикрепления к доку: курсор ближе <see cref="DockZonePx"/> к левому
    /// краю рабочей области главного окна (там закреплена палитра). Все размеры
    /// приводятся к физическим пикселям через <see cref="Visual.RenderScaling"/> —
    /// иначе зоны «уезжают» на экранах с масштабированием DPI.
    /// </summary>
    private FloatingGroupState? DetectDockHint(double screenX, double screenY)
    {
        if (Owner is not Window owner)
        {
            return null;
        }

        double scale = owner.RenderScaling;
        double left = owner.Position.X;
        double top = owner.Position.Y;
        double bottom = top + owner.Bounds.Height * scale;

        if (screenX - left < DockZonePx && screenY >= top && screenY <= bottom)
        {
            return FloatingGroupState.Left;
        }

        return null;
    }
}
