using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Координатный ввод VCB в рисующих инструментах (W3).</summary>
public sealed class ToolCoordinateInputTests
{
    private static (Scene Scene, ToolContext Context) Setup()
    {
        var scene = new Scene();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        return (scene, context);
    }

    [Fact]
    public void Rectangle_AbsolutePoints_CommitExactRing()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new RectangleTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 5, 0), relative: false));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Contains(mesh.Vertices, vertex => ToolsTestSupport.NearlyEqual(vertex.Position, new Vector3d(10, 5, 0)));
    }

    [Fact]
    public void Rectangle_RelativeSecondPoint_IsOffsetFromCorner()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new RectangleTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(2, 2, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(8, 3, 0), relative: true));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Contains(mesh.Vertices, vertex => ToolsTestSupport.NearlyEqual(vertex.Position, new Vector3d(10, 5, 0)));
    }

    [Fact]
    public void Rectangle_RelativeFirstPoint_IsRejected()
    {
        (_, ToolContext context) = Setup();
        var tool = new RectangleTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyPoint(new Vector3d(1, 1, 0), relative: true));
    }

    [Fact]
    public void Line_TwoPoints_CommitOpenSegment()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new LineTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 0, 0), relative: true));

        Assert.True(tool.OnPointer(PointerInput.Press(new Vector2d(0, 0), PointerButton.Right)));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(2, mesh.Vertices.Count);
        Assert.Contains(mesh.Vertices, vertex => ToolsTestSupport.NearlyEqual(vertex.Position, new Vector3d(10, 0, 0)));
    }

    [Fact]
    public void Circle_CenterAndRadiusPoint_CommitExactRadius()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new CircleTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(5, 0, 0), relative: false));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.All(mesh.Vertices, vertex => Assert.Equal(5, vertex.Position.Length(), 6));
    }

    [Fact]
    public void Polygon_CenterAndRadiusPoint_CommitRing()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new PolygonTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(0, 5, 0), relative: false));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.All(mesh.Vertices, vertex => Assert.Equal(5, vertex.Position.Length(), 6));
    }

    [Fact]
    public void Arc2_StartEndBulgePoint_CommitArc()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new Arc2PointTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(5, 5, 0), relative: false));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Contains(mesh.Vertices, vertex => vertex.Position.Y > 1);
    }

    [Fact]
    public void Arc3_ThreePoints_CommitArc()
    {
        (Scene scene, ToolContext context) = Setup();
        var tool = new Arc3PointTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(5, 5, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 0, 0), relative: false));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Contains(mesh.Vertices, vertex => vertex.Position.Y > 1);
    }
}
