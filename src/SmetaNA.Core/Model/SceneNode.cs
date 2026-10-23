using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Model;

/// <summary>
/// Узел иерархии сцены с локальной трансформацией (модуль M5).
/// Дети упорядочены; трансформации в мире — фаза 1 (задача 1.1).
/// </summary>
public abstract class SceneNode
{
    private readonly List<SceneNode> _children = [];

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public Transform3d LocalTransform { get; set; } = Transform3d.Identity;

    /// <summary>Тег (слой) узла; null — без тега. Скрытый тег скрывает поддерево узла.</summary>
    public Tag? Tag { get; set; }

    public SceneNode? Parent { get; private set; }
    public IReadOnlyList<SceneNode> Children => _children;

    public void AddChild(SceneNode child) => InsertChild(_children.Count, child);

    /// <summary>Вставка потомка в указанную позицию списка детей (нужно командам undo).</summary>
    public void InsertChild(int index, SceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if ((uint)index > (uint)_children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Индекс позиции вне диапазона детей узла.");
        }

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("Узел не может быть собственным потомком.");
        }

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("Узел уже принадлежит иерархии; сначала удалите его из прежнего родителя.");
        }

        for (SceneNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
            {
                throw new InvalidOperationException("Нельзя сделать узел потомком его собственного поддерева.");
            }
        }

        child.Parent = this;
        _children.Insert(index, child);
    }

    /// <summary>Индекс потомка среди детей узла; -1, если узел не является потомком.</summary>
    public int IndexOfChild(SceneNode child) => _children.IndexOf(child);

    public void RemoveChild(SceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
        {
            throw new InvalidOperationException("Узел не является потомком этого родителя.");
        }

        child.Parent = null;
    }
}

/// <summary>Группа — композиция узлов без собственной геометрии.</summary>
public sealed class GroupNode : SceneNode
{
}

/// <summary>Объект с мешевой геометрией.</summary>
public sealed class MeshNode : SceneNode
{
    public required HalfEdgeMesh Mesh { get; init; }
}

/// <summary>
/// Инстанс компонента: геометрия живёт в определении, правка мастера
/// отражается на всех инстансах (сценарий S5; реализация наследования — задача 3.4).
/// </summary>
public sealed class ComponentInstanceNode : SceneNode
{
    public required ComponentDefinition Definition { get; init; }
}

/// <summary>Определение компонента (мастер-контент).</summary>
public sealed class ComponentDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public SceneNode Content { get; set; } = new GroupNode();
}
