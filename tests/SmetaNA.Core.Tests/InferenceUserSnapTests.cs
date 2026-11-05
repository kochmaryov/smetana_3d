using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W5: пользовательские точки сборки (Snap) в снимке сцены, движке привязок,
/// клонировании и переносе вместе с контейнером.
/// </summary>
public sealed class InferenceUserSnapTests
{
    private static Scene SceneWithSnapInsideGroup(Vector3d localPosition, out SnapNode snap)
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            Name = "Стол",
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        snap = new SnapNode
        {
            Name = "Snap столешницы",
            Frame = new SnapFrame(localPosition, Vector3d.UnitZ, Vector3d.UnitY),
        };
        group.AddChild(snap);
        scene.Root.AddChild(group);
        return scene;
    }

    [Fact]
    public void Snapshot_ContainsUserSnap_InWorldCoordinates()
    {
        Scene scene = SceneWithSnapInsideGroup(new Vector3d(5, 6, 7), out _);

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        SnapUserPoint point = Assert.Single(snapshot.UserSnaps);
        InferenceAssert.PointEqual(new Vector3d(105, 6, 7), point.Frame.Position);
        Assert.Equal(SnapEntityKind.UserSnap, point.Entity.Kind);
        Assert.Equal("Стол", point.Entity.DisplayName);
    }

    [Fact]
    public void Snapshot_FollowsContainerTransform()
    {
        Scene scene = SceneWithSnapInsideGroup(new Vector3d(1, 0, 0), out SnapNode snap);
        var group = (GroupNode)scene.Root.Children[0];

        // поворот группы на 90° вокруг Z: локальная (1,0,0) уходит в (0,1,0)
        group.LocalTransform = new Transform3d(
            0, 1, 0, 0,
            -1, 0, 0, 0,
            0, 0, 1, 0,
            100, 0, 0, 1);

        SnapUserPoint point = Assert.Single(InferenceSceneBuilder.FromScene(scene).UserSnaps);

        InferenceAssert.PointEqual(new Vector3d(100, 1, 0), point.Frame.Position);
        InferenceAssert.DirectionEqual(Vector3d.UnitZ, point.Frame.Direction);
        Assert.NotNull(snap);
    }

    [Fact]
    public void Engine_SnapsToUserSnap_AndCarriesFrame()
    {
        InferenceScene scene = new InferenceScene(
            [],
            [],
            userSnaps:
            [
                new SnapUserPoint(
                    new SnapFrame(new Vector3d(20, 20, 0), Vector3d.UnitZ, Vector3d.UnitY),
                    new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.UserSnap, null, "Стол")),
            ]);

        SnapResult result = InferenceEngine.Solve(
            InferenceTestSupport.Request(InferenceTestSupport.TopDown(), scene, new Vector3d(20.4, 20.4, 0)));

        SnapCandidate best = Assert.IsType<SnapCandidate>(result.Best);
        Assert.Equal(SnapType.SnapPoint, best.Type);
        Assert.Equal(SnapDefaults.SnapPointPriority, best.Priority);
        Assert.NotNull(best.Frame);
        InferenceAssert.DirectionEqual(Vector3d.UnitZ, best.Frame!.Value.Direction);
        InferenceAssert.PointEqual(new Vector3d(20, 20, 0), best.Point);
    }

    [Fact]
    public void Engine_UserSnapBeatsEndpoint_AtSamePoint()
    {
        var segment = new SnapSegment(new Vector3d(20, 20, 0), new Vector3d(40, 20, 0));
        InferenceScene scene = new InferenceScene(
            [segment],
            [],
            userSnaps:
            [
                new SnapUserPoint(
                    new SnapFrame(new Vector3d(20, 20, 0), Vector3d.UnitZ, Vector3d.UnitY),
                    new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.UserSnap, null, "Стол")),
            ]);

        SnapResult result = InferenceEngine.Solve(
            InferenceTestSupport.Request(InferenceTestSupport.TopDown(), scene, new Vector3d(20.2, 20.2, 0)));

        Assert.Equal(SnapType.SnapPoint, result.Best!.Type);
    }

    [Fact]
    public void Engine_SnapPointsFilter_DisablesUserSnaps()
    {
        InferenceScene scene = new InferenceScene(
            [],
            [],
            userSnaps:
            [
                new SnapUserPoint(
                    new SnapFrame(new Vector3d(20, 20, 0), Vector3d.UnitZ, Vector3d.UnitY),
                    SnapEntityRef.None),
            ]);

        SnapResult result = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            scene,
            new Vector3d(20.2, 20.2, 0),
            filter: SnapFilter.All & ~SnapFilter.SnapPoints));

        Assert.NotEqual(SnapType.SnapPoint, result.Best?.Type);
    }

    [Fact]
    public void ToolContext_SnapScene_IncludesUserSnaps()
    {
        Scene scene = SceneWithSnapInsideGroup(new Vector3d(5, 5, 5), out _);
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);

        Assert.Single(context.SnapScene.UserSnaps);
    }

    [Fact]
    public void NodeCloner_CopiesSnapNodes()
    {
        Scene scene = SceneWithSnapInsideGroup(new Vector3d(2, 3, 4), out SnapNode snap);
        var group = (GroupNode)scene.Root.Children[0];

        var copy = Assert.IsType<GroupNode>(NodeCloner.DeepCopy(group));
        var copiedSnap = Assert.IsType<SnapNode>(Assert.Single(copy.Children));

        Assert.NotSame(snap, copiedSnap);
        Assert.Equal(snap.Frame, copiedSnap.Frame);
        Assert.Equal(snap.Name, copiedSnap.Name);
    }

    [Fact]
    public void SetSnapFrameCommand_UndoRestoresPreviousFrame()
    {
        Scene scene = SceneWithSnapInsideGroup(new Vector3d(1, 1, 1), out SnapNode snap);
        SnapFrame original = snap.Frame;
        var undo = new SmetaNA.Core.Undo.UndoStack();
        Assert.True(SnapFrame.TryCreate(new Vector3d(1, 1, 1), Vector3d.UnitX, Vector3d.UnitY, out SnapFrame rotated));

        undo.Push(new SetSnapFrameCommand(snap, rotated));
        Assert.Equal(rotated.Direction, snap.Frame.Direction);

        undo.Undo();
        Assert.Equal(original.Direction, snap.Frame.Direction);
        Assert.Equal(original.Up, snap.Frame.Up);
    }

    [Fact]
    public void SnapQuery_FindsSnapsInSceneAndInsideComponents()
    {
        (Scene scene, _) = ToolsTestSupport.SceneWith(ToolsTestSupport.Box(10, 10, 10));
        var group = new GroupNode { Name = "Стол" };
        group.AddChild(new SnapNode { Frame = new SnapFrame(new Vector3d(1, 2, 3), Vector3d.UnitZ, Vector3d.UnitY) });
        scene.Root.AddChild(group);

        var definition = new ComponentDefinition { Name = "Ножка" };
        definition.Content.AddChild(new SnapNode
        {
            Frame = new SnapFrame(new Vector3d(9, 9, 9), Vector3d.UnitZ, Vector3d.UnitY),
        });
        scene.Root.AddChild(new ComponentInstanceNode { Definition = definition });

        Assert.Equal(2, SnapQuery.FindInScene(scene).Count);
        Assert.Single(SnapQuery.FindIn(group));
    }

    [Fact]
    public void SnapQuery_AreConnected_FrontToFront()
    {
        SnapFrame front = new(new Vector3d(10, 0, 0), Vector3d.UnitZ, Vector3d.UnitY);
        SnapFrame back = new(new Vector3d(10, 0, 0), -Vector3d.UnitZ, Vector3d.UnitY);
        SnapFrame other = new(new Vector3d(10, 0, 0), Vector3d.UnitX, Vector3d.UnitY);

        Assert.True(SnapQuery.AreConnected(front, back));
        Assert.False(SnapQuery.AreConnected(front, other));
        Assert.False(SnapQuery.AreConnected(
            front,
            back with { Position = new Vector3d(11, 0, 0) }));
    }
}
