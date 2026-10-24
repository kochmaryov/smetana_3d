using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Глифы палитры инструментов (контракт волны 0 для набора иконок).
/// Отрисовка — трек A: ресурсы 24×24, stroke-стиль, <c>currentColor</c>.
/// </summary>
public enum ToolIconId
{
    None = 0,
    Select,
    Line,
    Rect,
    Circle,
    Polygon,
    Arc,
    Freehand,
    PushPull,
    Offset,
    Move,
    Rotate,
    Scale,
    Eraser,
    Soften,
    Paint,
    ArrayLinear,
    ArrayPolar,
    Mirror,
    FollowMe,
    Revolve,
    Dimension,
    ScaleDim,
    Printer,
    Book,
}

/// <summary>
/// Маппинг команды на глиф — единый источник для палитры, диалога настройки
/// и подсказок (спека tool-palette §4 «Иконки»).
/// </summary>
public static class ToolIconMap
{
    public static ToolIconId ForCommand(string commandId) => commandId switch
    {
        ShortcutCatalog.ToolSelect => ToolIconId.Select,
        ShortcutCatalog.ToolLine => ToolIconId.Line,
        ShortcutCatalog.ToolRectangle => ToolIconId.Rect,
        ShortcutCatalog.ToolCircle => ToolIconId.Circle,
        ShortcutCatalog.ToolPolygon => ToolIconId.Polygon,
        ShortcutCatalog.ToolArc => ToolIconId.Arc,
        ShortcutCatalog.ToolArc3Point => ToolIconId.Arc,
        ShortcutCatalog.ToolFreehand => ToolIconId.Freehand,
        ShortcutCatalog.ToolPushPull => ToolIconId.PushPull,
        ShortcutCatalog.ToolOffset => ToolIconId.Offset,
        ShortcutCatalog.ToolMove => ToolIconId.Move,
        ShortcutCatalog.ToolRotate => ToolIconId.Rotate,
        ShortcutCatalog.ToolScale => ToolIconId.Scale,
        ShortcutCatalog.ToolEraser => ToolIconId.Eraser,
        ShortcutCatalog.ToolSoften => ToolIconId.Soften,
        ShortcutCatalog.ToolPaintBucket => ToolIconId.Paint,
        ShortcutCatalog.ToolArrayLinear => ToolIconId.ArrayLinear,
        ShortcutCatalog.ToolArrayPolar => ToolIconId.ArrayPolar,
        ShortcutCatalog.ToolMirrorAxis => ToolIconId.Mirror,
        ShortcutCatalog.ToolMirrorPoints => ToolIconId.Mirror,
        ShortcutCatalog.ToolMirrorFace => ToolIconId.Mirror,
        ShortcutCatalog.ToolFollowMe => ToolIconId.FollowMe,
        ShortcutCatalog.ToolRevolve => ToolIconId.Revolve,
        ShortcutCatalog.ToolDimension => ToolIconId.Dimension,
        ShortcutCatalog.ToolScaleToDimension => ToolIconId.ScaleDim,
        ToolPaletteCommandIds.PrintVolume => ToolIconId.Printer,
        ToolPaletteCommandIds.Tutorial => ToolIconId.Book,
        _ => ToolIconId.None,
    };
}
