using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Tools.M6;

/// <summary>
/// Ключи строк модуля M6 (массивы, зеркало, Follow Me, вращение): RU/EN
/// держатся здесь, в своём модуле, и не добавляются в общие
/// <c>Resources/Strings*.resx</c> — координатор подключает их к своему сервису
/// локализации при интеграции.
/// </summary>
public static class M6ToolStrings
{
    /// <summary>Ключ имени «Массив (линейный)».</summary>
    public const string LinearArrayName = "M6.Tool.ArrayLinear";

    /// <summary>Ключ имени «Массив (круговой)».</summary>
    public const string PolarArrayName = "M6.Tool.ArrayPolar";

    /// <summary>Ключ имени «Зеркало (ось)».</summary>
    public const string MirrorAxisName = "M6.Tool.MirrorAxis";

    /// <summary>Ключ имени «Зеркало (2 точки)».</summary>
    public const string MirrorPointsName = "M6.Tool.MirrorPoints";

    /// <summary>Ключ имени «Зеркало (грань)».</summary>
    public const string MirrorFaceName = "M6.Tool.MirrorFace";

    /// <summary>Ключ имени «Follow Me».</summary>
    public const string FollowMeName = "M6.Tool.FollowMe";

    /// <summary>Ключ имени «Вращение».</summary>
    public const string RevolveName = "M6.Tool.Revolve";

    /// <summary>Ключ подсказки VCB «шаг, мм».</summary>
    public const string VcbSpacing = "M6.Vcb.Spacing";

    /// <summary>Ключ подсказки VCB «угол, °».</summary>
    public const string VcbAngle = "M6.Vcb.Angle";

    /// <summary>Ключ подсказки VCB «количество».</summary>
    public const string VcbCount = "M6.Vcb.Count";

    /// <summary>Ключ подсказки VCB «сегменты».</summary>
    public const string VcbSegments = "M6.Vcb.Segments";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (LinearArrayName, EditorCulture.Russian) => "Массив (линейный)",
        (LinearArrayName, EditorCulture.English) => "Linear Array",
        (PolarArrayName, EditorCulture.Russian) => "Массив (круговой)",
        (PolarArrayName, EditorCulture.English) => "Polar Array",
        (MirrorAxisName, EditorCulture.Russian) => "Зеркало (ось)",
        (MirrorAxisName, EditorCulture.English) => "Mirror (Axis)",
        (MirrorPointsName, EditorCulture.Russian) => "Зеркало (2 точки)",
        (MirrorPointsName, EditorCulture.English) => "Mirror (2 Points)",
        (MirrorFaceName, EditorCulture.Russian) => "Зеркало (грань)",
        (MirrorFaceName, EditorCulture.English) => "Mirror (Face)",
        (FollowMeName, EditorCulture.Russian) => "Follow Me",
        (FollowMeName, EditorCulture.English) => "Follow Me",
        (RevolveName, EditorCulture.Russian) => "Вращение",
        (RevolveName, EditorCulture.English) => "Revolve",
        (VcbSpacing, EditorCulture.Russian) => "Шаг, мм",
        (VcbSpacing, EditorCulture.English) => "Spacing, mm",
        (VcbAngle, EditorCulture.Russian) => "Угол, °",
        (VcbAngle, EditorCulture.English) => "Angle, °",
        (VcbCount, EditorCulture.Russian) => "Количество",
        (VcbCount, EditorCulture.English) => "Count",
        (VcbSegments, EditorCulture.Russian) => "Сегменты",
        (VcbSegments, EditorCulture.English) => "Segments",
        _ => key,
    };

    /// <summary>Имя инструмента для текущего или указанного языка.</summary>
    public static string GetName(string commandId, EditorCulture culture) => commandId switch
    {
        Core.Tools.M6.LinearArrayTool.ToolId => Get(LinearArrayName, culture),
        Core.Tools.M6.PolarArrayTool.ToolId => Get(PolarArrayName, culture),
        Core.Tools.M6.MirrorTool.AxisToolId => Get(MirrorAxisName, culture),
        Core.Tools.M6.MirrorTool.TwoPointsToolId => Get(MirrorPointsName, culture),
        Core.Tools.M6.MirrorTool.FaceToolId => Get(MirrorFaceName, culture),
        Core.Tools.M6.FollowMeTool.ToolId => Get(FollowMeName, culture),
        Core.Tools.M6.RevolveTool.ToolId => Get(RevolveName, culture),
        _ => commandId,
    };

    /// <summary>Подсказка числового ввода (VCB) инструмента.</summary>
    public static string GetVcbPrompt(string commandId, EditorCulture culture) => commandId switch
    {
        Core.Tools.M6.LinearArrayTool.ToolId => Get(VcbSpacing, culture),
        Core.Tools.M6.PolarArrayTool.ToolId => Get(VcbAngle, culture),
        Core.Tools.M6.RevolveTool.ToolId => Get(VcbAngle, culture),
        _ => string.Empty,
    };
}
