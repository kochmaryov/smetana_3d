using SmetaNA.Core.Undo;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Заглушка команды истории для отработки связки «VCB → IEditCommand → UndoStack»;
/// реальные команды сцены появятся в фазе 2.
/// </summary>
public sealed class StubEditCommand(string description) : IEditCommand
{
    public string Description { get; } = description;

    public void Execute()
    {
    }

    public void Undo()
    {
    }
}
