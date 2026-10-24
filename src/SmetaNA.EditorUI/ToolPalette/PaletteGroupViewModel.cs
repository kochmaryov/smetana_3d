using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>Группа палитры во view-модели: имя раздела и его кнопки.</summary>
public sealed class PaletteGroupViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public PaletteGroupViewModel(
        ILocalizationService localization,
        PaletteGroup group,
        IEnumerable<ToolStubViewModel> buttons)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(buttons);
        _localization = localization;
        Id = group.Id;
        NameKey = group.NameKey;
        Buttons = [.. buttons];
        _localization.CultureChanged += OnCultureChanged;
    }

    public string Id { get; }

    public string NameKey { get; }

    /// <summary>Локализованное имя группы («Рисование», «Правка», …).</summary>
    public string Name => _localization.Get(NameKey);

    public IReadOnlyList<ToolStubViewModel> Buttons { get; }

    private void OnCultureChanged() => OnPropertyChanged(nameof(Name));
}
