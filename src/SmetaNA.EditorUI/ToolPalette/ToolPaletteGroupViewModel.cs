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

    /// <summary>Отступ StackPanel между шапкой и сеткой кнопок (Spacing в разметке).</summary>
    public const double HeaderSpacing = 4;

    /// <summary>Высота ячейки кнопки 38×38 с отступом.</summary>
    public const double ButtonCellHeight = 40;

    /// <summary>Отступ после группы.</summary>
    public const double GroupSpacing = 8;

    /// <summary>Высота блока-разделителя перед группой (линия + отступ).</summary>
    public const double DividerHeight = 5;

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
        DetachCommand = new RelayCommand(_ => RequestDetach());
        ReturnToPaletteCommand = new RelayCommand(_ => RequestReturnToPalette());
        _localization.CultureChanged += OnCultureChanged;

        // Режим рейла не показывает кнопки инструментов: активный инструмент
        // отмечается на кнопке группы, поэтому группа слушает свои кнопки.
        foreach (ToolPaletteButtonViewModel button in AllGroupButtons(Buttons))
        {
            button.PropertyChanged += OnButtonPropertyChanged;
        }
    }

    /// <summary>Стабильный идентификатор группы (для раскладки).</summary>
    public string Id { get; }

    public string NameKey { get; }

    /// <summary>Локализованное имя группы («Рисование», «Правка», …).</summary>
    public string Name => _localization.Get(NameKey);

    /// <summary>Подсказка кнопки отрыва группы («⠿» в шапке группы, T3).</summary>
    public string DetachHint => _localization.Get(ToolPaletteStrings.GroupDetach);

    /// <summary>Подсказка кнопки возврата группы в палитру («×» окна/лотка, T3).</summary>
    public string CloseHint => _localization.Get(ToolPaletteStrings.GroupClose);

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

    /// <summary>Группа оторвана от палитры (плавает или прикреплена к краю, T3).</summary>
    public bool IsDetached
    {
        get => _isDetached;
        set => SetField(ref _isDetached, value);
    }

    private bool _isDetached;

    /// <summary>
    /// Показывать разделитель над группой: так секции отделяются друг от друга
    /// без рамок вокруг подписи. Снимается у первой группы палитры.
    /// </summary>
    public bool ShowDivider
    {
        get => _showDivider;
        internal set => SetField(ref _showDivider, value);
    }

    private bool _showDivider = true;

    /// <summary>
    /// Показывать в шапке стрелку свёртки и ручку отрыва. В режиме одной колонки
    /// (узкая палитра) места хватает только на подпись группы.
    /// </summary>
    public bool ShowHeaderControls
    {
        get => _showHeaderControls;
        internal set => SetField(ref _showHeaderControls, value);
    }

    private bool _showHeaderControls = true;

    /// <summary>Запрос отрыва группы от палитры (клик по «⠿»).</summary>
    public event Action<ToolPaletteGroupViewModel>? DetachRequested;

    /// <summary>Запрос возврата группы в палитру (кнопка «×» плавающего окна/лотка).</summary>
    public event Action<ToolPaletteGroupViewModel>? ReturnToPaletteRequested;

    /// <summary>Оторвать группу от палитры (в плавающее окно или краевую зону).</summary>
    public RelayCommand DetachCommand { get; }

    /// <summary>Вернуть группу в палитру.</summary>
    public RelayCommand ReturnToPaletteCommand { get; }

    /// <summary>Поднять запрос отрыва (вызывается контролом палитры).</summary>
    public void RequestDetach() => DetachRequested?.Invoke(this);

    /// <summary>Поднять запрос возврата в палитру (вызывается окном/лотком).</summary>
    public void RequestReturnToPalette() => ReturnToPaletteRequested?.Invoke(this);

    /// <summary>Вылет-панель группы в режиме рейла открыта.</summary>
    public bool IsFlyoutOpen
    {
        get => _isFlyoutOpen;
        set => SetField(ref _isFlyoutOpen, value);
    }

    public RelayCommand OpenFlyoutCommand { get; }

    /// <summary>
    /// В группе есть активный инструмент (включая под-инструменты вылетов):
    /// в режиме рейла это единственный индикатор текущего инструмента.
    /// </summary>
    public bool IsActive => AllGroupButtons(Buttons).Any(button => button.IsActive);

    private static IEnumerable<ToolPaletteButtonViewModel> AllGroupButtons(
        IEnumerable<ToolPaletteButtonViewModel> buttons) =>
        buttons.SelectMany(button => AllGroupButtons(button.FlyoutItems).Prepend(button));

    private void OnButtonPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolPaletteButtonViewModel.IsActive))
        {
            OnPropertyChanged(nameof(IsActive));
        }
    }

    /// <summary>
    /// Оценка высоты группы при заданном числе колонок (свёртка, R2):
    /// шапка + отступ StackPanel + строки кнопок + нижний отступ группы.
    /// Занижение оценки давало ложное «всё влезло» → появлялся вертикальный
    /// скроллбар палитры, поэтому отступы считаются явно.
    /// </summary>
    public double EstimateHeight(int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        double rows = Math.Ceiling(Buttons.Count / (double)columns);
        return (ShowDivider ? DividerHeight : 0)
            + HeaderHeight
            + (IsExpanded ? HeaderSpacing + (rows * ButtonCellHeight) : 0)
            + GroupSpacing;
    }

    private void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetachHint));
        OnPropertyChanged(nameof(CloseHint));
    }
}
