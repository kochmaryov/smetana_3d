namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Секция правого дока (свойства/история/…): иконка, заголовок и состояние выбора.
/// Док постоянный (50 px рейл + колонка панели): кнопка-иконка
/// (<see cref="Icons.UiIconId"/>) выбирает вкладку секции.
/// </summary>
public sealed class RightRailSectionViewModel : ViewModelBase
{
    private bool _isSelected;

    public RightRailSectionViewModel(string id, Icons.UiIconId icon, string title)
    {
        Id = id;
        Icon = icon;
        Title = title;
    }

    /// <summary>Стабильный идентификатор секции (для ключей локализации).</summary>
    public string Id { get; }

    /// <summary>Глиф иконки из icon-pack интерфейса для кнопки рейла.</summary>
    public Icons.UiIconId Icon { get; }

    /// <summary>Локализованный заголовок секции.</summary>
    public string Title { get; private set; }

    /// <summary>Обновить заголовок из функции локализации (смена культуры).</summary>
    public void RefreshTitle(Func<string> titleFactory)
    {
        ArgumentNullException.ThrowIfNull(titleFactory);
        Title = titleFactory();
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Секция выбрана (её панель показана в доке).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}

/// <summary>
/// Правый док главного окна: иконочный рейл (50 px) + колонка панели
/// выбранной секции (300 px). Панель расширяет сетку окна и не перекрывает
/// сцену (спека ui-redesign T2, приоритет no-overlay).
/// </summary>
public sealed class RightRailViewModel : ViewModelBase
{
    /// <summary>
    /// Ширина рейла с кнопками-иконками: кнопка 40 + отступы 2×1 + поле 2×3 + рамка.
    /// </summary>
    public const double RailWidth = 50;

    /// <summary>Ширина колонки панели секции.</summary>
    public const double DockWidth = 300;

    /// <summary>Ширина окна, начиная с которой док сворачивается в рейл.</summary>
    public const double CompactMaxWindowWidth = 960;

    public RightRailViewModel(IEnumerable<RightRailSectionViewModel> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = [.. sections];
    }

    /// <summary>Секции правой колонки в порядке отображения.</summary>
    public IReadOnlyList<RightRailSectionViewModel> Sections { get; }

    /// <summary>Ширина колонки панели секции (0 — док свёрнут).</summary>
    public double PanelWidth => IsDockOpen ? DockWidth : 0;

    /// <summary>Док с панелью секции раскрыт.</summary>
    public bool IsDockOpen
    {
        get => _isDockOpen;
        private set
        {
            if (SetField(ref _isDockOpen, value))
            {
                OnPropertyChanged(nameof(PanelWidth));
            }
        }
    }

    private bool _isDockOpen;

    /// <summary>Выбранная секция (её панель показана в доке).</summary>
    public RightRailSectionViewModel? OpenSection
    {
        get => _openSection;
        private set
        {
            if (SetField(ref _openSection, value))
            {
                foreach (RightRailSectionViewModel section in Sections)
                {
                    section.IsSelected = section == value;
                }

                RaiseShowFlags();
            }
        }
    }

    private RightRailSectionViewModel? _openSection;

    public bool ShowTutorial => OpenSection?.Id == "tutorial";
    public bool ShowProperties => OpenSection?.Id == "properties";
    public bool ShowHistory => OpenSection?.Id == "history";
    public bool ShowShortcuts => OpenSection?.Id == "shortcuts";
    public bool ShowPrintVolume => OpenSection?.Id == "printVolume";

    /// <summary>
    /// Выбрать секцию по клику на её иконку/вкладку. Повторный клик по уже
    /// выбранной секции сворачивает док в рейл.
    /// </summary>
    public void ToggleSection(RightRailSectionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (IsDockOpen && OpenSection == section)
        {
            IsDockOpen = false;
            return;
        }

        OpenSection = section;
        IsDockOpen = true;
    }

    /// <summary>
    /// Адаптив по ширине окна: ≤ 960 px док сворачивается в рейл (T11),
    /// раскрытие секции — выдвижением в сетку.
    /// </summary>
    public void ApplyWindowWidth(double width)
    {
        bool compact = width < CompactMaxWindowWidth;
        if (compact == _isCompact)
        {
            return;
        }

        _isCompact = compact;
        if (compact && IsDockOpen)
        {
            IsDockOpen = false;
        }
    }

    private bool _isCompact;

    private void RaiseShowFlags()
    {
        OnPropertyChanged(nameof(ShowTutorial));
        OnPropertyChanged(nameof(ShowProperties));
        OnPropertyChanged(nameof(ShowHistory));
        OnPropertyChanged(nameof(ShowShortcuts));
        OnPropertyChanged(nameof(ShowPrintVolume));
    }

    /// <summary>Свернуть док в рейл.</summary>
    public void CloseAllFlyouts()
    {
        IsDockOpen = false;
    }
}
