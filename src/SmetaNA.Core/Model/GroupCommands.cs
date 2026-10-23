using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Model;

/// <summary>
/// Группировка узлов (задача 3.3): выбранные узлы с общим родителем переносятся
/// в новую группу с единичной трансформацией — мировые позиции не меняются.
/// </summary>
public sealed class GroupNodesCommand : IEditCommand
{
    private readonly IReadOnlyList<SceneNode> _nodes;
    private readonly string _name;
    private readonly List<(SceneNode Node, int Index)> _positions = [];
    private GroupNode? _group;
    private SceneNode? _parent;

    /// <param name="nodes">Узлы с общим родителем; порядок сохраняется внутри группы.</param>
    /// <param name="name">Имя новой группы.</param>
    public GroupNodesCommand(IEnumerable<SceneNode> nodes, string name = "Группа")
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Имя группы не может быть пустым.", nameof(name));
        }

        var list = new List<SceneNode>();
        foreach (SceneNode node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (list.Contains(node))
            {
                throw new ArgumentException("Список узлов группировки содержит повторы.", nameof(nodes));
            }

            list.Add(node);
        }

        if (list.Count == 0)
        {
            throw new ArgumentException("Для группировки нужен хотя бы один узел.", nameof(nodes));
        }

        _nodes = list;
        _name = name;
    }

    public string Description => "Группировка";

    /// <summary>Созданная группа (после выполнения команды).</summary>
    public GroupNode? Group => _group;

    public void Execute()
    {
        _parent = _nodes[0].Parent
            ?? throw new InvalidOperationException("Группировать можно только узлы, входящие в иерархию сцены.");

        _positions.Clear();
        foreach (SceneNode node in _nodes)
        {
            if (!ReferenceEquals(node.Parent, _parent))
            {
                throw new InvalidOperationException("Группировать можно только узлы с общим родителем.");
            }

            _positions.Add((node, _parent.IndexOfChild(node)));
        }

        _group ??= new GroupNode { Name = _name };

        int insertIndex = _positions.Min(p => p.Index);
        _parent.InsertChild(insertIndex, _group);
        foreach (SceneNode node in _nodes)
        {
            _parent.RemoveChild(node);
            _group.AddChild(node);
        }
    }

    public void Undo()
    {
        if (_group is null || _parent is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        foreach (SceneNode node in _nodes)
        {
            _group.RemoveChild(node);
        }

        _parent.RemoveChild(_group);
        foreach ((SceneNode node, int index) in _positions.OrderBy(p => p.Index))
        {
            _parent.InsertChild(index, node);
        }
    }
}

/// <summary>
/// Расгруппировка (задача 3.3): дети группы поднимаются к её родителю
/// с сохранением порядка; сама группа остаётся пустой и восстанавливается при undo.
/// </summary>
public sealed class UngroupNodeCommand : IEditCommand
{
    private readonly GroupNode _group;
    private SceneNode? _parent;
    private int _index;
    private List<SceneNode> _children = [];

    public UngroupNodeCommand(GroupNode group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _group = group;
    }

    public string Description => "Расгруппировка";

    public void Execute()
    {
        _parent = _group.Parent
            ?? throw new InvalidOperationException("Расгруппировать можно только группу, входящую в иерархию.");
        _index = _parent.IndexOfChild(_group);
        _children = [.. _group.Children];

        _parent.RemoveChild(_group);
        for (int i = 0; i < _children.Count; i++)
        {
            _group.RemoveChild(_children[i]);
            _parent.InsertChild(_index + i, _children[i]);
        }
    }

    public void Undo()
    {
        if (_parent is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        foreach (SceneNode child in _children)
        {
            _parent.RemoveChild(child);
        }

        _parent.InsertChild(_index, _group);
        foreach (SceneNode child in _children)
        {
            _group.AddChild(child);
        }
    }
}
