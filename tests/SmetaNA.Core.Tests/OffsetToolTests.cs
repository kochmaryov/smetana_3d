using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Materials;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент Offset: сценарии ToolInput (hover/перетаскивание/VCB), точный ввод,
/// undo/redo, наследование материала.
/// </summary>
public class OffsetToolTests
{
    private static (ToolContext Context, UndoStack Undo) Setup(out HalfEdgeMesh mesh)
    {
        mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        return (context, undo);
    }

    [Fact]
    public void Tool_HoverThenVcb_InsetsFaceAndSplitsIt()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.NotNull(context.Selection.Face);
        Assert.Equal(ToolValueKind.Length, tool.Vcb.Kind);
        Assert.True(tool.Vcb.IsActive);

        Assert.True(tool.TryApplyValue(2));

        Assert.Equal(10, mesh.Faces.Count);
        Assert.Equal(12, mesh.Vertices.Count);
        Assert.True(mesh.IsWatertight);
        Assert.True(undo.CanUndo);
        Assert.False(tool.IsInteracting);

        // внутренняя грань — квадрат 6×6 со смещением 2
        IReadOnlyList<MeshVertex> inner = mesh.GetRing(mesh.Faces[^1]);
        Assert.Equal(4, inner.Count);
        foreach (MeshVertex vertex in inner)
        {
            Assert.Contains(vertex.Position.X, new[] { 2.0, 8.0 });
            Assert.Contains(vertex.Position.Y, new[] { 2.0, 8.0 });
            Assert.Equal(10, vertex.Position.Z, 9);
        }
    }

    [Fact]
    public void Tool_NegativeVcb_OutsetsFace()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.True(tool.TryApplyValue(-2));

        IReadOnlyList<MeshVertex> inner = mesh.GetRing(mesh.Faces[^1]);
        foreach (MeshVertex vertex in inner)
        {
            Assert.Contains(vertex.Position.X, new[] { -2.0, 12.0 });
            Assert.Contains(vertex.Position.Y, new[] { -2.0, 12.0 });
        }
    }

    [Fact]
    public void Tool_DragThenVcb_CommitsExactDistance()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d anchorPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(4, 5, 10));

        Assert.True(tool.OnPointer(PointerInput.Press(anchorPixel, PointerButton.Left)));
        Assert.True(tool.IsInteracting);
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.Distance > 0);

        Assert.True(tool.TryApplyValue(3));
        Assert.False(tool.IsInteracting);
        Assert.Equal(12, mesh.Vertices.Count);

        IReadOnlyList<MeshVertex> inner = mesh.GetRing(mesh.Faces[^1]);
        Assert.True(inner.All(vertex => Math.Abs(vertex.Position.X - 3) < 1e-6 || Math.Abs(vertex.Position.X - 7) < 1e-6));
        Assert.True(inner.All(vertex => Math.Abs(vertex.Position.Y - 3) < 1e-6 || Math.Abs(vertex.Position.Y - 7) < 1e-6));
    }

    [Fact]
    public void Tool_CommitThenUndo_RestoresTopologyAndIdentity()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        (int Vertices, int Edges, int Faces) before = ToolsTestSupport.Topology(mesh);
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.True(tool.TryApplyValue(2));

        undo.Undo();
        Assert.Equal(before, ToolsTestSupport.Topology(mesh));
        Assert.True(mesh.IsWatertight);
        Assert.Contains(top, mesh.Faces);

        undo.Redo();
        Assert.Equal(12, mesh.Vertices.Count);
        Assert.Equal(10, mesh.Faces.Count);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void Tool_Escape_DoesNotChangeScene()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d anchorPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 5, 10));
        tool.OnPointer(PointerInput.Press(anchorPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(6, 5, 10))));

        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.False(tool.IsInteracting);
        Assert.False(undo.CanUndo);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Empty(tool.Preview.Lines);
    }

    [Fact]
    public void Tool_OffsetInheritsFaceMaterial()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        top.MaterialIndex = 2;
        var tool = new OffsetTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.True(tool.TryApplyValue(2));

        // материал исходной грани наследуют новые грани (кольцо + внутренняя)
        foreach (MeshFace face in mesh.Faces.Skip(5))
        {
            Assert.Equal(2, face.MaterialIndex);
        }

        Assert.Equal(-1, mesh.Faces[0].MaterialIndex);
        Assert.Equal(MaterialPalette.Orange, MaterialResolver.Resolve(mesh, mesh.Faces[^1]));
    }

    [Fact]
    public void Tool_WithoutFace_VcbValueNotApplied()
    {
        (ToolContext context, _) = Setup(out _);
        var tool = new OffsetTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(2));
    }
}
