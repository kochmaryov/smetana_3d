using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Annotations;

/// <summary>Полная геометрия размера (новое состояние для команды правки).</summary>
public sealed record DimensionGeometry(
    DimensionKind Kind,
    Vector3d Start,
    Vector3d End,
    Vector3d? MeasureDirection,
    Vector3d LineOffset,
    string? TextOverride);

/// <summary>Полная геометрия текстовой метки.</summary>
public sealed record TextNoteGeometry(Vector3d Anchor, Vector3d LabelPoint, string Text);

/// <summary>Команда истории: добавление аннотации (размера/метки) в иерархию сцены.</summary>
public sealed class AddAnnotationCommand : IEditCommand
{
    private readonly SceneNode _parent;
    private readonly AnnotationNode _node;

    public AddAnnotationCommand(SceneNode parent, AnnotationNode node, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(node);
        _parent = parent;
        _node = node;
        Description = description ?? (node switch
        {
            DimensionNode => "Добавление размера",
            TextNoteNode => "Добавление метки",
            _ => "Добавление аннотации",
        });
    }

    public string Description { get; }

    public void Execute() => _parent.AddChild(_node);

    public void Undo() => _parent.RemoveChild(_node);
}

/// <summary>Команда истории: удаление аннотации с запоминанием позиции.</summary>
public sealed class RemoveAnnotationCommand : IEditCommand
{
    private readonly AnnotationNode _node;
    private SceneNode? _parent;
    private int _index;

    public RemoveAnnotationCommand(AnnotationNode node, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        Description = description ?? (node switch
        {
            DimensionNode => "Удаление размера",
            TextNoteNode => "Удаление метки",
            _ => "Удаление аннотации",
        });
    }

    public string Description { get; }

    public void Execute()
    {
        _parent = _node.Parent
            ?? throw new InvalidOperationException("Удалить можно только аннотацию, входящую в иерархию сцены.");
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

/// <summary>Команда истории: правка геометрии размера (откат к прежнему состоянию).</summary>
public sealed class EditDimensionCommand : IEditCommand
{
    private readonly DimensionNode _node;
    private readonly DimensionGeometry _new;
    private DimensionGeometry? _old;

    public EditDimensionCommand(DimensionNode node, DimensionGeometry newGeometry, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(newGeometry);
        _node = node;
        _new = newGeometry;
        Description = description ?? "Изменение размера";
    }

    public string Description { get; }

    public void Execute()
    {
        _old ??= Capture();
        Apply(_new);
    }

    public void Undo()
    {
        if (_old is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        Apply(_old);
    }

    private DimensionGeometry Capture() => new(
        _node.Kind,
        _node.Start,
        _node.End,
        _node.MeasureDirection,
        _node.LineOffset,
        _node.TextOverride);

    private void Apply(DimensionGeometry geometry)
    {
        _node.Kind = geometry.Kind;
        _node.Start = geometry.Start;
        _node.End = geometry.End;
        _node.MeasureDirection = geometry.MeasureDirection;
        _node.LineOffset = geometry.LineOffset;
        _node.TextOverride = geometry.TextOverride;
    }
}

/// <summary>Команда истории: правка текстовой метки (откат к прежнему состоянию).</summary>
public sealed class EditTextNoteCommand : IEditCommand
{
    private readonly TextNoteNode _node;
    private readonly TextNoteGeometry _new;
    private TextNoteGeometry? _old;

    public EditTextNoteCommand(TextNoteNode node, TextNoteGeometry newGeometry, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(newGeometry);
        _node = node;
        _new = newGeometry;
        Description = description ?? "Изменение метки";
    }

    public string Description { get; }

    public void Execute()
    {
        _old ??= new TextNoteGeometry(_node.Anchor, _node.LabelPoint, _node.Text);
        Apply(_new);
    }

    public void Undo()
    {
        if (_old is null)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        Apply(_old);
    }

    private void Apply(TextNoteGeometry geometry)
    {
        _node.Anchor = geometry.Anchor;
        _node.LabelPoint = geometry.LabelPoint;
        _node.Text = geometry.Text;
    }
}
