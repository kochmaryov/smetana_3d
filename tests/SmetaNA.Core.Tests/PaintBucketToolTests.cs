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
/// Инструмент Paint Bucket: назначение и смена материала граней, материал объекта
/// (Ctrl), пипетка (Alt), выбор материала по индексу VCB, undo/redo.
/// </summary>
public class PaintBucketToolTests
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
    public void Tool_Click_AssignsCurrentMaterialToFace()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new PaintBucketTool();
        tool.CurrentMaterial = MaterialPalette.Red;
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        Assert.Equal(mesh.Materials.IndexOf(MaterialPalette.Red), top.MaterialIndex);
        Assert.Equal(MaterialPalette.Red, MaterialResolver.Resolve(mesh, top));
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Tool_ClickAgain_ChangesMaterial()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new PaintBucketTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.CurrentMaterial = MaterialPalette.Red;
        Click(tool, pixel);
        tool.CurrentMaterial = MaterialPalette.Blue;
        Click(tool, pixel);

        Assert.Equal(mesh.Materials.IndexOf(MaterialPalette.Blue), top.MaterialIndex);

        undo.Undo();
        Assert.Equal(mesh.Materials.IndexOf(MaterialPalette.Red), top.MaterialIndex);

        undo.Undo();
        Assert.Equal(-1, top.MaterialIndex);
    }

    [Fact]
    public void Tool_Vcb_SelectsMaterialByPaletteIndex()
    {
        (ToolContext context, _) = Setup(out _);
        var tool = new PaintBucketTool();
        tool.Activate(context);

        Assert.Equal(ToolValueKind.Coordinate, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(6));
        Assert.Equal(MaterialPalette.Blue, tool.CurrentMaterial);

        Assert.False(tool.TryApplyValue(999));
        Assert.Equal(MaterialPalette.Blue, tool.CurrentMaterial);
    }

    [Fact]
    public void Tool_Stroke_PaintsAllFacesOfGesture()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        MeshFace front = ToolsTestSupport.FaceWithNormal(mesh, -Vector3d.UnitY);
        var tool = new PaintBucketTool();
        tool.CurrentMaterial = MaterialPalette.Green;
        tool.Activate(context);

        Vector2d topPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        Vector2d frontPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5));
        tool.OnPointer(PointerInput.Press(topPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(frontPixel));
        tool.OnPointer(PointerInput.Release(frontPixel, PointerButton.Left));

        int green = mesh.Materials.IndexOf(MaterialPalette.Green);
        Assert.Equal(green, top.MaterialIndex);
        Assert.Equal(green, front.MaterialIndex);

        // одна команда на весь жест
        undo.Undo();
        Assert.Equal(-1, top.MaterialIndex);
        Assert.Equal(-1, front.MaterialIndex);
    }

    [Fact]
    public void Tool_ObjectPaint_SetsMeshMaterialAndClearsFaceOverrides()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        top.MaterialIndex = 5;
        var tool = new PaintBucketTool();
        tool.CurrentMaterial = MaterialPalette.Green;
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left, ToolModifiers.Ctrl));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        Assert.Equal(mesh.Materials.IndexOf(MaterialPalette.Green), mesh.DefaultMaterialIndex);
        Assert.Equal(-1, top.MaterialIndex);
        Assert.Equal(MaterialPalette.Green, MaterialResolver.Resolve(mesh, top));

        undo.Undo();
        Assert.Equal(-1, mesh.DefaultMaterialIndex);
        Assert.Equal(5, top.MaterialIndex);
    }

    [Fact]
    public void Tool_Alt_SamplesMaterialFromFace()
    {
        (ToolContext context, _) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        top.MaterialIndex = mesh.Materials.IndexOfOrAdd(MaterialPalette.Purple);
        var tool = new PaintBucketTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left, ToolModifiers.Alt));

        Assert.Equal(MaterialPalette.Purple, tool.CurrentMaterial);
    }

    [Fact]
    public void Tool_PaintUndoRedo_RestoresAssignment()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new PaintBucketTool();
        tool.CurrentMaterial = MaterialPalette.Red;
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        Click(tool, pixel);

        undo.Undo();
        Assert.Equal(-1, top.MaterialIndex);

        undo.Redo();
        Assert.Equal(mesh.Materials.IndexOf(MaterialPalette.Red), top.MaterialIndex);
    }

    private static void Click(PaintBucketTool tool, Vector2d pixel)
    {
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));
    }
}
