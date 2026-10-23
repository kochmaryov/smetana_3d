using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Model;

/// <summary>Команда создания тега (слоя) в таблице тегов проекта.</summary>
public sealed class CreateTagCommand : IEditCommand
{
    private readonly TagTable _table;
    private readonly string _name;
    private Tag? _tag;

    public CreateTagCommand(TagTable table, string name)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(name);
        _table = table;
        _name = name;
    }

    public string Description => "Создание тега";

    /// <summary>Созданный тег (после выполнения команды).</summary>
    public Tag? Tag => _tag;

    public void Execute()
    {
        _tag ??= new Tag { Name = _name };
        _table.Add(_tag);
    }

    public void Undo()
    {
        if (_tag is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        _table.Remove(_tag);
    }
}

/// <summary>
/// Команда удаления тега: привязки узлов снимаются (включая узлы мастер-контента),
/// при undo тег и привязки восстанавливаются.
/// </summary>
public sealed class DeleteTagCommand : IEditCommand
{
    private readonly Scene _scene;
    private readonly Tag _tag;
    private List<SceneNode> _bound = [];

    public DeleteTagCommand(Scene scene, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(tag);
        _scene = scene;
        _tag = tag;
    }

    public string Description => "Удаление тега";

    public void Execute()
    {
        var bound = new HashSet<SceneNode>();
        foreach (OutlinerEntry entry in OutlinerQuery.Walk(_scene.Root))
        {
            if (ReferenceEquals(entry.Node.Tag, _tag))
            {
                bound.Add(entry.Node);
            }
        }

        _bound = [.. bound];
        foreach (SceneNode node in _bound)
        {
            node.Tag = null;
        }

        _scene.Tags.Remove(_tag);
    }

    public void Undo()
    {
        _scene.Tags.Add(_tag);
        foreach (SceneNode node in _bound)
        {
            node.Tag = _tag;
        }
    }
}

/// <summary>Команда переименования тега с проверкой уникальности имени.</summary>
public sealed class RenameTagCommand : IEditCommand
{
    private readonly TagTable _table;
    private readonly Tag _tag;
    private readonly string _newName;
    private string _oldName = string.Empty;

    public RenameTagCommand(TagTable table, Tag tag, string newName)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(newName);
        _table = table;
        _tag = tag;
        _newName = newName;
    }

    public string Description => "Переименование тега";

    public void Execute()
    {
        _oldName = _tag.Name;
        _table.Rename(_tag, _newName);
    }

    public void Undo() => _table.Rename(_tag, _oldName);
}

/// <summary>Команда включения/выключения видимости тега (скрывает поддеревья узлов).</summary>
public sealed class SetTagVisibilityCommand : IEditCommand
{
    private readonly Tag _tag;
    private readonly bool _isVisible;
    private bool _oldIsVisible;

    public SetTagVisibilityCommand(Tag tag, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _tag = tag;
        _isVisible = isVisible;
    }

    public string Description => "Видимость тега";

    public void Execute()
    {
        _oldIsVisible = _tag.IsVisible;
        _tag.IsVisible = _isVisible;
    }

    public void Undo() => _tag.IsVisible = _oldIsVisible;
}

/// <summary>Команда привязки узла к тегу; null — снять тег.</summary>
public sealed class AssignTagCommand : IEditCommand
{
    private readonly SceneNode _node;
    private readonly Tag? _tag;
    private Tag? _oldTag;

    public AssignTagCommand(SceneNode node, Tag? tag)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _tag = tag;
    }

    public string Description => "Привязка тега";

    public void Execute()
    {
        _oldTag = _node.Tag;
        _node.Tag = _tag;
    }

    public void Undo() => _node.Tag = _oldTag;
}
