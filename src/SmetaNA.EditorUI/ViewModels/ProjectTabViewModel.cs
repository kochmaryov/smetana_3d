using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Вкладка проекта (заготовка: без документов и сценариев закрытия).</summary>
public sealed class ProjectTabViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public ProjectTabViewModel(ILocalizationService localization, int number)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Number = number;
        _localization.CultureChanged += InvalidateAll;
    }

    public int Number { get; }

    public string Title =>
        string.Format(_localization.Get(LocalizationKeys.ProjectName), Number);
}
