using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Tools.M6;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Массивы (задача 5.1): линейный и круговой через ToolInput, точный ввод VCB
/// (шаг/угол и количество), одна отмена на весь массив, точная геометрия копий,
/// копии vs инстансы компонентов.
/// </summary>
public class M6ArrayToolsTests
{
    private static (ToolContext Context, UndoStack Undo) Setup(out HalfEdgeMesh mesh, out MeshNode node)
    {
        mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        return (context, undo);
    }

    [Fact]
    public void LinearArray_VcbSpacingAndCount_CreatesExactCopies()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        tool.OnPointer(PointerInput.Move(dragPixel));

        // точный шаг VCB (мм) — фиксирует шаг и переходит к количеству
        Assert.True(tool.TryApplyValue(25));
        Assert.Equal(ToolValueKind.Count, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(3));

        Assert.Equal(4, context.Scene.Root.Children.Count);
        BoundingBox3d original = M6Transforms.WorldBounds(node);
        for (int k = 1; k <= 3; k++)
        {
            MeshNode copy = (MeshNode)context.Scene.Root.Children[k];
            Assert.NotSame(node.Mesh, copy.Mesh);
            BoundingBox3d bounds = M6Transforms.WorldBounds(copy);
            Assert.True(ToolsTestSupport.NearlyEqual(
                original.Min + new Vector3d(25 * k, 0, 0),
                bounds.Min,
                1e-6));
            Assert.Equal(1000, ToolsTestSupport.SignedVolume(copy.Mesh), 6);
        }

