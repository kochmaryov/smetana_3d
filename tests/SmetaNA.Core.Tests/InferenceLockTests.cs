using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Фиксация привязки (Shift) и состояние блокировок/режимов в контексте.</summary>
public sealed class InferenceLockTests
{
    [Fact]
    public void LockedPoint_IsReturnedAsIs()
    {
        var locked = new SnapCandidate(
            SnapType.Endpoint,
            new Vector3d(0, 0, 20),
            1,
            SnapDefaults.EndpointPriority,
            SnapMarkerKind.Cube)
        {
            DegreesOfFreedom = 0,
        };
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.Cube(),
            new Vector3d(10, 10, 20)) with { LockedCandidate = locked };

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(SnapType.Endpoint, candidate.Type);
        Assert.True(candidate.IsLocked);
        InferenceAssert.PointEqual(new Vector3d(0, 0, 20), candidate.Point);
    }

    [Fact]
    public void LockedDirection_SlidesPointAlongLine()
    {
        var locked = new SnapCandidate(
            SnapType.AxisX,
            new Vector3d(10, 10, 0),
            1,
            SnapDefaults.AxisPriority,
            SnapMarkerKind.Line)
        {
            Direction = Vector3d.UnitX,
            SecondaryPoint = new Vector3d(10, 10, 0),
            ConstraintLine = new SnapLine(new Vector3d(10, 10, 0), Vector3d.UnitX),
            DegreesOfFreedom = 1,
        };
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(15, 14, 0)) with { LockedCandidate = locked };

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate candidate = Assert.Single(result.Candidates);
        Assert.True(candidate.IsLocked);
        Assert.Equal(10, candidate.Point.Y, 6);
        Assert.InRange(candidate.Point.X, 14.9, 15.1);
    }

    [Fact]
    public void Context_ToggleSnapLock_SameAxisTogglesOff_OtherReplaces()
    {
        var context = new ToolContext(new Scene(), new UndoStack());

        context.ToggleSnapLock(SnapModifiers.LockAxisX);
        Assert.Equal(SnapModifiers.LockAxisX, context.LockModifiers);

        context.ToggleSnapLock(SnapModifiers.LockAxisX);
        Assert.Equal(SnapModifiers.None, context.LockModifiers);

        context.ToggleSnapLock(SnapModifiers.LockAxisX);
        context.ToggleSnapLock(SnapModifiers.LockAxisY);
        Assert.Equal(SnapModifiers.LockAxisY, context.LockModifiers);

        context.ResetSnapLocks();
        Assert.Equal(SnapModifiers.None, context.LockModifiers);
    }

    [Fact]
    public void Context_CycleInferenceMode_RunsAllPointsParallel()
    {
        var context = new ToolContext(new Scene(), new UndoStack());

        context.CycleInferenceMode();
        Assert.Equal(SnapInferenceMode.PointsOnly, context.InferenceMode);
        Assert.Equal(SnapModifiers.PointsOnly, context.ResolveSnapModifiers());

        context.CycleInferenceMode();
        Assert.Equal(SnapInferenceMode.ParallelPerpendicularOnly, context.InferenceMode);
        Assert.Equal(SnapModifiers.ParallelPerpendicularOnly, context.ResolveSnapModifiers());

        context.CycleInferenceMode();
        Assert.Equal(SnapInferenceMode.All, context.InferenceMode);
        Assert.Equal(SnapModifiers.None, context.ResolveSnapModifiers());
    }

    [Fact]
    public void Context_ResolveSnapModifiers_CombinesLockAndCtrl()
    {
        var context = new ToolContext(new Scene(), new UndoStack());
        context.ToggleSnapLock(SnapModifiers.LockAxisZ);

        SnapModifiers modifiers = context.ResolveSnapModifiers(ctrl: true);

        Assert.True((modifiers & SnapModifiers.LockAxisZ) != 0);
        Assert.True((modifiers & SnapModifiers.PointsOnly) != 0);
    }
}
