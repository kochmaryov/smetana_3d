using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Model;

/// <summary>Команда добавления узла в иерархию (в т.ч. в мастер-контент компонента).</summary>
public sealed class AddNodeCommand : IEditCommand
{
    private readonly SceneNode _parent;
    private readonly SceneNode _child;
    private readonly int? _index;

    /// <param name="parent">Родитель (для правок в контексте — <see cref="EditingContext.ActiveNode"/>).</param>
    /// <param name="child">Добавляемый узел без текущего родителя.</param>
    /// <param name="index">Позиция среди детей; null — в конец.</param>
    public AddNodeCommand(SceneNode parent, SceneNode child, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        _parent = parent;
        _child = child;
        _index = index;
    }

    public string Description => "Добавление узла";

    public void Execute()
    {
        if (_index is int index)
        {
            _parent.InsertChild(index, _child);
        }
        else
        {
            _parent.AddChild(_child);
        }
    }

    public void Undo() => _parent.RemoveChild(_child);
}

/// <summary>Команда удаления узла из иерархии с запоминанием позиции.</summary>
public sealed class RemoveNodeCommand : IEditCommand
{
    private readonly SceneNode _node;
    private SceneNode? _parent;
    private int _index;

    public RemoveNodeCommand(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
    }

    public string Description => "Удаление узла";

    public void Execute()
    {
        _parent = _node.Parent
            ?? throw new InvalidOperationException("Удалить из иерархии можно только узел, имеющий родителя.");
        _index = _parent.IndexOfChild(_node);
        _parent.RemoveChild(_node);
    }

    public void Undo()
    {
        if (_parent is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        _parent.InsertChild(_index, _node);
    }
}

/// <summary>Команда перемещения узла к другому родителю (в т.ч. между ветками контекста).</summary>
public sealed class MoveNodeCommand : IEditCommand
{
    private readonly SceneNode _node;
    private readonly SceneNode _newParent;
    private readonly int? _index;
    private SceneNode? _oldParent;
    private int _oldIndex;

    /// <param name="index">Позиция среди детей нового родителя; null — в конец.</param>
    public MoveNodeCommand(SceneNode node, SceneNode newParent, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(newParent);
        _node = node;
        _newParent = newParent;
        _index = index;
    }

    public string Description => "Перемещение узла";

    public void Execute()
    {
        _oldParent = _node.Parent
            ?? throw new InvalidOperationException("Перемещать можно только узел, входящий в иерархию.");
        _oldIndex = _oldParent.IndexOfChild(_node);
        _oldParent.RemoveChild(_node);
        try
        {
            if (_index is int index)
            {
                _newParent.InsertChild(index, _node);
            }
            else
            {
                _newParent.AddChild(_node);
            }
        }
        catch
        {
            _oldParent.InsertChild(_oldIndex, _node);
            throw;
        }
    }

    public void Undo()
    {
        if (_oldParent is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        _newParent.RemoveChild(_node);
        _oldParent.InsertChild(_oldIndex, _node);
    }
}

/// <summary>Команда изменения локальной трансформации узла.</summary>
public sealed class SetNodeTransformCommand : IEditCommand
{
    private readonly SceneNode _node;
    private readonly Transform3d _newTransform;
    private Transform3d _oldTransform;

    public SetNodeTransformCommand(SceneNode node, Transform3d newTransform)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _newTransform = newTransform;
    }

    public string Description => "Трансформация узла";

    public void Execute()
    {
        _oldTransform = _node.LocalTransform;
        _node.LocalTransform = _newTransform;
    }

    public void Undo() => _node.LocalTransform = _oldTransform;
}

/// <summary>Команда переименования узла (для панели Outliner).</summary>
public sealed class RenameNodeCommand : IEditCommand
{
    private readonly SceneNode _node;
    private readonly string _newName;
    private string _oldName = string.Empty;

    public RenameNodeCommand(SceneNode node, string newName)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(newName);
        _node = node;
        _newName = newName;
    }

    public string Description => "Переименование узла";

    public void Execute()
    {
        _oldName = _node.Name;
        _node.Name = _newName;
    }

    public void Undo() => _node.Name = _oldName;
}

/// <summary>Команда включения/выключения собственной видимости узла.</summary>
public sealed class SetNodeVisibilityCommand : IEditCommand
{
    private readonly SceneNode _node;
    private readonly bool _isVisible;
    private bool _oldIsVisible;

    public SetNodeVisibilityCommand(SceneNode node, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _isVisible = isVisible;
    }

    public string Description => "Видимость узла";

    public void Execute()
    {
        _oldIsVisible = _node.IsVisible;
        _node.IsVisible = _isVisible;
    }

    public void Undo() => _node.IsVisible = _oldIsVisible;
}
