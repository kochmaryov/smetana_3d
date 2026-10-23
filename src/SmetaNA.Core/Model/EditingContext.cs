namespace SmetaNA.Core.Model;

/// <summary>Вид открытого контекста редактирования.</summary>
public enum EditContextKind
{
    /// <summary>Корневой контекст: редактируется сцена целиком.</summary>
    Root,

    /// <summary>Контекст группы: редактируется содержимое группы.</summary>
    Group,

    /// <summary>Контекст компонента: редактируется мастер-контент определения.</summary>
    Component,
}

/// <summary>
/// Кадр стека контекстов: что открыто и куда идут правки.
/// Для компонента <see cref="Target"/> — инстанс, а <see cref="ActiveNode"/> —
/// общий мастер-контент определения (правки отражаются на всех инстансах).
/// </summary>
public sealed record EditContextFrame(
    EditContextKind Kind,
    SceneNode Target,
    SceneNode ActiveNode,
    ComponentInstanceNode? Instance = null);

/// <summary>
/// Изоляция контекста редактирования (задачи 3.3–3.4): стек открытых контекстов
/// групп и компонентов. Правки через команды применяются к <see cref="ActiveNode"/>;
/// открыть можно только узел внутри текущего контекста, циклические ссылки
/// определений компонентов запрещены.
/// </summary>
public sealed class EditingContext
{
    private readonly List<EditContextFrame> _stack = [];

    public EditingContext(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scene = scene;
        _stack.Add(new EditContextFrame(EditContextKind.Root, scene.Root, scene.Root));
    }

    /// <summary>Срабатывает при открытии и закрытии контекстов — для обновления UI.</summary>
    public event Action? Changed;

    public Scene Scene { get; }

    /// <summary>Текущий (самый глубокий) открытый контекст.</summary>
    public EditContextFrame Current => _stack[^1];

    /// <summary>Узел, в который идут правки: корень, группа или мастер-контент компонента.</summary>
    public SceneNode ActiveNode => Current.ActiveNode;

    /// <summary>Открыт ли контекст группы или компонента (не корневой).</summary>
    public bool IsEditing => _stack.Count > 1;

    /// <summary>Глубина вложенности открытых контекстов (0 — корневой контекст).</summary>
    public int Depth => _stack.Count - 1;

    /// <summary>Все кадры стека снизу вверх: корень → текущий контекст.</summary>
    public IReadOnlyList<EditContextFrame> Frames => _stack.AsReadOnly();

    /// <summary>Можно ли открыть узел в текущем контексте.</summary>
    public bool CanOpen(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node switch
        {
            GroupNode group => IsInScope(group) && !IsGroupOpen(group),
            ComponentInstanceNode instance => IsInScope(instance) && !IsDefinitionOpen(instance.Definition),
            _ => false,
        };
    }

    /// <summary>
    /// Находится ли узел в зоне правки текущего контекста: сам <see cref="ActiveNode"/>,
    /// его поддерево или содержимое инстансов компонентов внутри него.
    /// </summary>
    public bool IsInScope(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ContainsNode(ActiveNode, node, []);
    }

    /// <summary>Открытие группы или инстанса компонента (по типу узла).</summary>
    public void Open(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node)
        {
            case GroupNode group:
                OpenGroup(group);
                break;
            case ComponentInstanceNode instance:
                OpenComponent(instance);
                break;
            default:
                throw new InvalidOperationException("Контекст редактирования открывают только группа или инстанс компонента.");
        }
    }

    /// <summary>Открытие группы: правки идут в саму группу.</summary>
    public void OpenGroup(GroupNode group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!IsInScope(group))
        {
            throw new InvalidOperationException("Группа вне зоны правки текущего контекста редактирования.");
        }

        if (IsGroupOpen(group))
        {
            throw new InvalidOperationException("Контекст этой группы уже открыт.");
        }

        _stack.Add(new EditContextFrame(EditContextKind.Group, group, group));
        Changed?.Invoke();
    }

    /// <summary>
    /// Открытие инстанса компонента: правки идут в мастер-контент определения
    /// и отражаются на всех инстансах (сценарий S5).
    /// </summary>
    public void OpenComponent(ComponentInstanceNode instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!IsInScope(instance))
        {
            throw new InvalidOperationException("Инстанс компонента вне зоны правки текущего контекста редактирования.");
        }

        if (IsDefinitionOpen(instance.Definition))
        {
            throw new InvalidOperationException($"Определение компонента «{instance.Definition.Name}» уже открыто: циклическая ссылка.");
        }

        _stack.Add(new EditContextFrame(
            EditContextKind.Component,
            instance,
            instance.Definition.Content,
            instance));
        Changed?.Invoke();
    }

    /// <summary>Закрытие текущего контекста; возвращает закрытый кадр.</summary>
    public EditContextFrame Close()
    {
        if (_stack.Count <= 1)
        {
            throw new InvalidOperationException("Корневой контекст закрыть нельзя.");
        }

        EditContextFrame closed = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        Changed?.Invoke();
        return closed;
    }

    /// <summary>Закрытие всех контекстов до корневого.</summary>
    public void CloseAll()
    {
        if (_stack.Count == 1)
        {
            return;
        }

        _stack.RemoveRange(1, _stack.Count - 1);
        Changed?.Invoke();
    }

    private bool IsGroupOpen(GroupNode group) =>
        _stack.Any(frame => frame.Kind == EditContextKind.Group && ReferenceEquals(frame.Target, group));

    private bool IsDefinitionOpen(ComponentDefinition definition) =>
        _stack.Any(frame => frame.Kind == EditContextKind.Component
            && frame.Instance is not null
            && ReferenceEquals(frame.Instance.Definition, definition));

    /// <summary>Поиск target в поддереве current с разворачиванием контента инстансов; защита от циклов.</summary>
    private static bool ContainsNode(SceneNode current, SceneNode target, HashSet<Guid> openDefinitions)
    {
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        if (current is ComponentInstanceNode instance && openDefinitions.Add(instance.Definition.Id))
        {
            bool found = ContainsNode(instance.Definition.Content, target, openDefinitions);
            openDefinitions.Remove(instance.Definition.Id);
            if (found)
            {
                return true;
            }
        }

        foreach (SceneNode child in current.Children)
        {
            if (ContainsNode(child, target, openDefinitions))
            {
                return true;
            }
        }

        return false;
    }
}
