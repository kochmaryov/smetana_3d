using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Фиксированные сценарии булевых операций (задача 5.3): кубы (точные объёмы),
/// сфера+куб, топология результата (genus, V−E+F), пустой результат.
/// </summary>
public class BooleanScenarioTests
{
    private static HalfEdgeMesh Moved(HalfEdgeMesh mesh, double x, double y, double z)
        => MeshTransforms.Transformed(mesh, Transform3d.CreateTranslation(new Vector3d(x, y, z)));

    [Fact]
    public void Union_DisjointCubes_VolumeExactlyTwo()
    {
        HalfEdgeMesh a = TestMeshes.Cube(1, 1, 1);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 3, 0, 0);

        HalfEdgeMesh result = CsgOperations.Union(a, b);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "union непересекающихся кубов");
        CsgAssert.AssertNear(m.Volume, 2.0, 1e-9, "объём двух кубов 1×1×1");
        Assert.Equal(24, m.FaceCount);
    }

    [Fact]
    public void Difference_Cubes_VolumeSevenAndHalf()
    {
        // A = куб 2×2×2, B = куб 1×1×1 со сдвигом (1,0,0): A∖B = 8 − 0.5 = 7.5.
        HalfEdgeMesh a = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 1, 0, 0);

        HalfEdgeMesh result = CsgOperations.Difference(a, b);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "difference кубов");
        CsgAssert.AssertNear(m.Volume, 7.5, 1e-9, "объём A∖B");
    }

    [Fact]
    public void Intersection_Cubes_VolumeHalf()
    {
        HalfEdgeMesh a = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 1, 0, 0);

        HalfEdgeMesh result = CsgOperations.Intersection(a, b);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "intersection кубов");
        CsgAssert.AssertNear(m.Volume, 0.5, 1e-9, "объём A∩B");
    }

    [Fact]
    public void Union_CubeAndSphere_ClosedGenusZero()
    {
        // Сфера r=1.2 с центром на грани куба 2×2×2: частичное пересечение с кривой CSG.
        HalfEdgeMesh cube = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh sphere = Moved(TestMeshes.Sphere(1.2, rings: 48, segments: 96), 1, 0, 0);

        HalfEdgeMesh result = CsgOperations.Union(cube, sphere);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "union куб+сфера");
        double sphereVolume = TestMeshes.AnalyticSphereVolume(1.2);
        Assert.True(
            m.Volume > 8.0 && m.Volume < 8.0 + sphereVolume,
            $"объём {m.Volume:F4} вне границ (8, {8.0 + sphereVolume:F4})");
        Assert.Equal(1, m.ComponentCount);
        Assert.Equal(0, m.Genus);
    }

    [Fact]
    public void Difference_CubeMinusThroughBox_CreatesGenusOne()
    {
        // Сквозное отверстие 0.5×0.5 в кубе 2×2×2: результат — плита с дыркой, род 1.
        HalfEdgeMesh cube = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh drill = TestMeshes.Cube(0.5, 0.5, 3);

        HalfEdgeMesh result = CsgOperations.Difference(cube, drill);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "куб со сквозным отверстием");
        CsgAssert.AssertNear(m.Volume, 7.5, 1e-9, "объём 8 − 0.5·0.5·2");
        Assert.Equal(0, m.EulerCharacteristic);
        Assert.Equal(1, m.Genus);
    }

    [Fact]
    public void Difference_CubeMinusInnerSphere_CreatesCavity()
    {
        // Сфера целиком внутри куба: замкнутая полость, χ = 4 (две компоненты поверхности), род −1.
        HalfEdgeMesh cube = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh ball = TestMeshes.Sphere(0.5, rings: 32, segments: 64);

        HalfEdgeMesh result = CsgOperations.Difference(cube, ball);

        MeshMetrics m = CsgAssert.AssertValidResult(result, "куб с внутренней полостью");
        Assert.Equal(4, m.EulerCharacteristic);
        Assert.Equal(-1, m.Genus);
        double expected = 8.0 - TestMeshes.AnalyticSphereVolume(0.5);
        CsgAssert.AssertNear(m.Volume, expected, 0.02, "объём куба за вычетом сферы (допуск на полиэдризацию)");
    }

    [Fact]
    public void EmptyResult_DifferenceOfIdenticalCubes_IsEmptyMesh()
    {
        HalfEdgeMesh cube = TestMeshes.Cube(1, 1, 1);

        HalfEdgeMesh result = CsgOperations.Difference(cube, cube);

        Assert.Empty(result.Faces);
        Assert.Empty(result.Vertices);

        using ManifoldSolid deferred = CsgOperations.DifferenceDeferred(cube, cube);
        Assert.True(deferred.IsEmpty);
        Assert.Equal(ManifoldError.NoError, deferred.Status);
        Assert.Empty(deferred.ToHalfEdgeMesh().Faces);
    }
}
