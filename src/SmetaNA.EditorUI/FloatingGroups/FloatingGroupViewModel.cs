using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.FloatingGroups;

/// <summary>
/// Размещение одной оторванной группы (T3): плавающее окно в любом месте
/// экрана или лоток у края рабочей области. Координаты — пиксели экрана.
/// </summary>
public sealed class FloatingGroupViewModel : ViewModelBase
{
    private readonly Action<FloatingGroupViewModel> _makeFloating;
    private double _x;
    private double _y;
    private FloatingGroupState _state;

    public FloatingGroupViewModel(
        ToolPaletteGroupViewModel group,
        FloatingGroupState state,
        double x,
        double y,
        Action<FloatingGroupViewModel> makeFloating,
        Action<FloatingGroupViewModel> returnToPalette)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(makeFloating);
        ArgumentNullException.ThrowIfNull(returnToPalette);
        Group = group;
        _state = state;
        _x = x;
        _y = y;
        _makeFloating = makeFloating;
        MakeFloatingCommand = new RelayCommand(_ => _makeFloating(this));
        ReturnToPaletteCommand = new RelayCommand(_ => returnToPalette(this));
    }

    /// <summary>Группа инструментов этого размещения.</summary>
    public ToolPaletteGroupViewModel Group { get; }

    /// <summary>Идентификатор группы (для размещений и персистентности).</summary>
    public string GroupId => Group.Id;

    /// <summary>Состояние: плавающее окно или краевой лоток.</summary>
    public FloatingGroupState State
    {
        get => _state;
        set => SetField(ref _state, value);
    }

    /// <summary>Координата X плавающего окна (пиксели экрана).</summary>
    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    /// <summary>Координата Y плавающего окна (пиксели экрана).</summary>
    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    /// <summary>Сделать группу снова плавающей (из краевого лотка).</summary>
    public RelayCommand MakeFloatingCommand { get; }

    /// <summary>Вернуть группу в левую палитру.</summary>
    public RelayCommand ReturnToPaletteCommand { get; }
}
