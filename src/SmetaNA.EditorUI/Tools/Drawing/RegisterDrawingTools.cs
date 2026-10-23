using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.Tools.Drawing;

/// <summary>Описание инструмента рисования для ленты и маршрутизации команд.</summary>
/// <param name="CommandId">Идентификатор команды (совпадает с <see cref="ITool.Id"/>).</param>
/// <param name="NameKey">Ключ имени: общий ключ локализации или ключ модуля.</param>
/// <param name="VcbKind">Вид числового ввода в строке значений (или <c>null</c>).</param>
/// <param name="Factory">Фабрика экземпляра инструмента.</param>
/// <param name="GestureHint">Подсказка горячей клавиши (по каталогу команд).</param>
public sealed record DrawingToolDefinition(
    string CommandId,
    string NameKey,
    VcbInputKind? VcbKind,
    Func<ITool> Factory,
    string GestureHint);

/// <summary>
/// Модуль подключения инструментов рисования (задача 2.1) в UI: единая точка
/// регистрации «Линия», «Прямоугольник», «Окружность», «Многоугольник»,
/// «Дуга (2 точки)», «Дуга (3 точки)» и «Кривая».
/// <para>
/// Существующие файлы EditorUI не меняются: координатор подключает модуль,
/// вызывая <see cref="TryActivate"/> из обработчика выбора инструмента ленты и
/// используя <see cref="RibbonDefinitions"/>/<see cref="DrawingToolStrings"/>
/// для кнопок и строк (общий resx не трогается).
/// </para>
/// </summary>
public static class RegisterDrawingTools
{
    /// <summary>Идентификатор команды «Дуга (3 точки)» (нет в общем каталоге команд).</summary>
    public const string ToolArc3PointId = Arc3PointTool.ToolId;

    private static readonly IReadOnlyList<DrawingToolDefinition> Definitions =
    [
        new(LineTool.ToolId, LocalizationKeys.ToolLine, VcbInputKind.Length, () => new LineTool(), "L"),
        new(RectangleTool.ToolId, LocalizationKeys.ToolRectangle, VcbInputKind.Length, () => new RectangleTool(), "R"),
        new(CircleTool.ToolId, LocalizationKeys.ToolCircle, VcbInputKind.Radius, () => new CircleTool(), "C"),
        new(PolygonTool.ToolId, LocalizationKeys.ToolPolygon, VcbInputKind.Radius, () => new PolygonTool(), "G"),
        new(Arc2PointTool.ToolId, LocalizationKeys.ToolArc, VcbInputKind.Angle, () => new Arc2PointTool(), "A"),
        new(
            Arc3PointTool.ToolId,
            DrawingToolStrings.ToolArc3PointName,
            VcbInputKind.Radius,
            () => new Arc3PointTool(),
            "Shift+A"),
        new(FreehandTool.ToolId, LocalizationKeys.ToolFreehand, null, () => new FreehandTool(), "F"),
    ];

    /// <summary>Инструменты рисования в порядке ленты.</summary>
    public static IReadOnlyList<DrawingToolDefinition> Tools => Definitions;

    /// <summary>Инструмент рисования с этим идентификатором команды?</summary>
    public static bool IsDrawingTool(string commandId)
        => Definitions.Any(definition => definition.CommandId == commandId);

    /// <summary>Новый экземпляр инструмента по идентификатору команды; <c>null</c> — не наш.</summary>
    public static ITool? Create(string commandId)
        => Definitions.FirstOrDefault(definition => definition.CommandId == commandId)?.Factory();

    /// <summary>
    /// Активировать инструмент рисования в контроллере; <c>false</c> — команда
    /// не относится к модулю (её обрабатывает существующий код).
    /// </summary>
    public static bool TryActivate(ToolController controller, string commandId)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (Create(commandId) is not { } tool)
        {
            return false;
        }

        controller.Activate(tool);
        return true;
    }

    /// <summary>
    /// Описания кнопок ленты в формате <see cref="ToolDefinition"/> главного окна
    /// (для добавления в коллекции панели инструментов без правки её кода).
    /// </summary>
    public static IReadOnlyList<ToolDefinition> RibbonDefinitions { get; } =
        [.. Definitions.Select(definition => new ToolDefinition(definition.CommandId, definition.NameKey, definition.VcbKind))];

    /// <summary>Имя инструмента для текущего или указанного языка.</summary>
    public static string DisplayName(string commandId, EditorCulture culture)
        => DrawingToolStrings.GetName(commandId, culture);
}
