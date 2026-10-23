using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Vcb;

namespace SmetaNA.EditorUI.Localization;

/// <summary>
/// Ключи строк ресурсов и отображения доменных значений на ключи.
/// Полный список ключей — в <c>Resources/Strings.resx</c> (RU) и <c>Strings.en.resx</c> (EN);
/// паритет проверяется тестами.
/// </summary>
public static class LocalizationKeys
{
    public const string AppTitle = "App.Title";

    public const string ToolbarPrimary = "Toolbar.Primary";
    public const string ToolbarAdvanced = "Toolbar.Advanced";
    public const string ToolbarAdvancedToggle = "Toolbar.AdvancedToggle";

    public const string ProjectNew = "Project.New";
    public const string ProjectName = "Project.Name";

    public const string ToolSelect = "Tool.Select";
    public const string ToolLine = "Tool.Line";
    public const string ToolRectangle = "Tool.Rectangle";
    public const string ToolCircle = "Tool.Circle";
    public const string ToolPolygon = "Tool.Polygon";
    public const string ToolArc = "Tool.Arc";
    public const string ToolFreehand = "Tool.Freehand";
    public const string ToolPushPull = "Tool.PushPull";
    public const string ToolOffset = "Tool.Offset";
    public const string ToolMove = "Tool.Move";
    public const string ToolRotate = "Tool.Rotate";
    public const string ToolScale = "Tool.Scale";
    public const string ToolEraser = "Tool.Eraser";
    public const string ToolSoften = "Tool.Soften";
    public const string ToolPaintBucket = "Tool.PaintBucket";
    public const string ToolStubStatus = "Tool.StubStatus";
    public const string ToolStubToolTip = "Tool.StubToolTip";
    public const string ToolNone = "Tool.None";

    public const string EditUndo = "Edit.Undo";
    public const string EditRedo = "Edit.Redo";
    public const string EditUndoNamed = "Edit.UndoNamed";
    public const string EditRedoNamed = "Edit.RedoNamed";

    public const string HistoryTitle = "History.Title";
    public const string HistoryEmpty = "History.Empty";
    public const string HistoryCurrent = "History.Current";
    public const string HistoryStubCommand = "History.StubCommand";

    public const string PropertiesTitle = "Properties.Title";
    public const string PropertiesActiveTool = "Properties.ActiveTool";
    public const string PropertiesSelection = "Properties.Selection";
    public const string PropertiesNoSelection = "Properties.NoSelection";
    public const string PropertiesScene = "Properties.Scene";
    public const string PropertiesSceneUnit = "Properties.SceneUnit";
    public const string PropertiesSceneNodes = "Properties.SceneNodes";
    public const string PropertiesStubNote = "Properties.StubNote";

    public const string ViewportTitle = "Viewport.Title";
    public const string ViewportPlaceholder = "Viewport.Placeholder";
    public const string ViewportPlaceholderHint = "Viewport.PlaceholderHint";

    public const string VcbPromptReady = "Vcb.PromptReady";
    public const string VcbPromptLength = "Vcb.PromptLength";
    public const string VcbPromptRadius = "Vcb.PromptRadius";
    public const string VcbPromptAngle = "Vcb.PromptAngle";
    public const string VcbPromptCoordinates = "Vcb.PromptCoordinates";
    public const string VcbWatermark = "Vcb.Watermark";
    public const string VcbCommitted = "Vcb.Committed";
    public const string VcbErrorEmpty = "Vcb.ErrorEmpty";
    public const string VcbErrorInvalidNumber = "Vcb.ErrorInvalidNumber";
    public const string VcbErrorNotFinite = "Vcb.ErrorNotFinite";
    public const string VcbErrorNegative = "Vcb.ErrorNegative";
    public const string VcbErrorNotPositive = "Vcb.ErrorNotPositive";
    public const string VcbErrorUnknownUnit = "Vcb.ErrorUnknownUnit";
    public const string VcbErrorComponentCount = "Vcb.ErrorComponentCount";

    public const string UnitMillimeter = "Unit.Millimeter";
    public const string UnitCentimeter = "Unit.Centimeter";
    public const string UnitMeter = "Unit.Meter";
    public const string UnitInch = "Unit.Inch";

