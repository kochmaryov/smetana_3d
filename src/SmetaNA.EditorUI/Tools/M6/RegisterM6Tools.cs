using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.M6;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.Tools.M6;

/// <summary>Описание инструмента M6 для ленты и маршрутизации команд.</summary>
/// <param name="CommandId">Идентификатор команды (совпадает с <see cref="ITool.Id"/>).</param>
/// <param name="NameKey">Ключ имени в <see cref="M6ToolStrings"/>.</param>
/// <param name="VcbKind">Вид числового ввода в строке значений (или <c>null</c>).</param>
/// <param name="Factory">Фабрика экземпляра инструмента.</param>
/// <param name="GestureHint">Подсказка горячей клавиши (по каталогу команд).</param>
public sealed record M6ToolDefinition(
    string CommandId,
    string NameKey,
    VcbInputKind? VcbKind,
    Func<ITool> Factory,
    string GestureHint);

/// <summary>
/// Модуль подключения инструментов M6 (задачи 5.1–5.2) в UI: единая точка
/// регистрации «Массив (линейный)», «Массив (круговой)», «Зеркало (ось)»,
/// «Зеркало (2 точки)», «Зеркало (грань)», «Follow Me» и «Вращение».
/// <para>
/// Существующие файлы EditorUI не меняются: координатор подключает модуль,
/// вызывая <see cref="TryActivate"/> из обработчика выбора инструмента ленты и
/// используя <see cref="RibbonDefinitions"/>/<see cref="M6ToolStrings"/> для
/// кнопок и строк (общий resx не трогается):
/// <code>
/// if (RegisterM6Tools.TryActivate(_session.Tools, commandId)) { ... }
/// foreach (ToolDefinition definition in RegisterM6Tools.RibbonDefinitions) { /* кнопки */ }
/// </code>
/// </para>
/// <para>
/// Числовой ввод: линейный массив — шаг (мм), затем количество копий;
/// круговой массив — угловой шаг (°), затем количество; вращение — угол (°),
/// затем число сегментов. Зеркало числом не управляется: опция «копия» —
/// Ctrl при фиксирующем клике.
/// </para>
/// </summary>
public static class RegisterM6Tools
{
    private static readonly IReadOnlyList<M6ToolDefinition> Definitions =
    [
        new(
            LinearArrayTool.ToolId,
            M6ToolStrings.LinearArrayName,
            VcbInputKind.Length,
            static () => new LinearArrayTool(),
            "Alt+M"),
        new(
            PolarArrayTool.ToolId,
            M6ToolStrings.PolarArrayName,
            VcbInputKind.Angle,
            static () => new PolarArrayTool(),
            "Alt+Shift+M"),
        new(
            MirrorTool.AxisToolId,
            M6ToolStrings.MirrorAxisName,
            null,
            static () => new MirrorTool(MirrorPlaneMode.Axis),
            "Alt+X"),
        new(
            MirrorTool.TwoPointsToolId,
            M6ToolStrings.MirrorPointsName,
            null,
            static () => new MirrorTool(MirrorPlaneMode.TwoPoints),
            "Alt+2"),
        new(
            MirrorTool.FaceToolId,
            M6ToolStrings.MirrorFaceName,
            null,
            static () => new MirrorTool(MirrorPlaneMode.Face),
            "Alt+F"),
        new(
            FollowMeTool.ToolId,
            M6ToolStrings.FollowMeName,
            null,
            static () => new FollowMeTool(),
            "Alt+W"),
        new(
            RevolveTool.ToolId,
            M6ToolStrings.RevolveName,
            VcbInputKind.Angle,
            static () => new RevolveTool(),
            "Alt+V"),
    ];

    /// <summary>Инструменты M6 в порядке ленты.</summary>
    public static IReadOnlyList<M6ToolDefinition> Tools => Definitions;

    /// <summary>Инструмент M6 с этим идентификатором команды?</summary>
    public static bool IsM6Tool(string commandId)
        => Definitions.Any(definition => definition.CommandId == commandId);

    /// <summary>Новый экземпляр инструмента по идентификатору команды; <c>null</c> — не наш.</summary>
    public static ITool? Create(string commandId)
        => Definitions.FirstOrDefault(definition => definition.CommandId == commandId)?.Factory();

    /// <summary>
    /// Активировать инструмент M6 в контроллере; <c>false</c> — команда
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
        => M6ToolStrings.GetName(commandId, culture);

    /// <summary>Подсказка числового ввода (VCB) инструмента.</summary>
    public static string VcbPrompt(string commandId, EditorCulture culture)
        => M6ToolStrings.GetVcbPrompt(commandId, culture);
}
