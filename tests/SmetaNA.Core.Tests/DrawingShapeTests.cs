using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструменты «Прямоугольник», «Окружность», «Многоугольник»: сценарии ToolInput,
/// VCB (точные размеры — DoD §5.4), рисование по грани сцены, undo/redo.
/// </summary>
public class DrawingShapeTests
{
    [Fact]
    public void Rectangle_TwoClicks_CreatesFaceWithExactCorners()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new RectangleTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(60, 40, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(60, 40, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointsEqual(
            [
                new Vector3d(10, 10, 0),
                new Vector3d(60, 10, 0),
                new Vector3d(60, 40, 0),
                new Vector3d(10, 40, 0),
            ],
            DrawingTestSupport.Vertices(mesh),
            1e-9);
        Assert.Single(mesh.Faces);
        Assert.Equal(8, mesh.HalfEdges.Count);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
        undo.Redo();
        Assert.Single(scene.Root.Children);
    }

    [Fact]
    public void Rectangle_VcbWidthAndHeight_CreatesExactRectangle()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new RectangleTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        Assert.True(tool.TryApplyValue(100));
        Assert.True(tool.TryApplyValue(50));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointsEqual(
            [
                new Vector3d(0, 0, 0),
                new Vector3d(100, 0, 0),
                new Vector3d(100, 50, 0),
                new Vector3d(0, 50, 0),
            ],
            DrawingTestSupport.Vertices(mesh));
        Assert.Single(mesh.Faces);
        Assert.False(tool.IsInteracting);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Rectangle_OnBoxFace_GoesIntoThatMesh_WithoutNewNode()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new RectangleTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(2, 2, 10));
        DrawingTestSupport.Click(context, tool, new Vector3d(8, 6, 10));

        Assert.Single(scene.Root.Children);
        Assert.Equal(12, box.Vertices.Count);
        Assert.Equal(7, box.Faces.Count);
        DrawingTestSupport.PointsEqual(
            [
                new Vector3d(2, 2, 10),
                new Vector3d(8, 2, 10),
                new Vector3d(8, 6, 10),
                new Vector3d(2, 6, 10),
            ],
            box.Vertices.Skip(8).Select(vertex => vertex.Position),
            1e-9);

        undo.Undo();
        Assert.Equal(8, box.Vertices.Count);
        Assert.Equal(6, box.Faces.Count);
    }

    [Fact]
    public void Circle_VcbRadius_CreatesExactRadiusRing()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new CircleTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        Assert.True(tool.TryApplyValue(25));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Single(mesh.Faces);
        DrawingTestSupport.AssertRingRadius(DrawingTestSupport.Vertices(mesh), new Vector3d(0, 0, 0), 25);
        Assert.All(DrawingTestSupport.Vertices(mesh), point => Assert.Equal(0, point.Z, 9));

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Circle_MouseRadius_CommitsOnSecondClick()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new CircleTool(segments: 8);
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(30, 30, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(55, 30, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(55, 30, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(8, mesh.Vertices.Count);
        DrawingTestSupport.AssertRingRadius(
            DrawingTestSupport.Vertices(mesh), new Vector3d(30, 30, 0), 25, 1e-9);
    }

    [Fact]
    public void Circle_VcbRadiusBeforeCenter_IsRejected()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new CircleTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(25));
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Polygon_VcbSidesAndRadius_CreatesExactPolygon()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new PolygonTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyValue(8));
        Assert.Equal(8, tool.Sides);
        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        Assert.True(tool.TryApplyValue(30));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(8, mesh.Vertices.Count);
        Assert.Single(mesh.Faces);
        DrawingTestSupport.AssertRingRadius(DrawingTestSupport.Vertices(mesh), new Vector3d(0, 0, 0), 30);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Polygon_MouseWithDefaultSides_CreatesHexagon()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new PolygonTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(30, 30, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(50, 30, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 30, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(6, mesh.Vertices.Count);
        Assert.Single(mesh.Faces);
        DrawingTestSupport.AssertRingRadius(
            DrawingTestSupport.Vertices(mesh), new Vector3d(30, 30, 0), 20, 1e-9);
    }

    [Fact]
    public void Polygon_SidesAreClampedToValidRange()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new PolygonTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyValue(1));
        Assert.Equal(DrawingShapes.MinPolygonSides, tool.Sides);

        Assert.True(tool.TryApplyValue(500));
        Assert.Equal(DrawingShapes.MaxPolygonSides, tool.Sides);
    }

    [Fact]
    public void Circle_Escape_CancelsWithoutSceneChanges()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new CircleTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(30, 30, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(55, 30, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Escape);

        Assert.Empty(scene.Root.Children);
        Assert.False(undo.CanUndo);
        Assert.False(tool.IsInteracting);
    }
}
