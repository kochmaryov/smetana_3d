using System.ComponentModel;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>
/// Кнопка палитры инструментов поверх <see cref="ToolStubViewModel"/> (B1, B3):
/// имя, жест и пояснение — из общей модели ленты; добавлены глиф
/// <see cref="ToolIconId"/>, под-инструменты вылета («Дуга», «Зеркало» — B2) и
/// тултип «Имя · жест» с пояснением <c>HintKey</c> в режиме «Новичок» (T7).
/// </summary>
public sealed class ToolPaletteButtonViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly Func<bool> _showHints;
    private bool _isFlyoutOpen;

    public ToolPaletteButtonViewModel(
        ILocalizationService localization,
        Func<bool> showHints,
        ToolStubViewModel tool,
        ToolIconId iconId,
        IReadOnlyList<ToolPaletteButtonViewModel>? flyoutItems = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(showHints);
        ArgumentNullException.ThrowIfNull(tool);
        _localization = localization;
        _showHints = showHints;
        Tool = tool;
        IconId = iconId;
        FlyoutItems = flyoutItems ?? [];
        OpenFlyoutCommand = new RelayCommand(() => IsFlyoutOpen = true);
        Tool.PropertyChanged += OnToolPropertyChanged;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>Модель кнопки ленты (общая для палитры и ленты).</summary>
    public ToolStubViewModel Tool { get; }

    public string CommandId => Tool.CommandId;

    /// <summary>Глиф кнопки (контракт волны 0; отрисовка — трек A).</summary>
    public ToolIconId IconId { get; }

    /// <summary>Под-инструменты вылета; пусто — вылета нет.</summary>
    public IReadOnlyList<ToolPaletteButtonViewModel> FlyoutItems { get; }

    public bool HasFlyout => FlyoutItems.Count > 0;

    public string Name => Tool.Name;

    public string Gesture => Tool.Gesture;

    /// <summary>Пояснение инструмента для новичков; пустое — если не задано.</summary>
    public string Hint => Tool.Hint;

    /// <summary>Подпись кнопки-заглушки вместо иконки (волна 0 → трек A).</summary>
    public string PaletteLabel => Tool.PaletteLabel;

    /// <summary>Строка «Имя · жест» (без жеста — только имя).</summary>
    public string Label => Gesture.Length > 0 ? $"{Name} · {Gesture}" : Name;

    /// <summary>Тултип «Имя · жест»; в режиме «Новичок» добавляется пояснение HintKey.</summary>
    public string ToolTip =>
        _showHints() && Hint.Length > 0
            ? Label + Environment.NewLine + Hint
            : Label;

    public RelayCommand ActivateCommand => Tool.ActivateCommand;

    /// <summary>Активная кнопка (текущий инструмент) — выделение в палитре.</summary>
    public bool IsActive
    {
        get => Tool.IsActive;
        set => Tool.IsActive = value;
    }

    /// <summary>Подсвеченная кнопка (текущий шаг урока).</summary>
    public bool IsHighlighted
    {
        get => Tool.IsHighlighted;
        set => Tool.IsHighlighted = value;
    }

    /// <summary>Вылет-меню кнопки открыто (B2).</summary>
    public bool IsFlyoutOpen
    {
        get => _isFlyoutOpen;
        set => SetField(ref _isFlyoutOpen, value);
    }

    public RelayCommand OpenFlyoutCommand { get; }

    /// <summary>Пересчитать тултип (смена языка или режима «Новичок»).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ToolTip));
    }

    private void OnToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ToolStubViewModel.IsActive):
                OnPropertyChanged(nameof(IsActive));
                break;
            case nameof(ToolStubViewModel.IsHighlighted):
                OnPropertyChanged(nameof(IsHighlighted));
                break;
            case null:
            case "":
                OnCultureChanged();
                break;
        }
    }

    private void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Gesture));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(PaletteLabel));
        Refresh();
    }
}
