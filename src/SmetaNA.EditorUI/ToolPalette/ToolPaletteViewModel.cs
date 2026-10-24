using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>Режим раскладки палитры по ширине окна (спека §4 «Адаптивность»).</summary>
public enum PaletteLayoutMode
{
    /// <summary>Две колонки кнопок 38×38 (ширина палитры 92 px).</summary>
    TwoColumns,

    /// <summary>Одна колонка, окно ≤ 960 px (ширина палитры 52 px).</summary>
    SingleColumn,

    /// <summary>Рейл с вылетами групп, окно ≤ 768 px (ширина палитры 56 px).</summary>
    Rail,
}

/// <summary>
/// View-модель палитры инструментов (трек B, T1/T2/T4/T7): группы и кнопки из
/// <see cref="ToolGroupRegistry"/>, активный инструмент, подсветка шага урока,
/// вылеты «Дуга»/«Зеркало», тултипы «Имя · жест» и адаптив 2 колонки →
/// 1 колонка → рейл со свёрткой групп при переполнении (§4, R2).
/// </summary>
public sealed class ToolPaletteViewModel : ViewModelBase
{
    /// <summary>Ширина окна, начиная с которой палитра сворачивается в одну колонку.</summary>
    public const double SingleColumnMaxWindowWidth = 960;

    /// <summary>Ширина окна, начиная с которой палитра становится рейлом с вылетами.</summary>
    public const double RailMaxWindowWidth = 768;

    /// <summary>Ширина палитры в две колонки (макет variant_a).</summary>
    public const double WidePaletteWidth = 92;

    /// <summary>Ширина палитры в одну колонку.</summary>
    public const double CompactPaletteWidth = 52;

    /// <summary>Ширина палитры-рейла (наработка варианта C).</summary>
    public const double RailPaletteWidth = 56;

    /// <summary>Ширина сетки кнопок: две колонки 38×38.</summary>
    public const double WideButtonsPanelWidth = 80;

    /// <summary>Ширина сетки кнопок: одна колонка.</summary>
    public const double CompactButtonsPanelWidth = 40;

    /// <summary>Групп, которые не сворачиваются автоматически (базовые 6 инструментов).</summary>
    private const int ProtectedGroupCount = 2;

    private readonly Action<ToolStubViewModel> _activate;
    private readonly List<ToolPaletteButtonViewModel> _allButtons = [];
    private PaletteLayoutMode _layoutMode = PaletteLayoutMode.TwoColumns;
    private string? _activeCommandId;
    private string? _highlightedCommandId;
    private double _viewportHeight;

    /// <param name="localization">Локализация имён, подсказок и групп.</param>
    /// <param name="shortcuts">Жесты для тултипов «Имя · жест» (T5).</param>
    /// <param name="activate">Активация инструмента/служебной команды хостом
    /// (как в <c>MainWindowViewModel.ActivatePaletteItem</c>).</param>
    /// <param name="showHints">Режим «Новичок»: добавлять пояснение HintKey в тултип.</param>
    /// <param name="groups">Группы палитры; по умолчанию — <see cref="ToolGroupRegistry.DefaultGroups"/>.</param>
    public ToolPaletteViewModel(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        Action<ToolStubViewModel> activate,
        Func<bool>? showHints = null,
        IReadOnlyList<PaletteGroup>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(activate);
        _activate = activate;
        Func<bool> hints = showHints ?? NoHints;

        Groups =
        [
            .. (groups ?? ToolGroupRegistry.DefaultGroups).Select(group =>
                new ToolPaletteGroupViewModel(
                    localization,
                    group,
                    [.. group.Buttons.Select(button => CreateButton(localization, shortcuts, hints, button))])),
        ];

        foreach (ToolPaletteGroupViewModel group in Groups)
        {
            foreach (ToolPaletteButtonViewModel button in group.Buttons)
            {
                RegisterButton(button);
            }
        }
    }

    /// <summary>Группы палитры в порядке по умолчанию.</summary>
    public IReadOnlyList<ToolPaletteGroupViewModel> Groups { get; }

    /// <summary>Все кнопки: группы + под-инструменты вылетов.</summary>
    public IReadOnlyList<ToolPaletteButtonViewModel> AllButtons => _allButtons;

    /// <summary>Текущий режим раскладки (см. <see cref="ApplyWindowWidth"/>).</summary>
    public PaletteLayoutMode LayoutMode
    {
        get => _layoutMode;
        private set
        {
            if (!SetField(ref _layoutMode, value))
            {
                return;
            }

            _viewportHeight = 0;
            OnPropertyChanged(nameof(IsTwoColumns));
            OnPropertyChanged(nameof(IsSingleColumn));
            OnPropertyChanged(nameof(IsRailMode));
            OnPropertyChanged(nameof(ButtonsPanelWidth));
            OnPropertyChanged(nameof(PaletteWidth));
        }
    }

    public bool IsTwoColumns => LayoutMode == PaletteLayoutMode.TwoColumns;

    public bool IsSingleColumn => LayoutMode == PaletteLayoutMode.SingleColumn;

    public bool IsRailMode => LayoutMode == PaletteLayoutMode.Rail;

    /// <summary>Ширина сетки кнопок: 80 px (2 колонки) → 40 px (1 колонка).</summary>
    public double ButtonsPanelWidth =>
        LayoutMode == PaletteLayoutMode.SingleColumn ? CompactButtonsPanelWidth : WideButtonsPanelWidth;