        // одна отмена — весь массив
        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.Single(context.Scene.Root.Children);
        undo.Redo();
        Assert.Equal(4, context.Scene.Root.Children.Count);
    }

    [Fact]
    public void LinearArray_VcbSpacing_ReportsLengthKind()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out _);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10))));

        Assert.Equal(ToolValueKind.Length, tool.Vcb.Kind);
        Assert.Equal(3, tool.Vcb.Value!.Value, 6);
        Assert.True(tool.TryApplyValue(25));
        Assert.Equal(25, tool.Step.Length(), 6);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void LinearArray_MouseOnly_FlowCommitsDefaultCount()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out _);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));
        Assert.Equal(ToolValueKind.Count, tool.Vcb.Kind);

        // клик фиксирует массив с количеством по умолчанию; парный release глотается
        Assert.True(tool.OnPointer(PointerInput.Press(dragPixel, PointerButton.Left)));
        Assert.Equal(4, context.Scene.Root.Children.Count);
        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));
        Assert.Equal(4, context.Scene.Root.Children.Count);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void LinearArray_NestedParent_ComposesWorldTransformExactly()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        var scene = new Scene();
        var group = new GroupNode
        {
            Name = "Group",
            // мир узла = локальная ∘ мир родителя: поворот группы вокруг Z на 90°
            LocalTransform = EditingTransforms.CreateRotation(Vector3d.UnitZ, Vector3d.Zero, Math.PI / 2),
        };
        scene.Root.AddChild(group);
        var node = new MeshNode { Mesh = box, Name = "Mesh" };
        group.AddChild(node);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));

        var tool = new LinearArrayTool();
        tool.Activate(context);

        // повёрнутый бокс занимает x∈[-10;0], y∈[0;10], z∈[0;10]
        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(-2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.TryApplyValue(25));
        Assert.True(tool.TryApplyValue(1));

        Assert.Equal(2, group.Children.Count);
        BoundingBox3d original = M6Transforms.WorldBounds(node);
        BoundingBox3d copy = M6Transforms.WorldBounds((MeshNode)group.Children[1]);
        Assert.Equal(original.Min.X + 25, copy.Min.X, 6);
        Assert.Equal(original.Min.Y, copy.Min.Y, 6);
        Assert.Equal(original.Min.Z, copy.Min.Z, 6);
        Assert.Equal(original.Max.X + 25, copy.Max.X, 6);
        Assert.Equal(original.Max.Y, copy.Max.Y, 6);
        Assert.Equal(original.Max.Z, copy.Max.Z, 6);
    }

    [Fact]
    public void LinearArray_ZeroStep_DoesNotCommit()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out _);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(pressPixel));
        tool.OnPointer(PointerInput.Release(pressPixel, PointerButton.Left));

        // направление не задано — точный ввод не применим, копий нет
        Assert.False(tool.TryApplyValue(25));
        Assert.Single(context.Scene.Root.Children);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void LinearArray_CountRoundingAndEscape()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out _);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(25);
        Assert.False(tool.TryApplyValue(0.4));
        Assert.True(tool.TryApplyValue(2.4));
        Assert.Equal(3, context.Scene.Root.Children.Count);

        // Escape до фиксации не меняет сцену
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(25);
        tool.OnKey(new KeyInput(ToolKey.Escape));
        Assert.Equal(3, context.Scene.Root.Children.Count);
    }

    [Fact]
    public void PolarArray_VcbAngleAndCount_RotatesCopiesAroundFaceNormal()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(20, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(10, -40, 25), new Vector3d(10, 5, 10));

        var tool = new PolarArrayTool();
        tool.Activate(context);

        // центр верхней грани — ось массива (нормаль +Z), опорное направление +Y
        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(10, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(10, 8, 10))));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(13, 5, 10))));

        Assert.Equal(ToolValueKind.Angle, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(90));
        Assert.Equal(ToolValueKind.Count, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(2));

        Assert.Equal(3, scene.Root.Children.Count);

        // +90° вокруг вертикали через (10;5): (x,y) → (15−y; x−5)
        BoundingBox3d first = M6Transforms.WorldBounds((MeshNode)scene.Root.Children[1]);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(5, -5, 0), first.Min, 1e-9));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(15, 15, 10), first.Max, 1e-9));
        Assert.Equal(2000, ToolsTestSupport.SignedVolume(((MeshNode)scene.Root.Children[1]).Mesh), 6);

        // +180°: бокс 20×10 возвращается в те же габариты
        BoundingBox3d second = M6Transforms.WorldBounds((MeshNode)scene.Root.Children[2]);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(0, 0, 0), second.Min, 1e-9));
        Assert.Equal(2000, ToolsTestSupport.SignedVolume(((MeshNode)scene.Root.Children[2]).Mesh), 6);

        // одна отмена — весь массив
        undo.Undo();
        Assert.Single(scene.Root.Children);
        undo.Redo();
        Assert.Equal(3, scene.Root.Children.Count);
    }

    [Fact]
    public void PolarArray_InstanceSubject_CopiesShareDefinition()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode content) = ToolsTestSupport.SceneWith(box, "Content");
        scene.Root.RemoveChild(content);
        var definition = new ComponentDefinition { Name = "Component", Content = content };
        var instance = new ComponentInstanceNode { Definition = definition, Name = "Instance" };
        scene.Root.AddChild(instance);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));

        var tool = new PolarArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 8, 10))));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(8, 5, 10))));
        tool.TryApplyValue(90);
        tool.TryApplyValue(1);

        Assert.Equal(2, scene.Root.Children.Count);
        var copy = Assert.IsType<ComponentInstanceNode>(scene.Root.Children[1]);
        Assert.Same(definition, copy.Definition);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void LinearArray_PlainMesh_CopyHasIndependentGeometry()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out MeshNode node);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(25);
        tool.TryApplyValue(1);

        var copy = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);
        Assert.NotSame(mesh, copy.Mesh);
        Assert.Equal(mesh.Vertices.Count, copy.Mesh.Vertices.Count);
        Assert.Equal(mesh.Faces.Count, copy.Mesh.Faces.Count);
    }

    [Fact]
    public void LinearArray_SelectionSubject_ArraysSelectedNode()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);
        var tool = new LinearArrayTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(25);
        tool.TryApplyValue(2);

        Assert.Equal(3, context.Scene.Root.Children.Count);
        Assert.True(undo.CanUndo);
    }
}
