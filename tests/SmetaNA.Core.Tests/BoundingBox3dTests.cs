using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class BoundingBox3dTests
{
    [Fact]
    public void Empty_IsEmpty_AndNeutralForUnion()
    {
        var box = new BoundingBox3d(new Vector3d(-1, -2, -3), new Vector3d(1, 2, 3));

        Assert.True(BoundingBox3d.Empty.IsEmpty);
        Assert.Equal(box, BoundingBox3d.Empty.Union(box));
        Assert.Equal(box, box.Union(BoundingBox3d.Empty));
    }

    [Fact]
    public void FromPoints_ComputesMinMax()
    {
        BoundingBox3d box = BoundingBox3d.FromPoints(
        [
            new Vector3d(1, -2, 3),
            new Vector3d(-4, 5, 6),
            new Vector3d(0, 0, 0),
        ]);

        GeometryAssert.VectorEqual(new Vector3d(-4, -2, 0), box.Min);
        GeometryAssert.VectorEqual(new Vector3d(1, 5, 6), box.Max);
    }

    [Fact]
    public void FromPoints_Empty_ReturnsEmpty()
    {
        Assert.True(BoundingBox3d.FromPoints([]).IsEmpty);
    }

    [Fact]
    public void CenterSizeAndDiagonal_OfUnitCube()
    {
        BoundingBox3d box = BoundingBox3d.FromPoints([new Vector3d(0, 0, 0), new Vector3d(1, 1, 1)]);

        GeometryAssert.VectorEqual(new Vector3d(0.5, 0.5, 0.5), box.Center);
        GeometryAssert.VectorEqual(new Vector3d(1, 1, 1), box.Size);
        Assert.Equal(Math.Sqrt(3), box.DiagonalLength, 9);
        Assert.Equal(0, BoundingBox3d.Empty.DiagonalLength);
    }

    [Fact]
    public void Contains_IsInclusive_AndEmptyContainsNothing()
    {
        var box = new BoundingBox3d(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));

        Assert.True(box.Contains(new Vector3d(0, 0, 0)));
        Assert.True(box.Contains(new Vector3d(1, 1, 1)));
        Assert.True(box.Contains(new Vector3d(0.5, 0.5, 0.5)));
        Assert.False(box.Contains(new Vector3d(1.5, 0.5, 0.5)));
        Assert.False(BoundingBox3d.Empty.Contains(new Vector3d(0, 0, 0)));
    }

    [Fact]
    public void Expanded_GrowsAndShrinks()
    {
        var box = new BoundingBox3d(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));

        BoundingBox3d grown = box.Expanded(0.5);
        GeometryAssert.VectorEqual(new Vector3d(-0.5, -0.5, -0.5), grown.Min);
        GeometryAssert.VectorEqual(new Vector3d(1.5, 1.5, 1.5), grown.Max);

        Assert.True(box.Expanded(-1).IsEmpty);
    }

    [Fact]
    public void Union_CombinesBoxes()
    {
        var first = new BoundingBox3d(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
        var second = new BoundingBox3d(new Vector3d(-2, 0.5, 3), new Vector3d(-1, 2, 4));

        BoundingBox3d union = first.Union(second);

        GeometryAssert.VectorEqual(new Vector3d(-2, 0, 0), union.Min);
        GeometryAssert.VectorEqual(new Vector3d(1, 2, 4), union.Max);
    }

    [Fact]
    public void Transformed_RotationAroundZ_KeepsCenteredBox()
    {
        var box = new BoundingBox3d(new Vector3d(-1, -1, -1), new Vector3d(1, 1, 1));

        BoundingBox3d rotated = box.Transformed(TestMeshes.RotationZ(Math.PI / 2));

        GeometryAssert.VectorEqual(new Vector3d(-1, -1, -1), rotated.Min);
        GeometryAssert.VectorEqual(new Vector3d(1, 1, 1), rotated.Max);
    }

    [Fact]
    public void Transformed_TranslationAndScale_ShiftsBox()
    {
        var box = new BoundingBox3d(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2));
        Transform3d transform = Transform3d.Multiply(TestMeshes.Scale(2, 2, 2), Transform3d.CreateTranslation(new Vector3d(5, 0, 0)));

        BoundingBox3d moved = box.Transformed(transform);

        GeometryAssert.VectorEqual(new Vector3d(7, 2, 2), moved.Min);
        GeometryAssert.VectorEqual(new Vector3d(9, 4, 4), moved.Max);
    }

    [Fact]
    public void FromMeshAndFromMeshData_CoverCubeVertices()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();
        MeshData data = mesh.ToTriangles();

        BoundingBox3d fromMesh = BoundingBox3d.FromMesh(mesh);
        BoundingBox3d fromData = BoundingBox3d.FromMeshData(data);

        GeometryAssert.VectorEqual(new Vector3d(0, 0, 0), fromMesh.Min);
        GeometryAssert.VectorEqual(new Vector3d(1, 1, 1), fromMesh.Max);
        Assert.Equal(fromMesh, fromData);
    }
}
