using SmetaNA.Core.Geometry;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W5+: shape inference прямоугольника (R-35) — почти квадрат и почти золотое
/// сечение доводятся до точной пропорции, в предпросмотре появляется подсказка.
/// </summary>
public sealed class RectangleShapeInferenceTests
{
    private static (ToolContext Context, RectangleTool Tool) Setup()
    {
        var scene = new Model.Scene();
        (ToolContext context, UndoStack _) = DrawingTestSupport.Setup(scene);
        var tool = new RectangleTool();
        tool.Activate(context);
        return (context, tool);
    }

    [Fact]
    public void AlmostSquare_BecomesExactSquare_WithHint()
    {
        (ToolContext context, RectangleTool tool) = Setup();

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(10.05, 9.95, 0));

        Assert.Equal(ShapeHintKind.Square, tool.ShapeHint);
        Assert.Contains(tool.Preview.Labels, label => label.LocalizationKey == "Shape.Square");
    }

    [Fact]
    public void AlmostGoldenRatio_IsSnapped_WithHint()
    {
        (ToolContext context, RectangleTool tool) = Setup();
        double width = 16;

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(width, width / 1.62, 0));

        Assert.Equal(ShapeHintKind.GoldenRatio, tool.ShapeHint);
        Assert.Contains(tool.Preview.Labels, label => label.LocalizationKey == "Shape.GoldenRatio");
    }

    [Fact]
    public void OrdinaryRectangle_HasNoHint()
    {
        (ToolContext context, RectangleTool tool) = Setup();

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(20, 5, 0));

        Assert.Equal(ShapeHintKind.None, tool.ShapeHint);
        Assert.DoesNotContain(tool.Preview.Labels, label => label.LocalizationKey is not null);
    }

    [Fact]
    public void SquareHint_CommitsChangedGeometry()
    {
        (ToolContext context, RectangleTool tool) = Setup();

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(10.05, 9.95, 0));

        var mesh = DrawingTestSupport.ShapeMeshOf(context.Scene);
        double minX = mesh.Vertices.Min(v => v.Position.X);
        double maxX = mesh.Vertices.Max(v => v.Position.X);
        double minY = mesh.Vertices.Min(v => v.Position.Y);
        double maxY = mesh.Vertices.Max(v => v.Position.Y);

        Assert.Equal(maxX - minX, maxY - minY, 6);
    }
}
