using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Состояние редактора окна: сцена и история редактирования
/// (контракты SmetaNA.Core используются только для чтения/вызова).
/// </summary>
public sealed class EditorSession
{
    public EditorSession()
    {
        UndoStack undoStack = new();
        Scene = new Scene();
        History = new UndoHistoryService(undoStack);
        Shortcuts = new Commands.ShortcutMap();
    }

    public Scene Scene { get; }

    public UndoHistoryService History { get; }

    public Commands.ShortcutMap Shortcuts { get; }
}
