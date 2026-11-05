using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Аудит §15.4 (P1): пересечение ребра с гранью (R-6) — ребро пробивает
/// плоскость грани внутри её контура.
/// </summary>
public sealed class InferenceEngineSegmentFaceIntersectionTests
{
    private static InferenceScene FaceWithPiercingEdge() =>
        new(
            [
                // вертикальное ребро в (20, 10) проходит сквозь плоскость z = 0
                new SnapSegment(new Vector3d(20, 10, -5), new Vector3d(20, 10, 5))
                {
                    Entity = new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.Edge, 0, "Edge"),
                },
                // ребро, не доходящее до плоскости: пересечения нет
                new SnapSegment(new Vector3d(25, 25, 3), new Vector3d(25, 25, 8)),
            ],
            [
                new SnapFace([
                    new Vector3d(0, 0, 0), new Vector3d(30, 0, 0), new Vector3d(30, 30, 0), new Vector3d(0, 30, 0),
                ])
                {
                    Entity = new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.Face, 0, "Face"),
                },
            ]);

    [Fact]
    public void SegmentThroughFace_ProducesIntersection()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            FaceWithPiercingEdge(),
            new Vector3d(20, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate intersection = InferenceAssert.Single(result, SnapType.Intersection);
        InferenceAssert.PointEqual(new Vector3d(20, 10, 0), intersection.Point, 1e-6);
        Assert.Equal(SnapEntityKind.Face, intersection.Entity.Kind);
    }

    [Fact]
    public void SegmentEndingBeforeFace_ProducesNoIntersection()
    {
        var scene = new InferenceScene(
            [new SnapSegment(new Vector3d(20, 10, 3), new Vector3d(20, 10, 8))],
            [
                new SnapFace([
                    new Vector3d(0, 0, 0), new Vector3d(30, 0, 0), new Vector3d(30, 30, 0), new Vector3d(0, 30, 0),
                ]),
            ]);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(20, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Intersection);
    }

    [Fact]
    public void SegmentOutsideFaceContour_ProducesNoIntersection()
    {
        var scene = new InferenceScene(
            [new SnapSegment(new Vector3d(50, 50, -5), new Vector3d(50, 50, 5))],
            [
                new SnapFace([
                    new Vector3d(0, 0, 0), new Vector3d(30, 0, 0), new Vector3d(30, 30, 0), new Vector3d(0, 30, 0),
                ]),
            ]);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(50, 50, 0));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Intersection);
    }
}
