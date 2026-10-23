using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Математика фигур рисования: плоскость построения, обратная трансформация,
/// кольца прямоугольника/окружности/многоугольника, дуги через три точки и по
/// хорде (радиус/угол), планарность контура.
/// </summary>
public class DrawingShapeMathTests
{
    private static void PointsEqual(Vector3d expected, Vector3d actual, double tolerance = 1e-9)
        => Assert.True(
            (expected - actual).Length() <= tolerance,
            $"Ожидалась точка {expected}, получена {actual}.");

    [Fact]
    public void DrawingPlane_Create_BuildsRightHandedOrthonormalBasis()
    {
        DrawingPlane plane = DrawingPlane.Create(new Vector3d(1, 2, 3), Vector3d.UnitZ, new Vector3d(1, 1, 0));

        PointsEqual(Vector3d.UnitX * Math.Sqrt(0.5) + Vector3d.UnitY * Math.Sqrt(0.5), plane.U);
        Assert.Equal(1, plane.Normal.Length(), 9);
        Assert.Equal(1, plane.U.Length(), 9);
        Assert.Equal(1, plane.V.Length(), 9);
        Assert.Equal(0, Vector3d.Dot(plane.U, plane.Normal), 9);
        PointsEqual(plane.Normal, Vector3d.Cross(plane.U, plane.V));
    }

    [Fact]
    public void DrawingPlane_TryIntersect_GroundWithTopDownRay()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();
        Ray3d ray = viewport.PickRay(InferenceTestSupport.Project(viewport, new Vector3d(12, -7, 0)));

        Assert.True(DrawingPlane.Ground.TryIntersect(ray, out Vector3d point));