    public const string ShortcutsTitle = "Shortcuts.Title";
    public const string ShortcutsHint = "Shortcuts.Hint";
    public const string ShortcutsCommand = "Shortcuts.Command";
    public const string ShortcutsGesture = "Shortcuts.Gesture";
    public const string ShortcutsApply = "Shortcuts.Apply";
    public const string ShortcutsReset = "Shortcuts.Reset";
    public const string ShortcutsResetAll = "Shortcuts.ResetAll";
    public const string ShortcutsErrorInvalid = "Shortcuts.ErrorInvalid";
    public const string ShortcutsErrorConflict = "Shortcuts.ErrorConflict";
    public const string ShortcutsErrorUnknown = "Shortcuts.ErrorUnknown";
    public const string ShortcutsApplied = "Shortcuts.Applied";

    public const string CommandToggleTheme = "Command.ToggleTheme";
    public const string CommandToggleLanguage = "Command.ToggleLanguage";

    public const string ThemeLabel = "Theme.Label";
    public const string ThemeLight = "Theme.Light";
    public const string ThemeDark = "Theme.Dark";
    public const string ThemeSwitched = "Theme.Switched";

    public const string LanguageLabel = "Language.Label";
    public const string LanguageRussian = "Language.Russian";
    public const string LanguageEnglish = "Language.English";
    public const string LanguageSwitched = "Language.Switched";

    public const string StatusReady = "Status.Ready";

    public const string ViewportModeLabel = "Viewport.ModeLabel";
    public const string ViewportModeWireframe = "Viewport.Mode.Wireframe";
    public const string ViewportModeShaded = "Viewport.Mode.Shaded";
    public const string ViewportModeXRay = "Viewport.Mode.XRay";
    public const string ViewportProjectionLabel = "Viewport.ProjectionLabel";
    public const string ViewportProjectionPerspective = "Viewport.ProjectionPerspective";
    public const string ViewportProjectionOrtho = "Viewport.ProjectionOrtho";
    public const string ViewportViewIso = "Viewport.View.Iso";
    public const string ViewportViewFront = "Viewport.View.Front";
    public const string ViewportViewBack = "Viewport.View.Back";
    public const string ViewportViewLeft = "Viewport.View.Left";
    public const string ViewportViewRight = "Viewport.View.Right";
    public const string ViewportViewTop = "Viewport.View.Top";
    public const string ViewportViewBottom = "Viewport.View.Bottom";
    public const string ViewportZoomExtents = "Viewport.ZoomExtents";
    public const string ViewportShowGrid = "Viewport.ShowGrid";
    public const string ViewportShowAxes = "Viewport.ShowAxes";
    public const string ViewportUnitLabel = "Viewport.UnitLabel";
    public const string ViewportHudStats = "Viewport.HudStats";
    public const string ViewportHudUnits = "Viewport.HudUnits";
    public const string ViewportHudSelection = "Viewport.HudSelection";
    public const string ViewportHudSelectionNone = "Viewport.HudSelectionNone";
    public const string ViewportCameraHint = "Viewport.CameraHint";

    /// <summary>Ключ сообщения об ошибке валидации VCB.</summary>
    public static string ForVcbError(VcbError error) => error switch
    {
        VcbError.None => string.Empty,
        VcbError.Empty => VcbErrorEmpty,
        VcbError.InvalidNumber => VcbErrorInvalidNumber,
        VcbError.NotFinite => VcbErrorNotFinite,
        VcbError.Negative => VcbErrorNegative,
        VcbError.NotPositive => VcbErrorNotPositive,
        VcbError.UnknownUnit => VcbErrorUnknownUnit,
        VcbError.ComponentCount => VcbErrorComponentCount,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    /// <summary>Ключ подсказки VCB для типа ввода.</summary>
    public static string ForVcbPrompt(VcbInputKind kind) => kind switch
    {
        VcbInputKind.Length => VcbPromptLength,
        VcbInputKind.Radius => VcbPromptRadius,
        VcbInputKind.Angle => VcbPromptAngle,
        VcbInputKind.Coordinate => VcbPromptCoordinates,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Ключ названия единицы длины.</summary>
    public static string ForUnit(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => UnitMillimeter,
        LengthUnit.Centimeter => UnitCentimeter,
        LengthUnit.Meter => UnitMeter,
        LengthUnit.Inch => UnitInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };
}
