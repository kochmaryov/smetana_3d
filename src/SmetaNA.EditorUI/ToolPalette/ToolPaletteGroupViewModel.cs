using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>
/// Группа палитры инструментов (раздел с подписью): кнопки, свёртка-аккордеон
/// при переполнении по высоте (риск R2) и панель-вылет для режима рейла (B4).
/// </summary>
public sealed class ToolPaletteGroupViewModel : ViewModelBase
{
    /// <summary>Высота шапки группы (кнопка-заголовок).</summary>
    public const double HeaderHeight = 20;

    /// <summary>Высота ячейки кнопки 38×38 с отступом.</summary>
    public const double ButtonCellHeight = 40;

    /// <summary>Отступ после группы.</summary>
    public const double GroupSpacing = 8;

    private readonly ILocalizationService _localization;
    private bool _isExpanded = true;
    private bool _isFlyoutOpen;

    public ToolPaletteGroupViewModel(
        ILocalizationService localization,
        PaletteGroup group,
        IReadOnlyList<ToolPaletteButtonViewModel> buttons)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(buttons);
        _localization = localization;
        Id = group.Id;
        NameKey = group.NameKey;
        Buttons = buttons;
        LeadIconId = buttons.Count > 0 ? buttons[0].IconId : Icons.ToolIconId.None;
        RailItems =
        [
            .. buttons.SelectMany(button =>
                button.HasFlyout
                    ? button.FlyoutItems
                    : new[] { button }),
        ];
        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        OpenFlyoutCommand = new RelayCommand(() => IsFlyoutOpen = true);
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>Стабильный идентификатор группы (для раскладки).</summary>
    public string Id { get; }

    public string NameKey { get; }

    /// <summary>Локализованное имя группы («Рисование», «Правка», …).</summary>
    public string Name => _localization.Get(NameKey);

    /// <summary>
    /// Ведущая иконка группы (глиф первой кнопки) — для кнопки группы в режиме
    /// рейла: кнопка-иконка с тултипом-именем вместо текстовой подписи.
    /// </summary>
    public Icons.ToolIconId LeadIconId { get; }

    public IReadOnlyList<ToolPaletteButtonViewModel> Buttons { get; }

    /// <summary>
    /// Пункты вылета рейла (≤ 768 px): под-инструменты вылетов разворачиваются
    /// плоско — любой инструмент остаётся в 2 действиях (клик по группе → пункт).
    /// </summary>
    public IReadOnlyList<ToolPaletteButtonViewModel> RailItems { get; }

    /// <summary>Группа раскрыта (аккордеон при переполнении палитры).</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public RelayCommand ToggleExpandedCommand { get; }

    /// <summary>Вылет-панель группы в режиме рейла открыта.</summary>
    public bool IsFlyoutOpen
    {
        get => _isFlyoutOpen;
        set => SetField(ref _isFlyoutOpen, value);
    }

    public RelayCommand OpenFlyoutCommand { get; }

    /// <summary>Оценка высоты группы при заданном числе колонок (свёртка, R2).</summary>
    public double EstimateHeight(int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        double rows = Math.Ceiling(Buttons.Count / (double)columns);
        return HeaderHeight + (IsExpanded ? rows * ButtonCellHeight : 0) + GroupSpacing;
    }

    private void OnCultureChanged() => OnPropertyChanged(nameof(Name));
}
