using System.Collections.ObjectModel;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Диалог «Настройка панели инструментов» (макет variant_a, спека T3): слева
/// дерево «Все инструменты» с чекбоксами, справа «Панель (порядок и группы)» с
/// перетаскиванием порядка и своими группами, колонка «Свойства кнопки»,
/// «Новая группа…», «Сбросить к исходной», «Отмена», «Применить».
/// <para>
/// Семантика: инструмент живёт в одной группе раскладки; чекбокс дерева —
/// видимость кнопки на палитре; под-инструменты вылета следуют за своей кнопкой.
/// Системные группы можно только скрыть, свои — создать/переименовать/удалить
/// (удалённая группа возвращает инструменты в группы по умолчанию).
/// </para>
/// </summary>
public sealed class ToolLayoutDialogViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly Dictionary<string, ToolLayoutToolNode> _toolsByCommand = new(StringComparer.Ordinal);
    private ShortcutMap _shortcuts = null!;
    private string _searchText = string.Empty;
    private ToolLayoutToolNode? _selectedTool;
    private ToolLayoutPanelGroupNode? _selectedGroup;

    public ToolLayoutDialogViewModel(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        ToolLayoutModel layout)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(layout);
        _localization = localization;

        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
        ResetCommand = new RelayCommand(Reset);
        AddToPanelCommand = new RelayCommand(AddToPanel, _ => SelectedTool is not null);
        RemoveFromPanelCommand = new RelayCommand(RemoveFromPanel, _ => SelectedTool?.Group is not null);
        MoveUpCommand = new RelayCommand(_ => MoveSelection(-1), _ => CanMoveSelection(-1));
        MoveDownCommand = new RelayCommand(_ => MoveSelection(1), _ => CanMoveSelection(1));
        DeleteGroupCommand = new RelayCommand(DeleteSelectedGroup, _ => SelectedGroup?.CanDelete == true);

        Build(ToolLayoutMigrations.UpgradeToCurrent(layout, ToolGroupRegistry.DefaultGroups), shortcuts);
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>Закрытие диалога: <c>true</c> — применить раскладку.</summary>
    public event Action<bool>? CloseRequested;

    // --- Строки диалога (локализация RU/EN, смена языка без перезапуска) ---

    public string Title => Resolve(ToolPaletteStrings.DialogTitle);
    public string Subtitle => Resolve(ToolLayoutDialogStrings.DialogSubtitle);
    public string AllToolsTitle => Resolve(ToolPaletteStrings.DialogAllTools);
    public string PaletteTitle => Resolve(ToolPaletteStrings.DialogPalette);
    public string ButtonPropsTitle => Resolve(ToolPaletteStrings.DialogButtonProps);
    public string SearchHint => Resolve(ToolLayoutDialogStrings.DialogSearchHint);
    public string DragHint => Resolve(ToolLayoutDialogStrings.DialogDragHint);
    public string NewGroupText => Resolve(ToolPaletteStrings.DialogNewGroup);
    public string RenameGroupText => Resolve(ToolLayoutDialogStrings.DialogRenameGroup);
    public string DeleteGroupText => Resolve(ToolLayoutDialogStrings.DialogDeleteGroup);
    public string ResetText => Resolve(ToolPaletteStrings.DialogReset);
    public string ApplyText => Resolve(ToolPaletteStrings.DialogApply);
    public string CancelText => Resolve(ToolPaletteStrings.DialogCancel);
    public string CloseText => Resolve(ToolLayoutDialogStrings.DialogClose);
    public string ArrowRightText => Resolve(ToolLayoutDialogStrings.DialogArrowRight);
    public string ArrowLeftText => Resolve(ToolLayoutDialogStrings.DialogArrowLeft);
    public string MoveUpGlyph => Resolve(ToolLayoutDialogStrings.DialogGlyphUp);
    public string MoveDownGlyph => Resolve(ToolLayoutDialogStrings.DialogGlyphDown);
    public string MoveUpToolTip => Resolve(ToolLayoutDialogStrings.DialogMoveUp);
    public string MoveDownToolTip => Resolve(ToolLayoutDialogStrings.DialogMoveDown);
    public string GroupVisibleToolTip => Resolve(ToolLayoutDialogStrings.DialogGroupVisible);
    public string PropertyGroupText => Resolve(ToolLayoutDialogStrings.DialogPropertyGroup);
    public string PropertyGestureText => Resolve(ToolLayoutDialogStrings.DialogPropertyGesture);
    public string PropertyVcbText => Resolve(ToolLayoutDialogStrings.DialogPropertyVcb);
    public string IconHintText => Resolve(ToolLayoutDialogStrings.DialogIconHint);
    public string ShowHintText => Resolve(ToolPaletteStrings.DialogShowHint);
    public string ShowGestureText => Resolve(ToolPaletteStrings.DialogShowGesture);
    public string HideCompactText => Resolve(ToolPaletteStrings.DialogHideCompact);

    // --- Состояние диалога ---

    /// <summary>Дерево «Все инструменты»: группы реестра, чекбоксы — у кнопок.</summary>
    public ObservableCollection<ToolLayoutAllGroupNode> AllGroups { get; } = [];

    /// <summary>«Панель (порядок и группы)»: группы и кнопки в порядке палитры.</summary>
    public ObservableCollection<ToolLayoutPanelGroupNode> PanelGroups { get; } = [];

    /// <summary>Фильтр дерева: по имени, горячей клавише или id команды.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value ?? string.Empty))
            {
                ApplySearch();
            }
        }
    }

    /// <summary>Выбранная кнопка (колонка «Свойства кнопки»).</summary>
    public ToolLayoutToolNode? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (ReferenceEquals(_selectedTool, value))
            {
                return;
            }

            if (_selectedTool is not null)
            {
                _selectedTool.IsSelected = false;
            }

            _selectedTool = value;
            if (_selectedTool is not null)
            {
                _selectedTool.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedTool));
            RaiseSelectionCommands();
        }
    }

    /// <summary>Выбранная группа панели (переименование, удаление, цель «→»).</summary>
    public ToolLayoutPanelGroupNode? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (ReferenceEquals(_selectedGroup, value))
            {
                return;
            }

            if (_selectedGroup is not null)
            {
                _selectedGroup.IsSelected = false;
            }

            _selectedGroup = value;
            if (_selectedGroup is not null)
            {
                _selectedGroup.IsSelected = true;
            }

            OnPropertyChanged();
            RaiseSelectionCommands();
        }
    }

    /// <summary>Кнопка выбрана — колонка свойств заполнена.</summary>
    public bool HasSelectedTool => SelectedTool is not null;

    /// <summary>Имя по умолчанию для новой группы («Мои инструменты»).</summary>
    public string DefaultGroupName => Resolve(ToolLayoutDialogStrings.DialogGroupNameDefault);

    // --- Команды ---

    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand AddToPanelCommand { get; }
    public RelayCommand RemoveFromPanelCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand DeleteGroupCommand { get; }

    // --- Операции (для кнопок диалога и тестов) ---

    /// <summary>Показать/скрыть кнопку на палитре (чекбокс дерева «Все инструменты»).</summary>
    public void SetToolVisible(string commandId, bool visible)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        if (_toolsByCommand.TryGetValue(commandId, out ToolLayoutToolNode? tool))
        {
            tool.IsVisible = visible;
        }
    }

    /// <summary>Выбрать кнопку диалога.</summary>
    public void SelectTool(ToolLayoutToolNode? tool) => SelectedTool = tool;

    /// <summary>Выбрать группу панели.</summary>
    public void SelectGroup(ToolLayoutPanelGroupNode? group) => SelectedGroup = group;

    /// <summary>
    /// Переставить кнопку на панели (drag&amp;drop): <paramref name="visibleIndex"/> —
    /// позиция среди видимых кнопок целевой группы (скрытые остаются на своих местах).
    /// </summary>
    public void MoveToolTo(ToolLayoutToolNode tool, ToolLayoutPanelGroupNode targetGroup, int visibleIndex)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(targetGroup);
        if (visibleIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        ToolLayoutPanelGroupNode? sourceGroup = tool.Group;
        sourceGroup?.Items.Remove(tool);

        int insertIndex = targetGroup.Items.Count;
        int seenVisible = 0;
        for (int index = 0; index < targetGroup.Items.Count; index++)
        {
            if (!targetGroup.Items[index].IsVisible)
            {
                continue;
            }

            if (seenVisible == visibleIndex)
            {
                insertIndex = index;
                break;
            }

            seenVisible++;
        }

        targetGroup.Items.Insert(insertIndex, tool);
        tool.Group = targetGroup;
        sourceGroup?.RefreshCounts();
        targetGroup.RefreshCounts();
    }

    /// <summary>Переставить группу на панели (drag&amp;drop).</summary>
    public void MoveGroupTo(ToolLayoutPanelGroupNode group, int index)
    {
        ArgumentNullException.ThrowIfNull(group);
        int oldIndex = PanelGroups.IndexOf(group);
        if (oldIndex < 0)
        {
            return;
        }

        int newIndex = Math.Clamp(index, 0, PanelGroups.Count - 1);
        if (oldIndex != newIndex)
        {
            PanelGroups.Move(oldIndex, newIndex);
        }
    }

    /// <summary>Создать свою группу (пустая имя — «Мои инструменты»); в конце панели.</summary>
    public ToolLayoutPanelGroupNode CreateGroup(string? name)
    {
        ToolLayoutPanelGroupNode group = new(
            _localization,
            NextCustomGroupId(),
            nameKey: null,
            customName: NormalizeGroupName(name));
        PanelGroups.Add(group);
        SelectedGroup = group;
        return group;
    }

    /// <summary>Переименовать свою группу; системная группа не меняется.</summary>
    public void RenameGroup(ToolLayoutPanelGroupNode group, string? name)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (group.CanRename)
        {
            group.Rename(NormalizeGroupName(name));
        }
    }

    /// <summary>
    /// Удалить свою группу: инструменты возвращаются в группы по умолчанию со
    /// своей видимостью; системная группа не удаляется.
    /// </summary>
    public void DeleteGroup(ToolLayoutPanelGroupNode group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!group.CanDelete)
        {
            return;
        }

        foreach (ToolLayoutToolNode tool in group.Items.ToList())
        {
            ToolLayoutPanelGroupNode home = FindPanelGroup(tool.DefaultGroupId)
                ?? PanelGroups.FirstOrDefault(candidate => !ReferenceEquals(candidate, group))
                ?? throw new InvalidOperationException("На панели нет группы для возврата инструмента.");
            home.Items.Add(tool);
            tool.Group = home;
            home.RefreshCounts();
        }

        group.Items.Clear();
        PanelGroups.Remove(group);
        if (ReferenceEquals(SelectedGroup, group))
        {
            SelectedGroup = null;
        }
    }

    /// <summary>Сбросить рабочую раскладку к дефолтной (в хранилище — по «Применить»).</summary>
    public void Reset()
    {
        SelectedTool = null;
        SelectedGroup = null;
        Build(ToolLayoutModel.CreateDefault(), _shortcuts);
    }

    /// <summary>Раскладка диалога (schema v1) — сохраняется хранилищем по «Применить».</summary>
    public ToolLayoutModel BuildLayout() =>
        new(
            ToolLayoutModel.CurrentVersion,
            [.. PanelGroups.Select(group => new ToolLayoutGroup(
                group.Id,
                group.IsCustom ? group.CustomName : null,
                group.IsVisible,
                [.. group.Items.Select((tool, index) => new ToolLayoutItem(tool.CommandId, tool.IsVisible, index))]))]);

    private void Apply() => CloseRequested?.Invoke(true);

    private void Cancel() => CloseRequested?.Invoke(false);

    private void AddToPanel(object? _)
    {
        ToolLayoutToolNode? tool = SelectedTool;
        if (tool is null)
        {
            return;
        }

        ToolLayoutPanelGroupNode target = SelectedGroup
            ?? tool.Group
            ?? FindPanelGroup(tool.DefaultGroupId)
            ?? PanelGroups.First();
        MoveToolTo(tool, target, target.Items.Count(item => item.IsVisible));
        tool.IsVisible = true;
    }

    private void RemoveFromPanel(object? _)
    {
        if (SelectedTool?.Group is not null)
        {
            SelectedTool.IsVisible = false;
        }
    }

    private void DeleteSelectedGroup(object? _)
    {
        if (SelectedGroup is not null)
        {
            DeleteGroup(SelectedGroup);
        }
    }

    private bool CanMoveSelection(int direction) =>
        SelectedTool?.Group is not null || SelectedGroup is not null;

    private void MoveSelection(int direction)
    {
        if (SelectedTool is not null)
        {
            if (SelectedTool is { IsVisible: true, Group: ToolLayoutPanelGroupNode group } tool)
            {
                List<ToolLayoutToolNode> visible = [.. group.Items.Where(item => item.IsVisible)];
                int current = visible.IndexOf(tool);
                int target = current + direction;
                if (current >= 0 && target >= 0 && target < visible.Count)
                {
                    MoveToolTo(tool, group, target);
                }
            }

            return;
        }

        if (SelectedGroup is not null)
        {
            MoveGroupTo(SelectedGroup, PanelGroups.IndexOf(SelectedGroup) + direction);
        }
    }

    private void Build(ToolLayoutModel layout, ShortcutMap shortcuts)
    {
        _shortcuts = shortcuts;
        _toolsByCommand.Clear();
        AllGroups.Clear();
        PanelGroups.Clear();

        // Кнопки реестра — по одному узлу на инструмент (живёт в одной группе).
        Dictionary<string, ToolLayoutToolNode> nodes = _toolsByCommand;
        foreach (PaletteGroup registryGroup in ToolGroupRegistry.DefaultGroups)
        {
            foreach (PaletteButton button in registryGroup.Buttons)
            {
                nodes[button.Definition.CommandId] = new ToolLayoutToolNode(
                    _localization,
                    shortcuts,
                    button,
                    registryGroup.Id);
            }
        }

        foreach (PaletteGroup registryGroup in ToolGroupRegistry.DefaultGroups)
        {
            ToolLayoutAllGroupNode allGroup = new(_localization, registryGroup);
            foreach (PaletteButton button in registryGroup.Buttons)
            {
                allGroup.Items.Add(nodes[button.Definition.CommandId]);
            }

            AllGroups.Add(allGroup);
        }

        foreach (ToolLayoutGroup group in layout.Groups)
        {
            ToolLayoutPanelGroupNode panelGroup = new(
                _localization,
                group.Id,
                ToolGroupRegistry.DefaultGroups.FirstOrDefault(candidate => candidate.Id == group.Id)?.NameKey,
                group.CustomName);
            foreach (ToolLayoutItem item in group.Items)
            {
                if (!nodes.TryGetValue(item.CommandId, out ToolLayoutToolNode? tool) || tool.Group is not null)
                {
                    continue;
                }

                tool.IsVisible = item.Visible;
                tool.Group = panelGroup;
                panelGroup.Items.Add(tool);
            }

            PanelGroups.Add(panelGroup);
        }

        // Инструменты без группы (неполная раскладка) — в группу по умолчанию.
        foreach (ToolLayoutToolNode tool in nodes.Values.Where(tool => tool.Group is null))
        {
            ToolLayoutPanelGroupNode home = FindPanelGroup(tool.DefaultGroupId) ?? PanelGroups.First();
            tool.Group = home;
            home.Items.Add(tool);
        }

        foreach (ToolLayoutPanelGroupNode group in PanelGroups)
        {
            group.RefreshCounts();
        }
    }

    private ToolLayoutPanelGroupNode? FindPanelGroup(string groupId) =>
        PanelGroups.FirstOrDefault(group => group.Id == groupId);

    private string NextCustomGroupId()
    {
        HashSet<string> used = [.. PanelGroups.Select(group => group.Id)];
        for (int index = 1; ; index++)
        {
            string candidate = $"custom.{index}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private string NormalizeGroupName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? ToolLayoutDialogStrings.Resolve(_localization, ToolLayoutDialogStrings.DialogGroupNameDefault)
            : name.Trim();

    private void ApplySearch()
    {
        foreach (ToolLayoutAllGroupNode group in AllGroups)
        {
            bool groupMatches = false;
            foreach (ToolLayoutToolNode tool in group.Items)
            {
                bool matches = tool.Matches(_searchText)
                    || tool.FlyoutItems.Any(child => child.Matches(_searchText));
                tool.IsMatch = matches;
                groupMatches |= matches;
            }

            group.IsMatch = groupMatches;
        }
    }

    private void RaiseSelectionCommands()
    {
        AddToPanelCommand.RaiseCanExecuteChanged();
        RemoveFromPanelCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        DeleteGroupCommand.RaiseCanExecuteChanged();
    }

    private string Resolve(string key) => ToolLayoutDialogStrings.Resolve(_localization, key);

    private void OnCultureChanged()
    {
        foreach (ToolLayoutGroupNodeBase group in AllGroups.Cast<ToolLayoutGroupNodeBase>().Concat(PanelGroups))
        {
            group.RefreshText();
        }

        InvalidateAll();
    }
}
