using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Model;

/// <summary>
/// Make Unique (задача 3.4): разрыв наследования для одного инстанса —
/// он получает собственное определение с клоном мастер-контента; остальные
/// инстансы продолжают наследовать правки прежнего мастера. Вложенные инстансы
/// в клоне остаются общими (свои определения не клонируются).
/// </summary>
public sealed class MakeUniqueInstanceCommand : IEditCommand
{
    private readonly ComponentInstanceNode _instance;
    private ComponentInstanceNode? _unique;
    private SceneNode? _parent;
    private int _index;

    public MakeUniqueInstanceCommand(ComponentInstanceNode instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instance = instance;
    }

    public string Description => "Сделать компонент уникальным";

    /// <summary>Новый уникальный инстанс (после выполнения команды).</summary>
    public ComponentInstanceNode? UniqueInstance => _unique;

    public void Execute()
    {
        _parent = _instance.Parent
            ?? throw new InvalidOperationException("Уникальным можно сделать только инстанс, входящий в иерархию.");
        _index = _parent.IndexOfChild(_instance);

        if (_unique is null)
        {
            _unique = new ComponentInstanceNode
            {
                Definition = ComponentFactory.CloneDefinition(_instance.Definition),
                Name = _instance.Name,
                IsVisible = _instance.IsVisible,
                LocalTransform = _instance.LocalTransform,
                Tag = _instance.Tag,
            };
        }

        MoveChildren(_instance, _unique);
        _parent.RemoveChild(_instance);
        _parent.InsertChild(_index, _unique);
    }

    public void Undo()
    {
        if (_parent is null || _unique is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        MoveChildren(_unique, _instance);
        _parent.RemoveChild(_unique);
        _parent.InsertChild(_index, _instance);
    }

    private static void MoveChildren(SceneNode from, SceneNode to)
    {
        List<SceneNode> children = [.. from.Children];
        foreach (SceneNode child in children)
        {
            from.RemoveChild(child);
            to.AddChild(child);
        }
    }
}
