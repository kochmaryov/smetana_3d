using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Линейные ограничения (оси, перпендикуляр, параллель, тангенс) и комбинированные привязки.</summary>
public sealed class InferenceEngineConstraintTests
{
    [Theory]
    [InlineData(12, 0, 0, SnapType.AxisX)]
    [InlineData(0, 12, 0, SnapType.AxisY)]
    public void Axis_TopDownWithoutAnchor_ConstrainsThroughOrigin(
        double cursorX, double cursorY, double cursorZ, SnapType expected)
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(cursorX, cursorY, cursorZ));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(expected, result.Best!.Type);
        Assert.Null(result.Best.SecondaryPoint);
        Assert.Equal(SnapMarkerKind.Line, result.Best.Marker);
    }

    [Fact]
    public void AxisZ_FrontView_ConstrainsThroughOrigin()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.Front(), InferenceScene.Empty, new Vector3d(0, 0, 12));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisZ, result.Best!.Type);
        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), result.Best.Direction!.Value);
    }

    [Fact]
    public void Axis_TopView_ProducesNoAxisParallelToView()
    {
        // Ось Z параллельна лучу взгляда — привязка к ней на виде сверху невозможна.
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(0, 0, 50));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.AxisZ);
    }

    [Fact]
    public void Axis_WithAnchor_ConstrainsThroughAnchor()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(12, 5, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisX, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(12, 5, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(1, 0, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Axis_WithAnchor_PointLiesOnAxisLineNotOnCursor()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(12, 6.5, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate axis = InferenceAssert.Single(result, SnapType.AxisX);
        Assert.Equal(5, axis.Point.Y, 6);
        Assert.Equal(0, axis.Point.Z, 6);
        Assert.InRange(axis.Point.X, 11.5, 12.5);
    }

    [Fact]
    public void Perpendicular_FromAnchorToEdge_SnapsToFoot()
    {
        var anchor = new Vector3d(17, 28, 0);
        var foot = new Vector3d(22.5, 22.5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.DiagonalEdge(), foot, anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Perpendicular, result.Best!.Type);
        InferenceAssert.PointEqual(foot, result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(1, -1, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Perpendicular_AtMidpoint_IsCombinedAndOutranksMidpoint()
    {
        var anchor = new Vector3d(25, 15, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.DiagonalEdge(), new Vector3d(20, 20, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Perpendicular, result.Best!.Type);
        Assert.True(result.Best.IsCombined);
        Assert.Equal(SnapMarkerKind.LineAndPoint, result.Best.Marker);
        Assert.Equal(SnapDefaults.MidpointPriority + 1, result.Best.Priority);
        InferenceAssert.PointEqual(new Vector3d(20, 20, 0), result.Best.Point);
    }

    [Fact]
    public void Perpendicular_WithoutAnchor_IsNotProduced()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.DiagonalEdge(), new Vector3d(22.5, 22.5, 0));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Perpendicular);
    }

    [Fact]
    public void Parallel_FromAnchorToEdgeDirection_SnapsAlongDirection()
    {
        var anchor = new Vector3d(5, 20, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.DiagonalEdge(), new Vector3d(15, 30, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Parallel, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(15, 30, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(1, 1, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Parallel_EqualDirections_AreDeduplicated()
    {
        var scene = new InferenceScene(
            [
                new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 10, 0)),
                new SnapSegment(new Vector3d(10, 30, 0), new Vector3d(30, 30, 0)),
            ],
            []);
        var anchor = new Vector3d(5, 20, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(15, 20, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.Single(result.Candidates, c => c.Type == SnapType.Parallel);
    }

    [Fact]
    public void Tangent_FromAnchorToCircle_SnapsToTangentPoint()
    {
        var scene = new InferenceScene(
            [],
            [],
            [new SnapCircle(new Vector3d(10, 10, 0), Vector3d.UnitZ, 5)]);
        var anchor = new Vector3d(20, 10, 0);
        var tangentPoint = new Vector3d(12.5, 10 + 5 * Math.Sqrt(3) / 2, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, tangentPoint, anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Tangent, result.Best!.Type);
        InferenceAssert.PointEqual(tangentPoint, result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(-Math.Sqrt(3) / 2, 0.5, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Tangent_WithoutAnchor_IsNotProduced()
    {
        var scene = new InferenceScene(
            [],
            [],
            [new SnapCircle(new Vector3d(10, 10, 0), Vector3d.UnitZ, 5)]);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(12.5, 10 + 5 * Math.Sqrt(3) / 2, 0));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Tangent);
    }

    [Fact]
    public void Tangent_AnchorInsideCircle_IsNotProduced()
    {
        var scene = new InferenceScene(
            [],
            [],
            [new SnapCircle(new Vector3d(10, 10, 0), Vector3d.UnitZ, 5)]);
        var anchor = new Vector3d(10, 12, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(12.5, 14.33, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Tangent);
    }

    [Fact]
    public void Combined_EndpointOnAxis_OutranksPlainEndpoint()
    {
        var scene = new InferenceScene(
            [new SnapSegment(new Vector3d(10, 14, 0), new Vector3d(20, 44, 0))],
            []);
        var anchor = new Vector3d(15, 14, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(10, 14, 0), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisX, result.Best!.Type);
        Assert.True(result.Best.IsCombined);
        Assert.Equal(SnapDefaults.EndpointPriority + 1, result.Best.Priority);
        InferenceAssert.PointEqual(new Vector3d(10, 14, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(1, 0, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Combined_OnFaceAlongAxis_ProducesConstrainedPoint()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.Cube(), new Vector3d(5, 5, 20), anchor: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisZ, result.Best!.Type);
        Assert.True(result.Best.IsCombined);
        Assert.Equal(SnapDefaults.OnFacePriority + 1, result.Best.Priority);
        InferenceAssert.PointEqual(new Vector3d(5, 5, 20), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(anchor, result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void Modifiers_LockAxisX_SuppressesPointSnapsAndOtherAxes()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(12, 5, 0),
            anchor: anchor,
            modifiers: SnapModifiers.LockAxisX);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisX, result.Best!.Type);
        InferenceAssert.None(result, SnapType.Endpoint);
        InferenceAssert.None(result, SnapType.AxisY);
        Assert.All(result.Candidates, c => Assert.NotNull(c.Direction));
    }

    [Fact]
    public void Modifiers_LockAxisY_ProducesOnlyAxisY()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(5, 12, 0),
            anchor: anchor,
            modifiers: SnapModifiers.LockAxisY);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.AxisY, result.Best!.Type);
        InferenceAssert.None(result, SnapType.AxisX);
        InferenceAssert.None(result, SnapType.Midpoint);
    }

    [Fact]
    public void Modifiers_LockPerpendicular_ProducesOnlyPerpendicular()
    {
        var anchor = new Vector3d(17, 28, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.DiagonalEdge(),
            new Vector3d(22.5, 22.5, 0),
            anchor: anchor,
            modifiers: SnapModifiers.LockPerpendicular);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Perpendicular, result.Best!.Type);
        InferenceAssert.None(result, SnapType.Parallel);
        InferenceAssert.None(result, SnapType.Midpoint);
    }
}
