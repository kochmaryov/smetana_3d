using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Пункт переключателя единиц измерения сцены (в HUD вьюпорта).</summary>
public sealed class UnitOption : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public UnitOption(ILocalizationService localization, LengthUnit unit)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Unit = unit;
        _localization.CultureChanged += InvalidateAll;
    }

    public LengthUnit Unit { get; }

    public string Name => _localization.Get(LocalizationKeys.ForUnit(Unit));
}
