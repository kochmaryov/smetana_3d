using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструменты дуг: «Дуга (2 точки)» и «Дуга (3 точки)» — сценарии ToolInput,
/// VCB (угол/радиус), точные концы и радиус, undo/redo, отмена.
/// </summary>
public class DrawingArcTests
{
    private static double AngleDeg(Vector3d center, Vector3d from, Vector3d to)
    {
        Vector3d a = from - center;
        Vector3d b = to - center;
        double cosine = Vector3d.Dot(a, b) / (a.Length() * b.Length());
        return Math.Acos(Math.Clamp(cosine, -1, 1)) * 180.0 / Math.PI;
    }

    [Fact]
    public void Arc2Point_MouseFlow_CommitsArcWithExactEndsOnRequestedSide()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new Arc2PointTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(40, 20, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(20, 40, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(20, 40, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        IReadOnlyList<Vector3d> vertices = DrawingTestSupport.Vertices(mesh);
        DrawingTestSupport.PointEqual(new Vector3d(0, 20, 0), vertices[0], 1e-9);
        DrawingTestSupport.PointEqual(new Vector3d(40, 20, 0), vertices[^1], 1e-9);
        Assert.All(vertices, point => Assert.True(point.Y >= 20 - 1e-6, $"Дуга ушла вниз: {point}."));
        DrawingTestSupport.AssertRingRadius(vertices, new Vector3d(20, 20, 0), 20, 1e-6);
        Assert.Empty(mesh.Faces);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Arc2Point_VcbAngle_CommitsExactSweep()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new Arc2PointTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(40, 20, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(20, 40, 0));
        Assert.True(tool.TryApplyValue(90));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        IReadOnlyList<Vector3d> vertices = DrawingTestSupport.Vertices(mesh);
        DrawingTestSupport.PointEqual(new Vector3d(0, 20, 0), vertices[0], 1e-9);
        DrawingTestSupport.PointEqual(new Vector3d(40, 20, 0), vertices[^1], 1e-9);
        Assert.True(DrawingShapes.TryCircumcircle(
            vertices[0], vertices[vertices.Count / 2], vertices[^1],
            out Vector3d center, out _, out double radius));
        DrawingTestSupport.PointEqual(new Vector3d(20, 0, 0), center, 1e-6);
        Assert.Equal(20 / Math.Sin(Math.PI / 4), radius, 6);
        Assert.Equal(90, AngleDeg(center, vertices[0], vertices[^1]), 6);
    }

    [Fact]
    public void Arc2Point_VcbAngleBeforeChord_IsRejected()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new Arc2PointTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(90));
        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        Assert.False(tool.TryApplyValue(90));
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Arc2Point_Escape_CancelsWithoutSceneChanges()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new Arc2PointTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(40, 20, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(20, 40, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Escape);

        Assert.Empty(scene.Root.Children);
        Assert.False(undo.CanUndo);
        Assert.False(tool.IsInteracting);
    }

    [Fact]
    public void Arc3Point_MouseFlow_CommitsArcThroughMiddlePoint()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new Arc3PointTool();
        tool.Activate(context);

        var start = new Vector3d(20, 0, 0);
        var through = new Vector3d(20 * Math.Cos(Math.PI / 4), 20 * Math.Sin(Math.PI / 4), 0);
        var end = new Vector3d(0, 20, 0);
        DrawingTestSupport.Click(context, tool, start);
        DrawingTestSupport.Click(context, tool, through);
        DrawingTestSupport.MoveTo(context, tool, end);
        DrawingTestSupport.Click(context, tool, end);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        IReadOnlyList<Vector3d> vertices = DrawingTestSupport.Vertices(mesh);
        DrawingTestSupport.PointEqual(start, vertices[0], 1e-9);
        DrawingTestSupport.PointEqual(end, vertices[^1], 1e-9);
        Assert.True(DrawingShapes.TryCircumcircle(
            vertices[0], vertices[vertices.Count / 2], vertices[^1],
            out Vector3d center, out _, out double radius));
        DrawingTestSupport.PointEqual(Vector3d.Zero, center, 1e-6);
        Assert.Equal(20, radius, 6);
        double toThrough = vertices.Min(point => (point - through).Length());
        Assert.True(toThrough <= 1e-6, $"Дуга не проходит через среднюю точку (ближайшая {toThrough}).");

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Arc3Point_VcbRadius_CommitsArcOfRadius()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new Arc3PointTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(20, 30, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(40, 20, 0));
        Assert.True(tool.TryApplyValue(20));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        IReadOnlyList<Vector3d> vertices = DrawingTestSupport.Vertices(mesh);
        DrawingTestSupport.PointEqual(new Vector3d(0, 20, 0), vertices[0], 1e-9);
        DrawingTestSupport.PointEqual(new Vector3d(40, 20, 0), vertices[^1], 1e-9);
        DrawingTestSupport.AssertRingRadius(vertices, new Vector3d(20, 20, 0), 20, 1e-6);
        Assert.All(vertices, point => Assert.True(point.Y >= 20 - 1e-6, $"Дуга ушла вниз: {point}."));
    }

    [Fact]
    public void Arc3Point_VcbRadiusWithoutEnd_IsRejected()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new Arc3PointTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(20));
        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        Assert.False(tool.TryApplyValue(20));
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Arc3Point_CollinearPoints_DoNotCommit()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new Arc3PointTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(20, 20, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(40, 20, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(40, 20, 0));

        Assert.Empty(scene.Root.Children);
        Assert.False(undo.CanUndo);
    }
}
