using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Запрос имени своей группы («Новая группа…», «Переименовать…»): имя вводится в
/// модальном окне; пустое имя заменяется на «Мои инструменты».
/// </summary>
public sealed class ToolLayoutGroupNameViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly bool _isNewGroup;
    private string _name;

    public ToolLayoutGroupNameViewModel(ILocalizationService localization, bool isNewGroup, string? initialName)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        _isNewGroup = isNewGroup;
        _name = initialName ?? string.Empty;
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        _localization.CultureChanged += InvalidateAll;
    }

    /// <summary>Закрытие окна: <c>true</c> — имя подтверждено.</summary>
    public event Action<bool>? CloseRequested;

    public string Title => ToolLayoutDialogStrings.Resolve(
        _localization,
        _isNewGroup ? ToolLayoutDialogStrings.DialogNewGroupTitle : ToolLayoutDialogStrings.DialogRenameGroupTitle);

    public string Label => ToolLayoutDialogStrings.Resolve(_localization, ToolLayoutDialogStrings.DialogGroupNameLabel);

    public string OkText => ToolLayoutDialogStrings.Resolve(_localization, ToolLayoutDialogStrings.DialogOk);

    public string CancelText => ToolLayoutDialogStrings.Resolve(_localization, ToolPalette.ToolPaletteStrings.DialogCancel);

    /// <summary>Имя группы (пустое — «Мои инструменты»).</summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty);
    }

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Создать модель запроса имени для новой группы.</summary>
    public static ToolLayoutGroupNameViewModel ForNewGroup(ILocalizationService localization, string defaultName) =>
        new(localization, isNewGroup: true, defaultName);

    /// <summary>Создать модель запроса имени для переименования группы.</summary>
    public static ToolLayoutGroupNameViewModel ForRename(ILocalizationService localization, string currentName) =>
        new(localization, isNewGroup: false, currentName);
}
