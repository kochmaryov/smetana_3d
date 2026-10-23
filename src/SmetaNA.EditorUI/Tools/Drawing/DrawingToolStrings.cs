using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Tools.Drawing;

/// <summary>
/// Строки модуля рисования (задача 2.1): RU/EN держатся здесь, в своём модуле,
/// и не добавляются в общие <c>Resources/Strings*.resx</c> — координатор
/// подключает их к своему сервису локализации при интеграции.
/// </summary>
public static class DrawingToolStrings
{
    /// <summary>Ключ имени инструмента «Дуга (3 точки)».</summary>
    public const string ToolArc3PointName = "Drawing.Tool.Arc3Point";

    /// <summary>Ключ подсказки инструмента «Дуга (3 точки)».</summary>
    public const string ToolArc3PointToolTip = "Drawing.Tool.Arc3Point.ToolTip";

    /// <summary>Ключ подсказки VCB «длина».</summary>
    public const string VcbLength = "Drawing.Vcb.Length";

    /// <summary>Ключ подсказки VCB «радиус».</summary>
    public const string VcbRadius = "Drawing.Vcb.Radius";

    /// <summary>Ключ подсказки VCB «угол».</summary>
    public const string VcbAngle = "Drawing.Vcb.Angle";

    /// <summary>Ключ подсказки VCB «число сторон».</summary>
    public const string VcbSides = "Drawing.Vcb.Sides";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (ToolArc3PointName, EditorCulture.Russian) => "Дуга (3 точки)",
        (ToolArc3PointName, EditorCulture.English) => "3-Point Arc",
        (ToolArc3PointToolTip, EditorCulture.Russian) =>
            "Дуга по трём точкам: начало, точка на дуге, конец",
        (ToolArc3PointToolTip, EditorCulture.English) =>
            "3-point arc: start, point on arc, end",
        (VcbLength, EditorCulture.Russian) => "Длина, мм",
        (VcbLength, EditorCulture.English) => "Length, mm",
        (VcbRadius, EditorCulture.Russian) => "Радиус, мм",
        (VcbRadius, EditorCulture.English) => "Radius, mm",
        (VcbAngle, EditorCulture.Russian) => "Угол, °",
        (VcbAngle, EditorCulture.English) => "Angle, °",
        (VcbSides, EditorCulture.Russian) => "Стороны",
        (VcbSides, EditorCulture.English) => "Sides",
        _ => key,
    };

    /// <summary>Имя инструмента рисования (для ленты и статуса).</summary>
    public static string GetName(string commandId, EditorCulture culture) => commandId switch
    {
        Core.Tools.Drawing.LineTool.ToolId => culture == EditorCulture.Russian ? "Линия" : "Line",
        Core.Tools.Drawing.RectangleTool.ToolId => culture == EditorCulture.Russian ? "Прямоугольник" : "Rectangle",
        Core.Tools.Drawing.CircleTool.ToolId => culture == EditorCulture.Russian ? "Окружность" : "Circle",
        Core.Tools.Drawing.PolygonTool.ToolId => culture == EditorCulture.Russian ? "Многоугольник" : "Polygon",
        Core.Tools.Drawing.Arc2PointTool.ToolId => culture == EditorCulture.Russian ? "Дуга (2 точки)" : "2-Point Arc",
        Core.Tools.Drawing.Arc3PointTool.ToolId => Get(ToolArc3PointName, culture),
        Core.Tools.Drawing.FreehandTool.ToolId => culture == EditorCulture.Russian ? "Кривая" : "Freehand",
        _ => commandId,
    };

    /// <summary>Подсказка числового ввода (VCB) инструмента.</summary>
    public static string GetVcbPrompt(string commandId, EditorCulture culture) => commandId switch
    {
        Core.Tools.Drawing.LineTool.ToolId => Get(VcbLength, culture),
        Core.Tools.Drawing.RectangleTool.ToolId => Get(VcbLength, culture),
        Core.Tools.Drawing.CircleTool.ToolId => Get(VcbRadius, culture),
        Core.Tools.Drawing.PolygonTool.ToolId => Get(VcbSides, culture),
        Core.Tools.Drawing.Arc2PointTool.ToolId => Get(VcbAngle, culture),
        Core.Tools.Drawing.Arc3PointTool.ToolId => Get(VcbRadius, culture),
        _ => string.Empty,
    };
}
