using SmetaNA.EditorUI.Localization;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Пункт переключателя режима отображения вьюпорта (каркас/штриховка/рентген).</summary>
public sealed class RenderModeOption : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public RenderModeOption(ILocalizationService localization, RenderMode mode)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Mode = mode;
        _localization.CultureChanged += InvalidateAll;
    }

    public RenderMode Mode { get; }

    public string Name => _localization.Get(Mode switch
    {
        RenderMode.Wireframe => LocalizationKeys.ViewportModeWireframe,
        RenderMode.Shaded => LocalizationKeys.ViewportModeShaded,
        RenderMode.XRay => LocalizationKeys.ViewportModeXRay,
        _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null),
    });
}
