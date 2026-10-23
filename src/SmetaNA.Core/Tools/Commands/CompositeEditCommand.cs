using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Составная команда истории: набор команд, выполняемых по порядку;
/// откат — в обратном порядке (атомарное изменение из нескольких шагов).
/// </summary>
public sealed class CompositeEditCommand : IEditCommand
{
    private readonly IReadOnlyList<IEditCommand> _commands;

    public CompositeEditCommand(string description, IReadOnlyList<IEditCommand> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(commands);
        Description = description;
        _commands = commands;
    }

    public string Description { get; }

    public IReadOnlyList<IEditCommand> Commands => _commands;

    public void Execute()
    {
        foreach (IEditCommand command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
