using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Модель аннотаций: узлы размеров/меток в сцене, команды undo/redo, поиск.
/// </summary>
public class DimensionModelTests
{
    [Fact]
    public void DimensionNode_ValueMm_FollowsKind()
    {
        var node = new DimensionNode
        {
            Kind = DimensionKind.Linear,
            Start = new Vector3d(0, 0, 0),
            End = new Vector3d(30, 40, 0),
            MeasureDirection = Vector3d.UnitX,
        };

        Assert.Equal(30, node.ValueMm, 9);

        node.Kind = DimensionKind.Aligned;
        Assert.Equal(50, node.ValueMm, 9);
    }

    [Fact]
    public void AnnotationNode_WorldTransform_ComposesParents()
    {
        var group = new GroupNode { LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)) };
        var node = new DimensionNode { LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 5, 0)) };
        group.AddChild(node);
        new Scene().Root.AddChild(group);

        Vector3d world = node.WorldTransform().TransformPoint(new Vector3d(1, 2, 3));

        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(101, 7, 3), world));
    }

    [Fact]
    public void AnnotationQuery_FindsAnnotations_SkipsHidden()
    {
        var scene = new Scene();
        var dimension = new DimensionNode { Name = "D1" };
        var note = new TextNoteNode { Name = "N1" };
        var hidden = new DimensionNode { Name = "D2", IsVisible = false };
        scene.Root.AddChild(dimension);
        scene.Root.AddChild(note);
        scene.Root.AddChild(hidden);

        Assert.Equal(2, AnnotationQuery.FindInScene(scene).Count);
        Assert.Equal(2, AnnotationQuery.FindDimensions(scene).Count + AnnotationQuery.FindTextNotes(scene).Count);
        Assert.Equal(3, AnnotationQuery.FindInScene(scene, includeHidden: true).Count);
        Assert.Same(dimension, Assert.Single(AnnotationQuery.FindDimensions(scene)));
    }

    [Fact]
    public void AddAnnotationCommand_UndoRedo_MovesNodeInHierarchy()
    {
        var scene = new Scene();
        var undo = new UndoStack();
        var node = new DimensionNode { Start = Vector3d.Zero, End = new Vector3d(10, 0, 0) };

        undo.Push(new AddAnnotationCommand(scene.Root, node, "Размер 10 мм"));
        Assert.Same(node, Assert.Single(scene.Root.Children));
        Assert.Equal("Размер 10 мм", undo.NextUndoDescription);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
        Assert.Null(node.Parent);

        undo.Redo();
        Assert.Same(node, Assert.Single(scene.Root.Children));
    }

    [Fact]
    public void RemoveAnnotationCommand_Undo_RestoresPosition()
    {
        var scene = new Scene();
        var first = new TextNoteNode { Text = "a" };
        var second = new DimensionNode();
        var third = new TextNoteNode { Text = "b" };
        scene.Root.AddChild(first);
        scene.Root.AddChild(second);
        scene.Root.AddChild(third);

        var command = new RemoveAnnotationCommand(second);
        command.Execute();
        Assert.Equal(new SceneNode[] { first, third }, scene.Root.Children);

        command.Undo();
        Assert.Equal(new SceneNode[] { first, second, third }, scene.Root.Children);
    }

    [Fact]
    public void EditDimensionCommand_UndoRedo_RestoresGeometry()
    {
        var node = new DimensionNode
        {
            Kind = DimensionKind.Aligned,
            Start = new Vector3d(0, 0, 0),
            End = new Vector3d(100, 0, 0),
            LineOffset = new Vector3d(0, 10, 0),
        };
        var command = new EditDimensionCommand(node, new DimensionGeometry(
            DimensionKind.Linear,
            new Vector3d(0, 0, 0),
            new Vector3d(120, 30, 0),
            Vector3d.UnitX,
            new Vector3d(0, 25, 0),
            "120 mm"));

        command.Execute();
        Assert.Equal(DimensionKind.Linear, node.Kind);
        Assert.Equal(120, node.ValueMm, 9);
        Assert.Equal("120 mm", node.TextOverride);

        command.Undo();
        Assert.Equal(DimensionKind.Aligned, node.Kind);
        Assert.Equal(100, node.ValueMm, 9);
        Assert.Null(node.TextOverride);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(0, 10, 0), node.LineOffset));

        command.Execute();
        Assert.Equal(120, node.ValueMm, 9);
    }

    [Fact]
    public void EditTextNoteCommand_UndoRedo_RestoresText()
    {
        var node = new TextNoteNode { Anchor = Vector3d.Zero, LabelPoint = new Vector3d(10, 10, 0), Text = "было" };
        var command = new EditTextNoteCommand(node, new TextNoteGeometry(
            new Vector3d(1, 2, 3),
            new Vector3d(4, 5, 6),
            "стало"));

        command.Execute();
        Assert.Equal("стало", node.Text);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(1, 2, 3), node.Anchor));

        command.Undo();
        Assert.Equal("было", node.Text);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(10, 10, 0), node.LabelPoint));
    }

    [Fact]
    public void RemoveAnnotationCommand_ExecuteWithoutParent_Throws()
    {
        var command = new RemoveAnnotationCommand(new DimensionNode());
        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }
}
