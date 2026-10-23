using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Строка списка горячих клавиш: команда и её текущая комбинация.</summary>
public sealed class ShortcutRowViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly ShortcutMap _shortcuts;

    public ShortcutRowViewModel(ILocalizationService localization, ShortcutMap shortcuts, string commandId)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        _localization = localization;
        _shortcuts = shortcuts;
        CommandId = commandId;
        _localization.CultureChanged += InvalidateAll;
    }

    public string CommandId { get; }

    public string Name =>
        _localization.Get(_shortcuts.GetNameKey(CommandId) ?? CommandId);

    public string Gesture => _shortcuts.Find(CommandId)?.ToString() ?? string.Empty;

    /// <summary>Обновить строку после изменения маппинга.</summary>
    public void Refresh() => InvalidateAll();
}
