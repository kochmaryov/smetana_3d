using System.ComponentModel;

namespace SmetaNA.EditorUI.Localization;

/// <summary>
/// Фасад строк для привязок XAML: при смене языка все привязки обновляются разом.
/// Каждая строка интерфейса, используемая в разметке, имеет здесь свойство
/// (полнота покрытия проверяется тестами).
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;

    public LocalizedStrings(ILocalizationService localization)
    {
        _localization = localization;
        _localization.CultureChanged += OnCultureChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppTitle => _localization.Get(LocalizationKeys.AppTitle);

    public string ToolbarPrimary => _localization.Get(LocalizationKeys.ToolbarPrimary);
    public string ToolbarAdvanced => _localization.Get(LocalizationKeys.ToolbarAdvanced);
    public string ToolbarAdvancedToggle => _localization.Get(LocalizationKeys.ToolbarAdvancedToggle);

    public string ProjectNew => _localization.Get(LocalizationKeys.ProjectNew);
    public string ProjectName => _localization.Get(LocalizationKeys.ProjectName);

    public string ToolSelect => _localization.Get(LocalizationKeys.ToolSelect);
    public string ToolLine => _localization.Get(LocalizationKeys.ToolLine);
    public string ToolRectangle => _localization.Get(LocalizationKeys.ToolRectangle);
    public string ToolCircle => _localization.Get(LocalizationKeys.ToolCircle);
    public string ToolPolygon => _localization.Get(LocalizationKeys.ToolPolygon);
    public string ToolArc => _localization.Get(LocalizationKeys.ToolArc);
    public string ToolFreehand => _localization.Get(LocalizationKeys.ToolFreehand);
    public string ToolPushPull => _localization.Get(LocalizationKeys.ToolPushPull);
    public string ToolOffset => _localization.Get(LocalizationKeys.ToolOffset);
    public string ToolMove => _localization.Get(LocalizationKeys.ToolMove);
    public string ToolRotate => _localization.Get(LocalizationKeys.ToolRotate);
    public string ToolScale => _localization.Get(LocalizationKeys.ToolScale);
    public string ToolEraser => _localization.Get(LocalizationKeys.ToolEraser);
    public string ToolSoften => _localization.Get(LocalizationKeys.ToolSoften);
    public string ToolPaintBucket => _localization.Get(LocalizationKeys.ToolPaintBucket);
    public string ToolStubStatus => _localization.Get(LocalizationKeys.ToolStubStatus);
    public string ToolStubToolTip => _localization.Get(LocalizationKeys.ToolStubToolTip);
    public string ToolNone => _localization.Get(LocalizationKeys.ToolNone);

    public string EditUndo => _localization.Get(LocalizationKeys.EditUndo);
    public string EditRedo => _localization.Get(LocalizationKeys.EditRedo);
    public string EditUndoNamed => _localization.Get(LocalizationKeys.EditUndoNamed);
    public string EditRedoNamed => _localization.Get(LocalizationKeys.EditRedoNamed);

    public string HistoryTitle => _localization.Get(LocalizationKeys.HistoryTitle);
    public string HistoryEmpty => _localization.Get(LocalizationKeys.HistoryEmpty);
    public string HistoryCurrent => _localization.Get(LocalizationKeys.HistoryCurrent);
    public string HistoryStubCommand => _localization.Get(LocalizationKeys.HistoryStubCommand);

    public string PropertiesTitle => _localization.Get(LocalizationKeys.PropertiesTitle);
    public string PropertiesActiveTool => _localization.Get(LocalizationKeys.PropertiesActiveTool);
    public string PropertiesSelection => _localization.Get(LocalizationKeys.PropertiesSelection);
    public string PropertiesNoSelection => _localization.Get(LocalizationKeys.PropertiesNoSelection);
    public string PropertiesScene => _localization.Get(LocalizationKeys.PropertiesScene);
    public string PropertiesSceneUnit => _localization.Get(LocalizationKeys.PropertiesSceneUnit);
    public string PropertiesSceneNodes => _localization.Get(LocalizationKeys.PropertiesSceneNodes);
    public string PropertiesStubNote => _localization.Get(LocalizationKeys.PropertiesStubNote);

    public string ViewportTitle => _localization.Get(LocalizationKeys.ViewportTitle);
    public string ViewportPlaceholder => _localization.Get(LocalizationKeys.ViewportPlaceholder);
    public string ViewportPlaceholderHint => _localization.Get(LocalizationKeys.ViewportPlaceholderHint);

    public string VcbPromptReady => _localization.Get(LocalizationKeys.VcbPromptReady);
    public string VcbPromptLength => _localization.Get(LocalizationKeys.VcbPromptLength);
    public string VcbPromptRadius => _localization.Get(LocalizationKeys.VcbPromptRadius);
    public string VcbPromptAngle => _localization.Get(LocalizationKeys.VcbPromptAngle);
    public string VcbPromptCoordinates => _localization.Get(LocalizationKeys.VcbPromptCoordinates);
    public string VcbWatermark => _localization.Get(LocalizationKeys.VcbWatermark);
    public string VcbCommitted => _localization.Get(LocalizationKeys.VcbCommitted);
    public string VcbErrorEmpty => _localization.Get(LocalizationKeys.VcbErrorEmpty);
    public string VcbErrorInvalidNumber => _localization.Get(LocalizationKeys.VcbErrorInvalidNumber);
    public string VcbErrorNotFinite => _localization.Get(LocalizationKeys.VcbErrorNotFinite);
    public string VcbErrorNegative => _localization.Get(LocalizationKeys.VcbErrorNegative);
    public string VcbErrorNotPositive => _localization.Get(LocalizationKeys.VcbErrorNotPositive);
    public string VcbErrorUnknownUnit => _localization.Get(LocalizationKeys.VcbErrorUnknownUnit);
    public string VcbErrorComponentCount => _localization.Get(LocalizationKeys.VcbErrorComponentCount);

    public string UnitMillimeter => _localization.Get(LocalizationKeys.UnitMillimeter);
    public string UnitCentimeter => _localization.Get(LocalizationKeys.UnitCentimeter);
    public string UnitMeter => _localization.Get(LocalizationKeys.UnitMeter);
    public string UnitInch => _localization.Get(LocalizationKeys.UnitInch);

    public string ShortcutsTitle => _localization.Get(LocalizationKeys.ShortcutsTitle);
    public string ShortcutsHint => _localization.Get(LocalizationKeys.ShortcutsHint);
    public string ShortcutsCommand => _localization.Get(LocalizationKeys.ShortcutsCommand);
    public string ShortcutsGesture => _localization.Get(LocalizationKeys.ShortcutsGesture);
    public string ShortcutsApply => _localization.Get(LocalizationKeys.ShortcutsApply);
    public string ShortcutsReset => _localization.Get(LocalizationKeys.ShortcutsReset);
    public string ShortcutsResetAll => _localization.Get(LocalizationKeys.ShortcutsResetAll);
    public string ShortcutsErrorInvalid => _localization.Get(LocalizationKeys.ShortcutsErrorInvalid);
    public string ShortcutsErrorConflict => _localization.Get(LocalizationKeys.ShortcutsErrorConflict);
    public string ShortcutsErrorUnknown => _localization.Get(LocalizationKeys.ShortcutsErrorUnknown);
    public string ShortcutsApplied => _localization.Get(LocalizationKeys.ShortcutsApplied);

    public string CommandToggleTheme => _localization.Get(LocalizationKeys.CommandToggleTheme);
    public string CommandToggleLanguage => _localization.Get(LocalizationKeys.CommandToggleLanguage);

    public string ThemeLabel => _localization.Get(LocalizationKeys.ThemeLabel);
    public string ThemeLight => _localization.Get(LocalizationKeys.ThemeLight);
    public string ThemeDark => _localization.Get(LocalizationKeys.ThemeDark);
    public string ThemeSwitched => _localization.Get(LocalizationKeys.ThemeSwitched);

    public string LanguageLabel => _localization.Get(LocalizationKeys.LanguageLabel);
    public string LanguageRussian => _localization.Get(LocalizationKeys.LanguageRussian);
    public string LanguageEnglish => _localization.Get(LocalizationKeys.LanguageEnglish);
    public string LanguageSwitched => _localization.Get(LocalizationKeys.LanguageSwitched);

    public string StatusReady => _localization.Get(LocalizationKeys.StatusReady);

    public string ViewportModeLabel => _localization.Get(LocalizationKeys.ViewportModeLabel);
    public string ViewportModeWireframe => _localization.Get(LocalizationKeys.ViewportModeWireframe);
    public string ViewportModeShaded => _localization.Get(LocalizationKeys.ViewportModeShaded);
    public string ViewportModeXRay => _localization.Get(LocalizationKeys.ViewportModeXRay);
    public string ViewportProjectionLabel => _localization.Get(LocalizationKeys.ViewportProjectionLabel);
    public string ViewportProjectionPerspective => _localization.Get(LocalizationKeys.ViewportProjectionPerspective);
    public string ViewportProjectionOrtho => _localization.Get(LocalizationKeys.ViewportProjectionOrtho);
    public string ViewportViewIso => _localization.Get(LocalizationKeys.ViewportViewIso);
    public string ViewportViewFront => _localization.Get(LocalizationKeys.ViewportViewFront);
    public string ViewportViewBack => _localization.Get(LocalizationKeys.ViewportViewBack);
    public string ViewportViewLeft => _localization.Get(LocalizationKeys.ViewportViewLeft);
    public string ViewportViewRight => _localization.Get(LocalizationKeys.ViewportViewRight);
    public string ViewportViewTop => _localization.Get(LocalizationKeys.ViewportViewTop);
    public string ViewportViewBottom => _localization.Get(LocalizationKeys.ViewportViewBottom);
    public string ViewportZoomExtents => _localization.Get(LocalizationKeys.ViewportZoomExtents);
    public string ViewportShowGrid => _localization.Get(LocalizationKeys.ViewportShowGrid);
    public string ViewportShowAxes => _localization.Get(LocalizationKeys.ViewportShowAxes);
    public string ViewportUnitLabel => _localization.Get(LocalizationKeys.ViewportUnitLabel);
    public string ViewportHudStats => _localization.Get(LocalizationKeys.ViewportHudStats);
    public string ViewportHudUnits => _localization.Get(LocalizationKeys.ViewportHudUnits);
    public string ViewportHudSelection => _localization.Get(LocalizationKeys.ViewportHudSelection);
    public string ViewportHudSelectionNone => _localization.Get(LocalizationKeys.ViewportHudSelectionNone);
    public string ViewportCameraHint => _localization.Get(LocalizationKeys.ViewportCameraHint);

    private void OnCultureChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
