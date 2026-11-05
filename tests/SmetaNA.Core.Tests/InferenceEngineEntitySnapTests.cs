using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Привязки W1: точка на ребре, продолжения, выравнивание от точки, сетка,
/// оси контекста и степень свободы кандидата.
/// </summary>
public sealed class InferenceEngineEntitySnapTests
{
    [Fact]
    public void OnEdge_NearEdge_SnapsToEdgeAndCarriesDirection()
    {
        var scene = new InferenceScene(
            [
                new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 10, 0))
                {
                    Entity = new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.Edge, 0, "Edge"),
                },
            ],
            []);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(15, 11.5, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.OnEdge, result.Best!.Type);
        // Проекция курсора на ребро в перспективе не совпадает с мировой точкой ровно.
        Assert.Equal(10, result.Best.Point.Y, 6);
        Assert.InRange(result.Best.Point.X, 14.9, 15.1);
        InferenceAssert.DirectionEqual(new Vector3d(1, 0, 0), result.Best.Direction!.Value);
        Assert.Equal(1, result.Best.DegreesOfFreedom);
        Assert.Equal(SnapMarkerKind.Square, result.Best.Marker);
        Assert.Equal(SnapEntityKind.Edge, result.Best.Entity.Kind);
    }

    [Fact]
    public void OnEdge_PointsOnly_KeepsPointButDropsDirection()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(15, 11.5, 0),
            modifiers: SnapModifiers.PointsOnly);

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate onEdge = InferenceAssert.Single(result, SnapType.OnEdge);
        Assert.Null(onEdge.Direction);
        Assert.Equal(1, onEdge.DegreesOfFreedom);
    }

    [Fact]
    public void ExtendEdge_BeyondSegmentEnd_SnapsToContinuation()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.SingleEdge(), new Vector3d(33, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.ExtendEdge, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(33, 10, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(1, 0, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(new Vector3d(30, 10, 0), result.Best.SecondaryPoint!.Value);
        Assert.NotNull(result.Best.ConstraintLine);
        Assert.Equal(1, result.Best.DegreesOfFreedom);
    }

    [Fact]
    public void FromPoint_ThroughReferencePoint_SnapsAlongDirection()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(15, 25, 0),
            anchor: new Vector3d(5, 5, 0),
            referencePoint: new Vector3d(15, 15, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.FromPoint, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(15, 25, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(0, 1, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(new Vector3d(15, 15, 0), result.Best.SecondaryPoint!.Value);
    }

    [Fact]
    public void FromPoint_SameAsAnchor_IsNotProduced()
    {
        var anchor = new Vector3d(5, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(15, 25, 0),
            anchor: anchor,
            referencePoint: anchor);

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.FromPoint);
    }

    [Fact]
    public void Grid_OnPlane_SnapsToNearestNode()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(11, 9, 0),
            grid: SnapGrid.Ground(5));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Grid, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(10, 10, 0), result.Best.Point);
        Assert.Equal(2, result.Best.DegreesOfFreedom);
        Assert.Equal(SnapEntityKind.Grid, result.Best.Entity.Kind);
    }

    [Fact]
    public void Grid_Disabled_IsNotProduced()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(11, 9, 0),
            grid: SnapGrid.Ground(5) with { IsEnabled = false });

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Grid);
    }

    [Fact]
    public void Grid_FilterDisabled_IsNotProduced()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(11, 9, 0),
            filter: SnapFilter.All & ~SnapFilter.Grid,
            grid: SnapGrid.Ground(5));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Grid);
    }

    [Fact]
    public void InstanceOrigin_AndAxes_AreSnapped()
    {
        var entity = new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.ComponentInstance, null, "Box");
        var instance = new SnapInstanceOrigin(
            new Vector3d(100, 0, 0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, entity);
        var scene = new InferenceScene([], [], [], [], [instance]);

        SnapResult originResult = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(100, 0, 0)));

        Assert.NotNull(originResult.Best);
        Assert.Equal(SnapType.InstanceOrigin, originResult.Best!.Type);
        Assert.Equal(0, originResult.Best.DegreesOfFreedom);
        Assert.Equal("Box", originResult.Best.Entity.DisplayName);

        // Вид спереди: ось Z не направлена в камеру и не конкурирует с X.
        SnapResult axisResult = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.Front(), scene, new Vector3d(110, 0, 1.5)));

        SnapCandidate axisX = InferenceAssert.Single(axisResult, SnapType.AxisInstanceX);
        InferenceAssert.DirectionEqual(new Vector3d(1, 0, 0), axisX.Direction!.Value);
        Assert.Equal(1, axisX.DegreesOfFreedom);
        Assert.Equal("Box", axisX.Entity.DisplayName);
    }

    [Fact]
    public void DegreesOfFreedom_EndpointIsZero_AxisIsOne_OnFaceIsTwo()
    {
        SnapResult endpoint = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.Oblique(), InferenceTestSupport.Cube(), new Vector3d(0, 0, 20)));
        Assert.NotNull(endpoint.Best);
        Assert.Equal(0, endpoint.Best!.DegreesOfFreedom);

        SnapResult face = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.Cube(), new Vector3d(5, 7, 20)));
        Assert.Equal(2, InferenceAssert.Single(face, SnapType.OnFace).DegreesOfFreedom);

        SnapResult axis = InferenceEngine.Solve(InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(12, 5, 0),
            anchor: new Vector3d(5, 5, 0)));
        Assert.NotNull(axis.Best);
        Assert.Equal(1, axis.Best!.DegreesOfFreedom);
    }
}
