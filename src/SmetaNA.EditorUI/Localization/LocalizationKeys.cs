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
    public const string ToolArc3Point = "Tool.Arc3Point";
    public const string ToolFreehand = "Tool.Freehand";
    public const string ToolPushPull = "Tool.PushPull";
    public const string ToolOffset = "Tool.Offset";
    public const string ToolMove = "Tool.Move";
    public const string ToolRotate = "Tool.Rotate";
    public const string ToolScale = "Tool.Scale";
    public const string ToolEraser = "Tool.Eraser";
    public const string ToolSoften = "Tool.Soften";
    public const string ToolPaintBucket = "Tool.PaintBucket";
    public const string ToolDimension = "Tool.Dimension";
    public const string ToolScaleToDimension = "Tool.ScaleToDimension";
    public const string ToolStubStatus = "Tool.StubStatus";
    public const string ToolStubToolTip = "Tool.StubToolTip";
    public const string ToolNone = "Tool.None";

    // Объяснения инструментов для подсказок режима «Новичок» (задача 6.2).
    public const string ToolHintSelect = "Tool.Hint.Select";
    public const string ToolHintLine = "Tool.Hint.Line";
    public const string ToolHintRectangle = "Tool.Hint.Rectangle";
    public const string ToolHintCircle = "Tool.Hint.Circle";
    public const string ToolHintPolygon = "Tool.Hint.Polygon";
    public const string ToolHintArc = "Tool.Hint.Arc";
    public const string ToolHintArc3Point = "Tool.Hint.Arc3Point";
    public const string ToolHintFreehand = "Tool.Hint.Freehand";
    public const string ToolHintPushPull = "Tool.Hint.PushPull";
    public const string ToolHintOffset = "Tool.Hint.Offset";
    public const string ToolHintMove = "Tool.Hint.Move";
    public const string ToolHintRotate = "Tool.Hint.Rotate";
    public const string ToolHintScale = "Tool.Hint.Scale";
    public const string ToolHintEraser = "Tool.Hint.Eraser";
    public const string ToolHintSoften = "Tool.Hint.Soften";
    public const string ToolHintPaintBucket = "Tool.Hint.PaintBucket";
    public const string ToolHintDimension = "Tool.Hint.Dimension";
    public const string ToolHintScaleToDimension = "Tool.Hint.ScaleToDimension";

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
    public const string PropertiesToolHint = "Properties.ToolHint";
    public const string PropertiesToolShortcut = "Properties.ToolShortcut";
    public const string PropertiesToolInput = "Properties.ToolInput";

    // Режим «Новичок»/«Профи» (задача 6.2).
    public const string ModeLabel = "Mode.Label";
    public const string ModeNovice = "Mode.Novice";
    public const string ModeExpert = "Mode.Expert";
    public const string ModeSwitched = "Mode.Switched";
    public const string ModeNoviceHint = "Mode.NoviceHint";

    // Интерактивный урок S1 «стакан за 15 минут» (задача 6.2).
    public const string TutorialTitle = "Tutorial.Title";
    public const string TutorialStart = "Tutorial.Start";
    public const string TutorialClose = "Tutorial.Close";
    public const string TutorialNext = "Tutorial.Next";
    public const string TutorialBack = "Tutorial.Back";
    public const string TutorialRestart = "Tutorial.Restart";
    public const string TutorialCompleted = "Tutorial.Completed";
    public const string TutorialProgress = "Tutorial.Progress";
    public const string TutorialS1Name = "Tutorial.S1.Name";
    public const string TutorialS1Intro = "Tutorial.S1.Intro";
    public const string TutorialS1Step1 = "Tutorial.S1.Step1";
    public const string TutorialS1Step2 = "Tutorial.S1.Step2";
    public const string TutorialS1Step3 = "Tutorial.S1.Step3";
    public const string TutorialS1Step4 = "Tutorial.S1.Step4";
    public const string TutorialS1Step5 = "Tutorial.S1.Step5";
    public const string TutorialS1Step6 = "Tutorial.S1.Step6";
    public const string TutorialS1Step7 = "Tutorial.S1.Step7";
    public const string TutorialS1Done = "Tutorial.S1.Done";

    // Экран лицензирования (задача 6.4).
    public const string LicenseTitle = "License.Title";
    public const string LicenseOpen = "License.Open";
    public const string LicenseStatus = "License.Status";
    public const string LicenseValidUntil = "License.ValidUntil";
    public const string LicenseKeyLabel = "License.KeyLabel";
    public const string LicenseKeyPlaceholder = "License.KeyPlaceholder";
    public const string LicenseKeyValidate = "License.KeyValidate";
    public const string LicenseKeyEmpty = "License.KeyEmpty";
    public const string LicenseKeyValid = "License.KeyValid";
    public const string LicenseKeyInvalid = "License.KeyInvalid";
    public const string LicenseActivationRequest = "License.ActivationRequest";
    public const string LicenseActivationRequestHint = "License.ActivationRequestHint";
    public const string LicenseTokenLabel = "License.TokenLabel";
    public const string LicenseTokenPlaceholder = "License.TokenPlaceholder";
    public const string LicenseApplyToken = "License.ApplyToken";
    public const string LicenseTokenEmpty = "License.TokenEmpty";
    public const string LicenseTokenApplied = "License.TokenApplied";
    public const string LicenseTokenFailed = "License.TokenFailed";
    public const string LicenseTrialTitle = "License.TrialTitle";
    public const string LicenseTrialStart = "License.TrialStart";
    public const string LicenseTrialStarted = "License.TrialStarted";
    public const string LicenseTrialNotStarted = "License.TrialNotStarted";
    public const string LicenseTrialDaysLeft = "License.TrialDaysLeft";
    public const string LicenseTrialUntil = "License.TrialUntil";
    public const string LicenseDeactivate = "License.Deactivate";
    public const string LicenseDeactivated = "License.Deactivated";
    public const string LicenseDeactivateFailed = "License.DeactivateFailed";
    public const string LicenseDeactivateReceipt = "License.DeactivateReceipt";
    public const string LicenseDeactivateReceiptHint = "License.DeactivateReceiptHint";
    public const string LicenseDeviceLabel = "License.DeviceLabel";
    public const string LicenseOfflineNote = "License.OfflineNote";
    public const string LicenseDevKeyWarning = "License.DevKeyWarning";
    public const string LicenseRefresh = "License.Refresh";
    public const string LicenseClose = "License.Close";
    public const string LicenseStateNotLicensed = "License.State.NotLicensed";
    public const string LicenseStateNotActivated = "License.State.NotActivated";
    public const string LicenseStateLicensed = "License.State.Licensed";
    public const string LicenseStateLicenseExpired = "License.State.LicenseExpired";
    public const string LicenseStateDeviceMismatch = "License.State.DeviceMismatch";
    public const string LicenseStateRevoked = "License.State.Revoked";
    public const string LicenseStateDeactivated = "License.State.Deactivated";
    public const string LicenseStateTrialActive = "License.State.TrialActive";
    public const string LicenseStateTrialExpired = "License.State.TrialExpired";
    public const string LicenseStateInvalidKey = "License.State.InvalidKey";
    public const string LicenseStateInvalidState = "License.State.InvalidState";
    public const string LicenseProblemInvalidSignature = "License.Problem.InvalidSignature";
    public const string LicenseProblemMalformed = "License.Problem.Malformed";
    public const string LicenseProblemNotYetValid = "License.Problem.NotYetValid";
    public const string LicenseProblemExpired = "License.Problem.Expired";
    public const string LicenseProblemLicenseeMismatch = "License.Problem.LicenseeMismatch";
    public const string LicenseTokenProblemInvalidSignature = "License.TokenProblem.InvalidSignature";
    public const string LicenseTokenProblemDeviceMismatch = "License.TokenProblem.DeviceMismatch";
    public const string LicenseTokenProblemNotYetValid = "License.TokenProblem.NotYetValid";
    public const string LicenseTokenProblemExpired = "License.TokenProblem.Expired";
    public const string LicenseTokenProblemRevoked = "License.TokenProblem.Revoked";
    public const string LicenseTokenProblemKeyMismatch = "License.TokenProblem.KeyMismatch";
    public const string LicenseTokenProblemKeyExpired = "License.TokenProblem.KeyExpired";
    public const string LicenseTokenProblemInvalidKey = "License.TokenProblem.InvalidKey";

    // Пункты меню (задача 6.1).
    public const string MenuFile = "Menu.File";
    public const string MenuEdit = "Menu.Edit";
    public const string MenuView = "Menu.View";
    public const string MenuHelp = "Menu.Help";

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
