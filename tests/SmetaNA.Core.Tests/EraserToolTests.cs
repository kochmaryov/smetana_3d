using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Инструмент Eraser: стирание граней щелчком и жестом, undo/redo.</summary>
public class EraserToolTests
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
    public void Tool_Click_ErasesFaceUnderCursor()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new EraserTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        Assert.Equal(5, mesh.Faces.Count);
        Assert.False(mesh.IsWatertight);
        Assert.True(undo.CanUndo);
        Assert.Empty(tool.Pending);
    }

    [Fact]
    public void Tool_Drag_ErasesAllFacesOfStroke()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new EraserTool();
        tool.Activate(context);

        Vector2d topPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        Vector2d frontPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5));
        tool.OnPointer(PointerInput.Press(topPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(frontPixel));
        Assert.Equal(2, tool.Pending.Count);
        tool.OnPointer(PointerInput.Release(frontPixel, PointerButton.Left));

        // верхняя и передняя грани стёрты одной командой
        Assert.Equal(4, mesh.Faces.Count);
        Assert.True(undo.CanUndo);

        undo.Undo();
        Assert.Equal(6, mesh.Faces.Count);
        Assert.True(mesh.IsWatertight);

        undo.Redo();
        Assert.Equal(4, mesh.Faces.Count);
    }

    [Fact]
    public void Tool_Undo_RestoresFaceIdentity()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new EraserTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        undo.Undo();
        Assert.Contains(top, mesh.Faces);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void Tool_Escape_CancelsStrokeWithoutErase()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new EraserTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.False(tool.IsInteracting);
        Assert.Empty(tool.Pending);
        Assert.False(undo.CanUndo);
        Assert.Equal(6, mesh.Faces.Count);
    }

    [Fact]
    public void Tool_VcbValue_NotApplied()
    {
        (ToolContext context, _) = Setup(out _);
        var tool = new EraserTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(5));
    }

    [Fact]
    public void Tool_Miss_DoesNothing()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new EraserTool();
        tool.Activate(context);

        // курсор мимо модели: жест не начинается
        Assert.False(tool.OnPointer(PointerInput.Press(new Vector2d(5, 5), PointerButton.Left)));
        Assert.False(undo.CanUndo);
        Assert.Equal(6, mesh.Faces.Count);
    }
}
