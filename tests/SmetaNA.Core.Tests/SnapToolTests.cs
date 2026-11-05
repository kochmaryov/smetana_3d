using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W5: инструмент «Точка сборки» — постановка центра с привязками, ориентация
/// вторым кликом, отмена шага и одна запись истории на snap.
/// </summary>
public sealed class SnapToolTests
{
    private static (ToolContext Context, UndoStack Undo, SnapTool Tool) Setup(Scene scene)
    {
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new SnapTool();
        tool.Activate(context);
        return (context, undo, tool);
    }

    [Fact]
    public void Click_ThenClick_AddsSnapWithOrientation_AsSingleUndoStep()
    {
        var scene = new Scene();
        (ToolContext context, UndoStack undo, SnapTool tool) = Setup(scene);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        Assert.NotNull(tool.Position);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 0, 0));

        SnapNode snap = Assert.IsType<SnapNode>(Assert.Single(scene.Root.Children));
        InferenceAssert.PointEqual(new Vector3d(0, 0, 0), snap.Frame.Position);
        InferenceAssert.DirectionEqual(Vector3d.UnitX, snap.Frame.Direction);
        Assert.Null(tool.Position);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Enter_FinishesWithDefaultBlueAxis()
    {
        var scene = new Scene();
        (ToolContext context, _, SnapTool tool) = Setup(scene);

        DrawingTestSupport.Click(context, tool, new Vector3d(5, 5, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        SnapNode snap = Assert.IsType<SnapNode>(Assert.Single(scene.Root.Children));
        InferenceAssert.DirectionEqual(SnapTool.DefaultDirection, snap.Frame.Direction);
    }

    [Fact]
    public void Escape_OnSecondStep_LeavesSceneUntouched()
    {
        var scene = new Scene();
        (ToolContext context, _, SnapTool tool) = Setup(scene);

        DrawingTestSupport.Click(context, tool, new Vector3d(5, 5, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Escape);

        Assert.Null(tool.Position);
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void SnapInsideGroup_IsAddedToPickedFaceParent()
    {
        (Scene scene, MeshNode mesh) = ToolsTestSupport.SceneWith(ToolsTestSupport.Box(10, 10, 10));
        var group = new GroupNode { Name = "Группа" };
        scene.Root.RemoveChild(mesh);
        group.AddChild(mesh);
        scene.Root.AddChild(group);

        (ToolContext context, _, SnapTool tool) = Setup(scene);
        DrawingTestSupport.Click(context, tool, new Vector3d(5, 5, 10));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        SnapNode snap = Assert.IsType<SnapNode>(Assert.Single(group.Children.OfType<SnapNode>()));
        Assert.Equal(10, snap.Frame.Position.Z, 6);
    }

    [Fact]
    public void Click_OnExistingSnap_EditsOrientation_InsteadOfCreatingNew()
    {
        var scene = new Scene();
        Assert.True(SnapFrame.TryCreate(new Vector3d(30, 30, 0), Vector3d.UnitZ, Vector3d.UnitY, out SnapFrame existing));
        var node = new SnapNode { Name = "Старый", Frame = existing };
        scene.Root.AddChild(node);

        (ToolContext context, UndoStack undo, SnapTool tool) = Setup(scene);

        DrawingTestSupport.Click(context, tool, new Vector3d(30.2, 30.2, 0));
        Assert.Same(node, tool.EditingSnap);

        DrawingTestSupport.Click(context, tool, new Vector3d(30, 30, 10));

        SnapNode snap = Assert.Single(scene.Root.Children.OfType<SnapNode>());
        Assert.Same(node, snap);
        InferenceAssert.DirectionEqual(Vector3d.UnitZ, snap.Frame.Direction);
        Assert.Equal("Ориентация точки сборки", undo.NextUndoDescription);
    }

    [Fact]
    public void OrientationWithAlt_FlipsDisk()
    {
        var scene = new Scene();
        (ToolContext context, _, SnapTool tool) = Setup(scene);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        tool.OnPointer(PointerInput.Press(
            DrawingTestSupport.Px(context, new Vector3d(10, 0, 0)),
            PointerButton.Left,
            ToolModifiers.Alt));

        SnapNode snap = Assert.IsType<SnapNode>(Assert.Single(scene.Root.Children));
        InferenceAssert.DirectionEqual(-Vector3d.UnitX, snap.Frame.Direction);
    }

    [Fact]
    public void Delete_RemovesHoveredSnap_AndUndoRestoresIt()
    {
        var scene = new Scene();
        Assert.True(SnapFrame.TryCreate(new Vector3d(30, 30, 0), Vector3d.UnitZ, Vector3d.UnitY, out SnapFrame existing));
        scene.Root.AddChild(new SnapNode { Name = "Старый", Frame = existing });

        (ToolContext context, UndoStack undo, SnapTool tool) = Setup(scene);
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(30.2, 30.2, 0));

        DrawingTestSupport.Key(context, tool, ToolKey.Delete);

        Assert.Empty(scene.Root.Children.OfType<SnapNode>());
        undo.Undo();
        Assert.Single(scene.Root.Children.OfType<SnapNode>());
    }
}
