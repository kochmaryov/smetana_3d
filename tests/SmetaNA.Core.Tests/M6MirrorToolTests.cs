using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.M6;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Зеркало/симметрия (задача 5.1): плоскости «ось», «2 точки», «грань как
/// плоскость», опция «копия» (Ctrl), режим «на место», undo/redo одной командой,
/// сохранение объёма и наружных нормалей, зеркало инстанса компонента.
/// </summary>
public class M6MirrorToolTests
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
    public void MirrorByFace_Copy_AddsMirroredNodeWithOutwardNormals()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out MeshNode node);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(-40, 5, 25), new Vector3d(5, 5, 10));

        var tool = new MirrorTool(MirrorPlaneMode.Face);
        tool.Activate(context);

        // клик по грани x=0 с Ctrl — режим копии; плоскость грани — плоскость симметрии
        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 5, 5));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left, ToolModifiers.Ctrl)));

        Assert.Equal(2, context.Scene.Root.Children.Count);
        var copy = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);
        Assert.NotSame(node, copy);

        BoundingBox3d bounds = M6Transforms.WorldBounds(copy);
        Assert.Equal(-10, bounds.Min.X, 6);
        Assert.Equal(0, bounds.Max.X, 6);
        Assert.Equal(0, bounds.Min.Y, 6);
        Assert.Equal(10, bounds.Max.Y, 6);

        // объём и наружные нормали сохранены (кольца реверсированы)
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(copy.Mesh), 6);
        Assert.Equal(ToolsTestSupport.Topology(mesh), ToolsTestSupport.Topology(copy.Mesh));
        Assert.True(copy.Mesh.IsWatertight);

        // одна отмена — вся операция
        undo.Undo();
        Assert.Single(context.Scene.Root.Children);
        undo.Redo();
        Assert.Equal(2, context.Scene.Root.Children.Count);
    }

    [Fact]
    public void MirrorByFace_Move_ReplacesNodeAndUndoRestoresOriginal()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(-40, 5, 25), new Vector3d(5, 5, 10));

        var tool = new MirrorTool(MirrorPlaneMode.Face);
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 5, 5));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));

        // «на место»: объект заменён зеркальным, узел один
        Assert.Single(context.Scene.Root.Children);
        SceneNode mirrored = context.Scene.Root.Children[0];
        Assert.NotSame(node, mirrored);
        BoundingBox3d bounds = M6Transforms.WorldBounds(mirrored);
        Assert.Equal(-10, bounds.Min.X, 6);
        Assert.Equal(0, bounds.Max.X, 6);
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(((MeshNode)mirrored).Mesh), 6);

        undo.Undo();
        Assert.Same(node, context.Scene.Root.Children[0]);
        Assert.Equal(0, M6Transforms.WorldBounds(node).Min.X, 6);
        Assert.Equal(10, M6Transforms.WorldBounds(node).Max.X, 6);
    }

    [Fact]
    public void MirrorByAxis_UsesDominantAxisThroughClickPoint()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);

        var tool = new MirrorTool(MirrorPlaneMode.Axis);
        tool.Activate(context);

        // клик по верхней грани: главная ось нормали — Z, плоскость z=10 через точку клика
        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));

        Assert.Single(context.Scene.Root.Children);
        SceneNode mirrored = context.Scene.Root.Children[0];
        Assert.NotSame(node, mirrored);
        BoundingBox3d bounds = M6Transforms.WorldBounds(mirrored);
        Assert.Equal(10, bounds.Min.Z, 6);
        Assert.Equal(20, bounds.Max.Z, 6);
        Assert.Equal(0, bounds.Min.X, 6);
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(((MeshNode)mirrored).Mesh), 6);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void MirrorByTwoPoints_VerticalPlaneThroughPoints()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);

        var tool = new MirrorTool(MirrorPlaneMode.TwoPoints);
        tool.Activate(context);

        // две точки на «земле» задают вертикальную плоскость x=20; Ctrl — копия
        Vector2d first = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(20, 0, 0));
        Vector2d second = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(20, 10, 0));
        Assert.True(tool.OnPointer(PointerInput.Press(first, PointerButton.Left)));
        Assert.True(tool.IsInteracting);
        Assert.True(tool.OnPointer(PointerInput.Press(second, PointerButton.Left, ToolModifiers.Ctrl)));

        Assert.Equal(2, context.Scene.Root.Children.Count);
        BoundingBox3d copy = M6Transforms.WorldBounds((MeshNode)context.Scene.Root.Children[1]);
        Assert.Equal(30, copy.Min.X, 6);
        Assert.Equal(40, copy.Max.X, 6);
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(((MeshNode)context.Scene.Root.Children[1]).Mesh), 6);

        // оригинал на месте
        Assert.Equal(0, M6Transforms.WorldBounds(node).Min.X, 6);
        undo.Undo();
        Assert.Single(context.Scene.Root.Children);
    }

    [Fact]
    public void MirrorByTwoPoints_EscapeCancelsWithoutChanges()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);

        var tool = new MirrorTool(MirrorPlaneMode.TwoPoints);
        tool.Activate(context);

        Vector2d first = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(20, 0, 0));
        tool.OnPointer(PointerInput.Press(first, PointerButton.Left));
        tool.OnKey(new KeyInput(ToolKey.Escape));
        Assert.False(tool.IsInteracting);
        Assert.Single(context.Scene.Root.Children);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Mirror_Instance_CopyGetsMirroredDefinition()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode content) = ToolsTestSupport.SceneWith(box, "Content");
        scene.Root.RemoveChild(content);
        var definition = new ComponentDefinition { Name = "Component", Content = content };
        var instance = new ComponentInstanceNode { Definition = definition, Name = "Instance" };
        scene.Root.AddChild(instance);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(-40, 5, 25), new Vector3d(5, 5, 10));

        var tool = new MirrorTool(MirrorPlaneMode.Face);
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 5, 5));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left, ToolModifiers.Ctrl)));

        var copy = Assert.IsType<ComponentInstanceNode>(scene.Root.Children[1]);
        Assert.NotSame(definition, copy.Definition);
        var mirroredContent = Assert.IsType<MeshNode>(copy.Definition.Content);
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(mirroredContent.Mesh), 6);
        Assert.Equal(ToolsTestSupport.Topology(box), ToolsTestSupport.Topology(mirroredContent.Mesh));
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Mirror_WireEdges_PreservedInCopy()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshVertex wireA = mesh.AddVertex(new Vector3d(0, 0, 20));
        MeshVertex wireB = mesh.AddVertex(new Vector3d(10, 0, 20));
        mesh.AddWireEdge(wireA, wireB);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(-40, 5, 25), new Vector3d(5, 5, 10));

        var tool = new MirrorTool(MirrorPlaneMode.Face);
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 5, 5));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left, ToolModifiers.Ctrl));

        var copy = Assert.IsType<MeshNode>(scene.Root.Children[1]);
        Assert.Equal(mesh.Vertices.Count, copy.Mesh.Vertices.Count);
        Assert.Equal(mesh.HalfEdges.Count, copy.Mesh.HalfEdges.Count);
    }
}
