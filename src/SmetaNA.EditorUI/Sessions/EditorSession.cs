using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Tools;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Состояние редактора окна: сцена, история редактирования и контроллер
/// инструментов (контракты SmetaNA.Core используются только для чтения/вызова).
/// </summary>
public sealed class EditorSession
{
    public EditorSession()
    {
        UndoStack undoStack = new();
        Scene = new Scene();
        History = new UndoHistoryService(undoStack);
        Tools = new ToolController(Scene, undoStack);
        Shortcuts = new Commands.ShortcutMap();
    }

    public Scene Scene { get; }

    public UndoHistoryService History { get; }

    /// <summary>Инструменты редактора (маршрутизация ввода вьюпорта).</summary>
    public ToolController Tools { get; }

    public Commands.ShortcutMap Shortcuts { get; }
}