        PointsEqual(new Vector3d(12, -7, 0), point, 1e-9);
    }

    [Fact]
    public void TransformInverter_RoundTripsTranslationAndRotation()
    {
        Transform3d transform = Transform3d.CreateTranslation(new Vector3d(3, -4, 5))
            * new Transform3d(
                0, 1, 0, 0,
                -1, 0, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1);
        var point = new Vector3d(7, 8, 9);

        Assert.True(TransformInverter.TryInvert(transform, out Transform3d inverse));

        PointsEqual(point, inverse.TransformPoint(transform.TransformPoint(point)), 1e-9);
    }

    [Fact]
    public void TransformInverter_SingularTransform_Fails()
    {
        Transform3d flat = new(
            1, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1);

        Assert.False(TransformInverter.TryInvert(flat, out _));
    }

    [Fact]
    public void RectRing_IsCounterClockwiseWithExactCorners()
    {
        DrawingPlane plane = DrawingPlane.Ground;

        IReadOnlyList<Vector3d> ring = DrawingShapes.RectRing(plane, 0, 0, 100, 50);

        DrawingTestSupport.PointsEqual(
            [new Vector3d(0, 0, 0), new Vector3d(100, 0, 0), new Vector3d(100, 50, 0), new Vector3d(0, 50, 0)],
            ring);
    }

    [Fact]
    public void RectRing_Degenerate_Throws()
    {
        Assert.Throws<ArgumentException>(() => DrawingShapes.RectRing(DrawingPlane.Ground, 0, 0, 0, 5));
    }

    [Fact]
    public void CircleRing_PutsVerticesExactlyOnRadius()
    {
        IReadOnlyList<Vector3d> ring = DrawingShapes.CircleRing(
            DrawingPlane.Ground, new Vector3d(3, 4, 0), 25, 16);

        Assert.Equal(16, ring.Count);
        DrawingTestSupport.AssertRingRadius(ring, new Vector3d(3, 4, 0), 25);
    }

    [Fact]
    public void PolygonRing_HasRequestedSidesAndRadius()
    {
        IReadOnlyList<Vector3d> ring = DrawingShapes.PolygonRing(
            DrawingPlane.Ground, Vector3d.Zero, 30, 8);

        Assert.Equal(8, ring.Count);
        DrawingTestSupport.AssertRingRadius(ring, Vector3d.Zero, 30);
        PointsEqual(new Vector3d(30, 0, 0), ring[0]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public void PolygonRing_TooFewSides_Throws(int sides)
        => Assert.Throws<ArgumentException>(() => DrawingShapes.PolygonRing(DrawingPlane.Ground, Vector3d.Zero, 10, sides));

    [Fact]
    public void TryCircumcircle_RightTriangle_FindsHypotenuseMidpoint()
    {
        Assert.True(DrawingShapes.TryCircumcircle(
            new Vector3d(0, 0, 0),
            new Vector3d(4, 0, 0),
            new Vector3d(0, 6, 0),
            out Vector3d center,
            out Vector3d normal,
            out double radius));

        PointsEqual(new Vector3d(2, 3, 0), center, 1e-9);
        PointsEqual(Vector3d.UnitZ, normal);
        Assert.Equal(Math.Sqrt(13), radius, 9);
    }

    [Fact]
    public void TryCircumcircle_CollinearPoints_Fails()
    {
        Assert.False(DrawingShapes.TryCircumcircle(
            new Vector3d(0, 0, 0),
            new Vector3d(5, 0, 0),
            new Vector3d(10, 0, 0),
            out _,
            out _,
            out _));
    }

    [Fact]
    public void ArcThrough3Points_QuarterCircle_PassesThroughMiddlePoint()
    {
        Vector3d start = new(10, 0, 0);
        Vector3d through = new(10 * Math.Cos(Math.PI / 4), 10 * Math.Sin(Math.PI / 4), 0);
        Vector3d end = new(0, 10, 0);

        IReadOnlyList<Vector3d>? arc = DrawingShapes.ArcThrough3Points(start, through, end);

        Assert.NotNull(arc);
        Assert.Equal(7, arc!.Count); // 90° / 15° = 6 сегментов
        PointsEqual(start, arc[0], 1e-9);
        PointsEqual(end, arc[^1], 1e-9);
        DrawingTestSupport.AssertRingRadius(arc, Vector3d.Zero, 10, 1e-9);
        PointsEqual(through, arc[3], 1e-6);
    }

    [Fact]
    public void ArcThrough3Points_Collinear_ReturnsNull()
    {
        Assert.Null(DrawingShapes.ArcThrough3Points(
            new Vector3d(0, 0, 0),
            new Vector3d(5, 0, 0),
            new Vector3d(10, 0, 0)));
    }

    [Fact]
    public void ArcChordRadius_Semicircle_BulgesTowardPoint()
    {
        IReadOnlyList<Vector3d> arc = DrawingShapes.ArcChordRadius(
            new Vector3d(-10, 0, 0),
            new Vector3d(10, 0, 0),
            10,
            new Vector3d(0, 20, 0),
            Vector3d.UnitZ);

        PointsEqual(new Vector3d(-10, 0, 0), arc[0], 1e-9);
        PointsEqual(new Vector3d(10, 0, 0), arc[^1], 1e-9);
        DrawingTestSupport.AssertRingRadius(arc, Vector3d.Zero, 10, 1e-9);
        Assert.All(arc, point => Assert.True(point.Y >= -1e-9, "Дуга ушла в противоположную сторону."));
    }

    [Fact]
    public void ArcChordAngle_QuarterAngle_GivesExactRadius()
    {
        IReadOnlyList<Vector3d> arc = DrawingShapes.ArcChordAngle(
            new Vector3d(-10, 0, 0),
            new Vector3d(10, 0, 0),
            90,
            new Vector3d(0, 20, 0),
            Vector3d.UnitZ);

        double radius = 20 / (2 * Math.Sin(Math.PI / 4));
        Assert.True(DrawingShapes.TryCircumcircle(arc[0], arc[arc.Count / 2], arc[^1], out _, out _, out double circumRadius));
        Assert.Equal(radius, circumRadius, 9);
    }

    [Fact]
    public void ArcChordRadius_RadiusBelowHalfChord_Throws()
    {
        Assert.Throws<ArgumentException>(() => DrawingShapes.ArcChordRadius(
            new Vector3d(-10, 0, 0),
            new Vector3d(10, 0, 0),
            5,
            new Vector3d(0, 20, 0),
            Vector3d.UnitZ));
    }

    [Fact]
    public void IsPlanar_PlanarAndNonPlanarContours()
    {
        Assert.True(DrawingShapes.IsPlanar(
            [new Vector3d(0, 0, 0), new Vector3d(5, 0, 0), new Vector3d(5, 5, 0), new Vector3d(0, 5, 0)]));
        Assert.False(DrawingShapes.IsPlanar(
            [new Vector3d(0, 0, 0), new Vector3d(5, 0, 0), new Vector3d(5, 5, 5), new Vector3d(0, 5, 0)]));
    }
}
