namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Глифы интерфейса (icon-pack общего назначения): команды главного окна,
/// элементы вьюпорта, панели шорткатов и обучения. Тот же контракт, что и у
/// <see cref="ToolIconId"/>: поле 24×24, stroke-стиль без заливки, перекраска
/// через currentColor (отрисовка — <see cref="UiIcon"/>).
/// </summary>
public enum UiIconId
{
    None = 0,
    New,
    Undo,
    Redo,
    Mode,
    Theme,
    Language,
    Ortho,
    Grid,
    Axes,
    Iso,
    ViewFront,
    ViewBack,
    ViewLeft,
    ViewRight,
    ViewTop,
    ViewBottom,
    ZoomExtents,
    Gear,
    Check,
    Reset,
    ResetAll,
    Play,
    ChevronLeft,
    ChevronRight,
    ChevronUp,
    ChevronDown,
    ArrowLeft,
    ArrowRight,
    Restart,
    Close,
    Plus,
    Pencil,
    Trash,
    Import,
    Ban,
    Book,
    Info,
    History,
    Keyboard,
    Printer,
}
