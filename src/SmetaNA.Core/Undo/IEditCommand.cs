namespace SmetaNA.Core.Undo;

/// <summary>
/// Команда истории редактирования: каждое изменение сцены идёт через команду
/// (UX-принцип «дешёвый и полный undo/redo», спека §3.2).
/// </summary>
public interface IEditCommand
{
    string Description { get; }

    void Execute();

    void Undo();
}
