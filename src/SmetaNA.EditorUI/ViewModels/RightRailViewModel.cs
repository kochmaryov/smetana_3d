namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Секция правого рейла (свойства/история/…): иконка, заголовок и состояние вылета.
/// Рейл постоянный (44 px): кнопка-иконка (<see cref="Icons.UiIconId"/>), по клику
/// вылетает содержимое секции вправо.
/// </summary>
public sealed class RightRailSectionViewModel : ViewModelBase
{
    private bool _isFlyoutOpen;

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

    /// <summary>Вылет секции в режиме рейла открыт.</summary>
    public bool IsFlyoutOpen
    {
        get => _isFlyoutOpen;
        set => SetField(ref _isFlyoutOpen, value);
    }
}

/// <summary>
/// Правый рейл главного окна: постоянный иконочный рейл 44 px — секции
/// (свойства/история/…) открываются вылетом вправо по клику на иконку,
/// чтобы не сжимать вьюпорт и не заливать экран текстом подсказок.
/// </summary>
public sealed class RightRailViewModel : ViewModelBase
{
    /// <summary>Ширина рейла с кнопками-иконками секций.</summary>
    public const double RailWidth = 44;

    public RightRailViewModel(IEnumerable<RightRailSectionViewModel> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = [.. sections];
    }

    /// <summary>Секции правой колонки в порядке отображения.</summary>
    public IReadOnlyList<RightRailSectionViewModel> Sections { get; }

    /// <summary>Ширина панели: постоянный рейл 44 px.</summary>
    public double PanelWidth => RailWidth;

    /// <summary>Открытая секция (управляет единым вылетом).</summary>
    public RightRailSectionViewModel? OpenSection
    {
        get => _openSection;
        private set
        {
            if (SetField(ref _openSection, value))
            {
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
    /// Открыть/закрыть вылет секции по клику на её иконку в рейле.
    /// Повторный клик по той же секции закрывает вылет.
    /// </summary>
    public void ToggleSection(RightRailSectionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (OpenSection == section)
        {
            section.IsFlyoutOpen = !section.IsFlyoutOpen;
            if (!section.IsFlyoutOpen)
            {
                OpenSection = null;
            }

            return;
        }

        foreach (RightRailSectionViewModel other in Sections)
        {
            other.IsFlyoutOpen = false;
        }

        OpenSection = section;
        section.IsFlyoutOpen = true;
    }

    private void RaiseShowFlags()
    {
        OnPropertyChanged(nameof(ShowTutorial));
        OnPropertyChanged(nameof(ShowProperties));
        OnPropertyChanged(nameof(ShowHistory));
        OnPropertyChanged(nameof(ShowShortcuts));
        OnPropertyChanged(nameof(ShowPrintVolume));
    }

    /// <summary>Закрыть все вылеты рейла.</summary>
    public void CloseAllFlyouts()
    {
        foreach (RightRailSectionViewModel section in Sections)
        {
            section.IsFlyoutOpen = false;
        }

        OpenSection = null;
    }
}
