using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Инструменты публикуют результат привязок для подсказок вьюпорта (W2).</summary>
public sealed class InferenceToolSnapPublishTests
{
    [Fact]
    public void LineTool_MoveNearVertex_PublishesEndpointSnap()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        // Подсказка публикуется уже на наведении — до первого клика.
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(0.5, 0.5, 10));

        Assert.NotNull(tool.CurrentSnap.Best);
        Assert.Equal(SnapType.Endpoint, tool.CurrentSnap.Best!.Type);
        Assert.Equal(0, tool.CurrentSnap.Best.DegreesOfFreedom);
        InferenceAssert.PointEqual(new Vector3d(0, 0, 10), tool.CurrentSnap.Best.Point);
    }

    [Fact]
    public void LineTool_Deactivate_ClearsPublishedSnap()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(0.5, 0.5, 10));

        tool.Deactivate();

        Assert.True(tool.CurrentSnap.IsEmpty);
    }
}