    /// <summary>Ширина палитры: 92 px → 52 px (≤ 960 px) → 56 px (рейл ≤ 768 px).</summary>
    public double PaletteWidth => LayoutMode switch
    {
        PaletteLayoutMode.SingleColumn => CompactPaletteWidth,
        PaletteLayoutMode.Rail => RailPaletteWidth,
        _ => WidePaletteWidth,
    };

    /// <summary>Идентификатор активного инструмента (или <c>null</c> — нет).</summary>
    public string? ActiveCommandId
    {
        get => _activeCommandId;
        set
        {
            if (!SetField(ref _activeCommandId, value))
            {
                return;
            }

            foreach (ToolPaletteButtonViewModel button in _allButtons)
            {
                button.IsActive = value is not null && button.CommandId == value;
            }
        }
    }

    /// <summary>
    /// Команда текущего шага урока для подсветки кнопки (T7); <c>null</c> — снять
    /// подсветку. Кнопка с вылетом подсвечивается и по под-инструментам.
    /// </summary>
    public string? HighlightedCommandId
    {
        get => _highlightedCommandId;
        set
        {
            if (!SetField(ref _highlightedCommandId, value))
            {
                return;
            }

            foreach (ToolPaletteButtonViewModel button in _allButtons)
            {
                button.IsHighlighted = value is not null
                    && (button.CommandId == value
                        || button.FlyoutItems.Any(item => item.CommandId == value));
            }
        }
    }

    /// <summary>Режим палитры по ширине окна: ≤ 960 px — одна колонка, ≤ 768 px — рейл.</summary>
    public static PaletteLayoutMode ResolveMode(double windowWidth) => windowWidth switch
    {
        <= RailMaxWindowWidth => PaletteLayoutMode.Rail,
        <= SingleColumnMaxWindowWidth => PaletteLayoutMode.SingleColumn,
        _ => PaletteLayoutMode.TwoColumns,
    };

    /// <summary>Применить ширину окна (контрол сообщает при изменении размеров).</summary>
    public void ApplyWindowWidth(double windowWidth) => LayoutMode = ResolveMode(windowWidth);

    /// <summary>
    /// Свёртка групп при переполнении по высоте (риск R2): группы с конца
    /// сворачиваются, пока содержимое не поместится; первые две группы (базовые
    /// 6 инструментов) не сворачиваются — остаток покрывает скролл палитры.
    /// Высота кэшируется: повторный вызов с той же высотой ничего не меняет.
    /// </summary>
    public void ApplyViewportHeight(double viewportHeight)
    {
        if (viewportHeight <= 0 || Math.Abs(viewportHeight - _viewportHeight) < 0.5)
        {
            return;
        }

        _viewportHeight = viewportHeight;
        if (LayoutMode == PaletteLayoutMode.Rail)
        {
            return;
        }

        int columns = LayoutMode == PaletteLayoutMode.SingleColumn ? 1 : 2;
        for (int index = Groups.Count - 1; index >= ProtectedGroupCount; index--)
        {
            if (TotalHeight(columns) <= viewportHeight)
            {
                break;
            }

            Groups[index].IsExpanded = false;
        }
    }

    /// <summary>Кнопка по идентификатору команды; <c>null</c> — не найдена.</summary>
    public ToolPaletteButtonViewModel? FindButton(string commandId)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        return _allButtons.FirstOrDefault(button => button.CommandId == commandId);
    }

    /// <summary>Пересчитать тултипы (смена режима «Новичок» или языка).</summary>
    public void RefreshToolTips()
    {
        foreach (ToolPaletteButtonViewModel button in _allButtons)
        {
            button.Refresh();
        }
    }

    /// <summary>Закрыть открытые вылеты (выбор инструмента завершает действие).</summary>
    public void CloseFlyouts()
    {
        foreach (ToolPaletteButtonViewModel button in _allButtons)
        {
            button.IsFlyoutOpen = false;
        }

        foreach (ToolPaletteGroupViewModel group in Groups)
        {
            group.IsFlyoutOpen = false;
        }
    }

    private double TotalHeight(int columns) =>
        Groups.Sum(group => group.EstimateHeight(columns));

    private ToolPaletteButtonViewModel CreateButton(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        Func<bool> showHints,
        PaletteButton button)
    {
        return new ToolPaletteButtonViewModel(
            localization,
            showHints,
            new ToolStubViewModel(localization, shortcuts, button.Definition, OnButtonActivated, showHints),
            button.IconId,
            [.. button.FlyoutItems.Select(item => CreateButton(localization, shortcuts, showHints, item))]);
    }

    /// <summary>Кнопка и её под-инструменты — в плоский список (кнопка раньше вылета).</summary>
    private void RegisterButton(ToolPaletteButtonViewModel button)
    {
        _allButtons.Add(button);
        foreach (ToolPaletteButtonViewModel item in button.FlyoutItems)
        {
            RegisterButton(item);
        }
    }

    private void OnButtonActivated(ToolStubViewModel tool)
    {
        if (!ToolGroupRegistry.IsServiceCommand(tool.CommandId))
        {
            ActiveCommandId = tool.CommandId;
        }

        CloseFlyouts();
        _activate(tool);
    }

    private static bool NoHints() => false;
}
