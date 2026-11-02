using System.Collections.ObjectModel;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.FloatingGroups;

/// <summary>
/// Менеджер перемещаемых групп (T3): отрыв от палитры, плавающие окна в любом
/// месте экрана, лотки у краёв рабочей области и персистентность размещений
/// в <c>floating-groups.json</c> (версия схемы + миграция — R7).
/// Плавающие окна создаёт/закрывает окно приложения по событиям
/// <see cref="FloatingRequested"/> / <see cref="PlacementRemoved"/>.
/// </summary>
public sealed class FloatingGroupsViewModel : ViewModelBase
{
    private readonly IFloatingGroupsStore _store;
    private readonly Dictionary<string, FloatingGroupViewModel> _byGroup = [];
    private FloatingGroupState? _dockHint;

    public FloatingGroupsViewModel(
        IFloatingGroupsStore store,
        IEnumerable<ToolPaletteGroupViewModel> groups)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(groups);
        _store = store;

        FloatingGroupsModel model = store.Load();
        foreach (ToolPaletteGroupViewModel group in groups)
        {
            FloatingGroupPlacement placement = model.Groups
                .FirstOrDefault(candidate => candidate.GroupId == group.Id)
                ?? new FloatingGroupPlacement(group.Id, FloatingGroupState.Palette, 0, 0);

            if (placement.State != FloatingGroupState.Palette)
            {
                group.IsDetached = true;
                FloatingGroupViewModel floating = CreateFloating(
                    group, placement.State, placement.X, placement.Y);
                CollectionFor(placement.State).Add(floating);
            }

            group.DetachRequested += OnDetachRequested;
            group.ReturnToPaletteRequested += OnReturnToPaletteRequested;
            SubscribedGroups.Add(group);
        }
    }

    /// <summary>Плавающие окна (показывает окно приложения).</summary>
    public ObservableCollection<FloatingGroupViewModel> Floating { get; } = [];

    /// <summary>Лотки у левого края рабочей области.</summary>
    public ObservableCollection<FloatingGroupViewModel> DockedLeft { get; } = [];

    /// <summary>Лотки у правого края рабочей области.</summary>
    public ObservableCollection<FloatingGroupViewModel> DockedRight { get; } = [];

    /// <summary>Лотки у верхнего края рабочей области.</summary>
    public ObservableCollection<FloatingGroupViewModel> DockedTop { get; } = [];

    /// <summary>Лотки у нижнего края рабочей области.</summary>
    public ObservableCollection<FloatingGroupViewModel> DockedBottom { get; } = [];

    /// <summary>Запрос показа плавающего окна (обрабатывает окно приложения).</summary>
    public event Action<FloatingGroupViewModel>? FloatingRequested;

    /// <summary>Плавающее окно больше не нужно (лоток/возврат в палитру).</summary>
    public event Action<FloatingGroupViewModel>? PlacementRemoved;

    /// <summary>Подсветка зоны прикрепления при перетаскивании плавающего окна.</summary>
    public FloatingGroupState? DockHint
    {
        get => _dockHint;
        private set => SetField(ref _dockHint, value);
    }

    /// <summary>
    /// Оторвать группу от палитры (клик по «⠿» в шапке группы): группа
    /// становится плавающим окном у места по умолчанию.
    /// </summary>
    private void OnDetachRequested(ToolPaletteGroupViewModel group)
    {
        if (_byGroup.ContainsKey(group.Id))
        {
            return;
        }

        group.IsDetached = true;
        FloatingGroupViewModel floating = CreateFloating(group, FloatingGroupState.Floating, 0, 0);
        Floating.Add(floating);
        Save();
        FloatingRequested?.Invoke(floating);
    }

    /// <summary>Вернуть группу в палитру («×» окна/лотка).</summary>
    private void OnReturnToPaletteRequested(ToolPaletteGroupViewModel group)
    {
        if (!_byGroup.Remove(group.Id, out FloatingGroupViewModel? floating))
        {
            return;
        }

        group.IsDetached = false;
        RemoveFromAll(floating);
        Save();
        PlacementRemoved?.Invoke(floating);
    }

    /// <summary>
    /// Переместить плавающее окно при перетаскивании: подсветка зоны
    /// прикрепления у края рабочей области (Left/Right/Top/Bottom)
    /// или свободное плавание (<c>null</c>).
    /// </summary>
    public void Move(FloatingGroupViewModel floating, double x, double y, FloatingGroupState? dockHint)
    {
        ArgumentNullException.ThrowIfNull(floating);
        floating.X = x;
        floating.Y = y;
        DockHint = dockHint;
    }

    /// <summary>
    /// Завершить перетаскивание: при подсвеченной зоне окно прикрепляется
    /// лотком к краю, иначе остаётся плавающим с текущими координатами.
    /// </summary>
    public void EndDrag(FloatingGroupViewModel floating, FloatingGroupState? dockHint)
    {
        ArgumentNullException.ThrowIfNull(floating);
        DockHint = null;
        if (dockHint is { } state
            && state is FloatingGroupState.Left or FloatingGroupState.Right
                or FloatingGroupState.Top or FloatingGroupState.Bottom)
        {
            floating.State = state;
            Floating.Remove(floating);
            CollectionFor(state).Add(floating);
            PlacementRemoved?.Invoke(floating);
        }

        Save();
    }

    /// <summary>Сделать группу снова плавающей (из краевого лотка).</summary>
    public void MakeFloating(FloatingGroupViewModel floating, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(floating);
        RemoveFromAll(floating);
        floating.State = FloatingGroupState.Floating;
        floating.X = x;
        floating.Y = y;
        Floating.Add(floating);
        Save();
        FloatingRequested?.Invoke(floating);
    }

    /// <summary>Все размещения (для сохранения и проверок).</summary>
    public IEnumerable<FloatingGroupViewModel> AllPlacements()
    {
        foreach (FloatingGroupViewModel floating in Floating)
        {
            yield return floating;
        }

        foreach (FloatingGroupViewModel floating in DockedLeft)
        {
            yield return floating;
        }

        foreach (FloatingGroupViewModel floating in DockedRight)
        {
            yield return floating;
        }

        foreach (FloatingGroupViewModel floating in DockedTop)
        {
            yield return floating;
        }

        foreach (FloatingGroupViewModel floating in DockedBottom)
        {
            yield return floating;
        }
    }

    /// <summary>Состояние группы: размещение вне палитры или <see cref="FloatingGroupState.Palette"/>.</summary>
    public FloatingGroupState StateOf(string groupId) =>
        _byGroup.TryGetValue(groupId, out FloatingGroupViewModel? floating)
            ? floating.State
            : FloatingGroupState.Palette;

    /// <summary>
    /// Управление размещением из диалога «Настройка панели» (T9): отрыв/возврат
    /// идёт тем же путём, что и «⠿»/«×» палитры (окна и персистентность — события).
    /// </summary>
    public void SetDetached(string groupId, bool detached)
    {
        ToolPaletteGroupViewModel? group = SubscribedGroups
            .FirstOrDefault(candidate => candidate.Id == groupId);
        if (group is null)
        {
            return;
        }

        if (detached)
        {
            group.RequestDetach();
        }
        else
        {
            group.RequestReturnToPalette();
        }
    }

    /// <summary>Сохранить размещения (схема версионируется, R7).</summary>
    public void Save()
    {
        List<FloatingGroupPlacement> placements =
        [
            .. AllPlacements().Select(floating => new FloatingGroupPlacement(
                floating.GroupId,
                floating.State,
                floating.X,
                floating.Y)),
        ];

        _store.Save(new FloatingGroupsModel(FloatingGroupsModel.CurrentVersion, placements));
    }

    private ObservableCollection<FloatingGroupViewModel> CollectionFor(FloatingGroupState state) =>
        state switch
        {
            FloatingGroupState.Left => DockedLeft,
            FloatingGroupState.Right => DockedRight,
            FloatingGroupState.Top => DockedTop,
            FloatingGroupState.Bottom => DockedBottom,
            _ => Floating,
        };

    private void RemoveFromAll(FloatingGroupViewModel floating)
    {
        Floating.Remove(floating);
        DockedLeft.Remove(floating);
        DockedRight.Remove(floating);
        DockedTop.Remove(floating);
        DockedBottom.Remove(floating);
    }

    private FloatingGroupViewModel CreateFloating(
        ToolPaletteGroupViewModel group,
        FloatingGroupState state,
        double x,
        double y)
    {
        FloatingGroupViewModel floating = new(
            group,
            state,
            x,
            y,
            makeFloating: target => MakeFloating(target, target.X, target.Y),
            returnToPalette: target => group.RequestReturnToPalette());
        _byGroup[group.Id] = floating;
        return floating;
    }

    /// <summary>
    /// Перепривязка после пересборки палитры (раскладка применена в диалоге):
    /// размещения сохраняются по идентификаторам групп, окна пересоздаются
    /// через события <see cref="PlacementRemoved"/>/<see cref="FloatingRequested"/>.
    /// </summary>
    public void Rebind(IEnumerable<ToolPaletteGroupViewModel> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        // Снять подписки со старых групп и снять флаги отрыва.
        foreach (ToolPaletteGroupViewModel oldGroup in SubscribedGroups)
        {
            oldGroup.DetachRequested -= OnDetachRequested;
            oldGroup.ReturnToPaletteRequested -= OnReturnToPaletteRequested;
            oldGroup.IsDetached = false;
        }

        // Размещения по идентификаторам: состояние и координаты сохраняются.
        Dictionary<string, (FloatingGroupState State, double X, double Y)> saved = AllPlacements()
            .ToDictionary(
                floating => floating.GroupId,
                floating => (floating.State, floating.X, floating.Y));
        foreach (FloatingGroupViewModel floating in AllPlacements().ToList())
        {
            PlacementRemoved?.Invoke(floating);
        }

        _byGroup.Clear();
        Floating.Clear();
        DockedLeft.Clear();
        DockedRight.Clear();
        DockedTop.Clear();
        DockedBottom.Clear();

        SubscribedGroups.Clear();
        foreach (ToolPaletteGroupViewModel group in groups)
        {
            SubscribedGroups.Add(group);
            group.DetachRequested += OnDetachRequested;
            group.ReturnToPaletteRequested += OnReturnToPaletteRequested;
            if (!saved.TryGetValue(group.Id, out (FloatingGroupState State, double X, double Y) placement)
                || placement.State == FloatingGroupState.Palette)
            {
                continue;
            }

            group.IsDetached = true;
            FloatingGroupViewModel floating = CreateFloating(
                group, placement.State, placement.X, placement.Y);
            CollectionFor(placement.State).Add(floating);
            if (placement.State == FloatingGroupState.Floating)
            {
                FloatingRequested?.Invoke(floating);
            }
        }

        Save();
    }

    private List<ToolPaletteGroupViewModel> SubscribedGroups { get; } = [];
}
