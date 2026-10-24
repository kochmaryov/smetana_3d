using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Описание кнопки инструмента в ленте: имя и подсказка — из общего каталога
/// локализации, вид числового ввода — для VCB.
/// </summary>
/// <param name="CommandId">Идентификатор команды (совпадает с id инструмента Core).</param>
/// <param name="NameKey">Ключ имени в общем каталоге локализации.</param>
/// <param name="VcbKind">Вид числового ввода в строке значений (или <c>null</c>).</param>
/// <param name="HintKey">Ключ объяснения инструмента для режима «Новичок».</param>
public sealed record ToolDefinition(
    string CommandId,
    string NameKey,
    VcbInputKind? VcbKind,
    string? HintKey = null);

/// <summary>Кнопка инструмента в ленте.</summary>
public sealed class ToolStubViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly ShortcutMap _shortcuts;
    private readonly Func<bool> _showHints;
    private bool _isHighlighted;
    private bool _isActive;

    public ToolStubViewModel(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        ToolDefinition definition,
        Action<ToolStubViewModel> activate,
        Func<bool>? showHints = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(activate);
        _localization = localization;
        _shortcuts = shortcuts;
        _showHints = showHints ?? NoHints;
        CommandId = definition.CommandId;
        NameKey = definition.NameKey;
        HintKey = definition.HintKey;
        VcbKind = definition.VcbKind;
        ActivateCommand = new RelayCommand(_ => activate(this));
        _localization.CultureChanged += InvalidateAll;
    }

    public string CommandId { get; }
    public string NameKey { get; }
    public string? HintKey { get; }
    public VcbInputKind? VcbKind { get; }

    public RelayCommand ActivateCommand { get; }

    public string Name => _localization.Get(NameKey);

    /// <summary>Объяснение инструмента для новичков; пустое — если не задано.</summary>
    public string Hint => HintKey is null ? string.Empty : _localization.Get(HintKey);

    public string ToolTip
    {
        get
        {
            var parts = new List<string> { Name };
            if (_showHints() && Hint.Length > 0)
            {
                parts.Add(Hint);
            }

            if (Gesture.Length > 0)
            {
                parts.Add(Gesture);
            }

            return string.Join(Environment.NewLine, parts);
        }
    }

    /// <summary>Подсвеченная кнопка (текущий шаг урока).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetField(ref _isHighlighted, value);
    }

    /// <summary>Активная кнопка (текущий инструмент) — выделение в палитре.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    /// <summary>Текущая комбинация клавиш команды.</summary>
    public string Gesture => _shortcuts.Find(CommandId)?.ToString() ?? string.Empty;

    /// <summary>Подпись кнопки-заглушки палитры (волна 0): жест или «·» без него.</summary>
    public string PaletteLabel => Gesture.Length > 0 ? Gesture : "·";

    /// <summary>Пересчитать подсказку (смена режима или языка).</summary>
    public void Refresh() => OnPropertyChanged(nameof(ToolTip));

    private static bool NoHints() => false;
}
