using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Описание инструмента-заглушки (фаза 2 подключит реальные инструменты).</summary>
public sealed record ToolDefinition(string CommandId, string NameKey, VcbInputKind? VcbKind);

/// <summary>Кнопка инструмента-заглушки в ленте.</summary>
public sealed class ToolStubViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly ShortcutMap _shortcuts;

    public ToolStubViewModel(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        ToolDefinition definition,
        Action<ToolStubViewModel> activate)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(activate);
        _localization = localization;
        _shortcuts = shortcuts;
        CommandId = definition.CommandId;
        NameKey = definition.NameKey;
        VcbKind = definition.VcbKind;
        ActivateCommand = new RelayCommand(_ => activate(this));
        _localization.CultureChanged += InvalidateAll;
    }

    public string CommandId { get; }
    public string NameKey { get; }
    public VcbInputKind? VcbKind { get; }

    public RelayCommand ActivateCommand { get; }

    public string Name => _localization.Get(NameKey);

    public string ToolTip =>
        string.Format(_localization.Get(LocalizationKeys.ToolStubToolTip), Name);

    /// <summary>Текущая комбинация клавиш команды.</summary>
    public string Gesture => _shortcuts.Find(CommandId)?.ToString() ?? string.Empty;
}
