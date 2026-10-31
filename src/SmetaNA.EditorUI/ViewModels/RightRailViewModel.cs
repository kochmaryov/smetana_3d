namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Секция правого рейла (свойства/история/…): иконка, заголовок и состояние вылета.
/// В обычном режиме (≥ 960 px) секция показывается стеком в колонке 320 px;
/// в рейле (≤ 960 px) — кнопка-иконка (<see cref="Icons.UiIconId"/>), по клику
/// вылетает содержимое секции.
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
/// Адаптив правой колонки (по образцу палитры инструментов): при ширине окна
/// ≤ 960 px колонка 320 px сворачивается в иконочный рейл 44 px с вылетами
/// секций, чтобы не сжимать вьюпорт на узких экранах.
/// </summary>
public sealed class RightRailViewModel : ViewModelBase
{
    /// <summary>Окно ≤ 960 px — колонка превращается в рейл.</summary>
    public const double RailMaxWindowWidth = 960;

    /// <summary>Ширина развёрнутой колонки (обычный режим).</summary>
    public const double ExpandedWidth = 320;

    /// <summary>Ширина свёрнутого рейла (≤ 960 px).</summary>
    public const double RailWidth = 44;

    private bool _isRailMode;

    public RightRailViewModel(IEnumerable<RightRailSectionViewModel> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = [.. sections];
        _isRailMode = false;
    }

    /// <summary>Секции правой колонки в порядке отображения.</summary>
    public IReadOnlyList<RightRailSectionViewModel> Sections { get; }

    /// <summary>Режим рейла (≤ 960 px): иконки + вылеты вместо колонки.</summary>
    public bool IsRailMode
    {
        get => _isRailMode;
        private set
        {
            if (SetField(ref _isRailMode, value))
            {
                OnPropertyChanged(nameof(PanelWidth));
                if (value)
                {
                    CloseAllFlyouts();
                }
            }
        }
    }

    /// <summary>Текущая ширина панели: 320 px (обычно) или 44 px (рейл).</summary>
    public double PanelWidth => IsRailMode ? RailWidth : ExpandedWidth;

    /// <summary>Открытая в рейле секция (управляет единым вылетом).</summary>
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
    /// Переключить режим по ширине окна (точка подключения адаптива из
    /// LayoutUpdated контрола). Вызывается часто — значение кэшируется.
    /// </summary>
    public void ApplyWindowWidth(double windowWidth) =>
        IsRailMode = windowWidth > 0 && windowWidth <= RailMaxWindowWidth;

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

    /// <summary>Закрыть все вылеты (при входе в режим рейла).</summary>
    public void CloseAllFlyouts()
    {
        foreach (RightRailSectionViewModel section in Sections)
        {
            section.IsFlyoutOpen = false;
        }

        OpenSection = null;
    }
}
