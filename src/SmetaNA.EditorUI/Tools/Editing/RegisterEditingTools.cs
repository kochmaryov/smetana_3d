using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.Tools.Editing;

/// <summary>
/// Регистрация инструмента редактирования: идентификатор команды, фабрика
/// инструмента, тип числового ввода (VCB) и ключ имени в
/// <see cref="EditingToolStrings"/>.
/// </summary>
/// <param name="CommandId">Идентификатор команды (совпадает с <see cref="ITool.Id"/>).</param>
/// <param name="Factory">Фабрика нового экземпляра инструмента.</param>
/// <param name="VcbKind">Тип числового ввода или <c>null</c> (ввода нет).</param>
/// <param name="NameKey">Ключ имени в <see cref="EditingToolStrings"/>.</param>
public sealed record EditingToolRegistration(
    string CommandId,
    Func<ITool> Factory,
    VcbInputKind? VcbKind,
    string NameKey);

/// <summary>
/// Модуль интеграции инструментов редактирования (задача 2.3) в UI: регистрация
/// Offset, Move/Copy, Rotate, Scale, Eraser, Soften/Smooth и Paint Bucket.
/// <para>
/// Подключение координатором (существующие файлы EditorUI не изменяются):
/// <code>
/// foreach (ToolDefinition definition in RegisterEditingTools.Definitions) { /* кнопки ленты */ }
/// ITool tool = RegisterEditingTools.Create(EditingToolStrings.Keys.Move)!;
/// toolController.Activate(tool);
/// string name = RegisterEditingTools.GetName(EditingToolStrings.Keys.Move, EditorCulture.Russian);
/// </code>
/// Имена инструментов локализуются строками модуля (<see cref="EditingToolStrings"/>),
/// а не общими resx.
/// </para>
/// </summary>
public static class RegisterEditingTools
{
    private static readonly IReadOnlyList<EditingToolRegistration> ToolRegistrations =
    [
        new(OffsetTool.ToolId, static () => new OffsetTool(), VcbInputKind.Length, EditingToolStrings.Keys.Offset),
        new(MoveTool.ToolId, static () => new MoveTool(), VcbInputKind.Length, EditingToolStrings.Keys.Move),
        new(RotateTool.ToolId, static () => new RotateTool(), VcbInputKind.Angle, EditingToolStrings.Keys.Rotate),
        new(ScaleTool.ToolId, static () => new ScaleTool(), VcbInputKind.Radius, EditingToolStrings.Keys.Scale),
        new(EraserTool.ToolId, static () => new EraserTool(), null, EditingToolStrings.Keys.Eraser),
        new(SoftenSmoothTool.ToolId, static () => new SoftenSmoothTool(), VcbInputKind.Angle, EditingToolStrings.Keys.Soften),
        new(PaintBucketTool.ToolId, static () => new PaintBucketTool(), null, EditingToolStrings.Keys.Paint),
    ];

    /// <summary>Все регистрации инструментов редактирования.</summary>
    public static IReadOnlyList<EditingToolRegistration> Tools => ToolRegistrations;

    /// <summary>Описания для ленты/панели инструментов (совместимо с <see cref="ToolDefinition"/>).</summary>
    public static IReadOnlyList<ToolDefinition> Definitions { get; } =
        [.. ToolRegistrations.Select(registration =>
            new ToolDefinition(registration.CommandId, registration.NameKey, registration.VcbKind))];

    /// <summary>Новый экземпляр инструмента по идентификатору команды; <c>null</c> — команда не найдена.</summary>
    public static ITool? Create(string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        EditingToolRegistration? registration = Find(commandId);
        return registration?.Factory();
    }

    /// <summary>Локализованное имя инструмента (строки модуля, RU/EN).</summary>
    public static string GetName(string commandId, EditorCulture culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        EditingToolRegistration? registration = Find(commandId);
        return registration is null
            ? commandId
            : EditingToolStrings.Get(registration.NameKey, culture);
    }

    /// <summary>Ключ имени инструмента или <c>null</c>, если команда не найдена.</summary>
    public static string? GetNameKey(string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        return Find(commandId)?.NameKey;
    }

    private static EditingToolRegistration? Find(string commandId)
    {
        foreach (EditingToolRegistration registration in ToolRegistrations)
        {
            if (string.Equals(registration.CommandId, commandId, StringComparison.Ordinal))
            {
                return registration;
            }
        }

        return null;
    }
}
