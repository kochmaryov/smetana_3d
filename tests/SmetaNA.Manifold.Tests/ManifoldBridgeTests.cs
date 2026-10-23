using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Мост HalfEdgeMesh/MeshData ↔ Manifold: round-trip, валидность (manifold_status),
/// genus, объём, V−E+F; диагностика невалидного входа; SDF-колбэк из C#.
/// </summary>
public class ManifoldBridgeTests
{
    [Fact]
    public void Cube_RoundTrip_PreservesTopologyAndVolume()
    {
        HalfEdgeMesh cube = TestMeshes.Cube(1, 1, 1);
        using ManifoldSolid s = ManifoldBridge.ToSolid(cube);
        Assert.Equal(ManifoldError.NoError, s.Status);
        Assert.Equal(12, s.TriangleCount);
        CsgAssert.AssertNear(s.Volume, 1.0, 1e-9, "объём куба 1×1×1");
        Assert.Equal(0, s.Genus);
        Assert.Equal(2, s.VertexCount - s.EdgeCount + s.TriangleCount);

        HalfEdgeMesh back = s.ToHalfEdgeMesh();
        MeshMetrics m = CsgAssert.AssertValidResult(back, "cube round-trip");
        CsgAssert.AssertNear(m.Volume, 1.0, 1e-9, "объём после round-trip");
        Assert.Equal(12, m.FaceCount);
    }

    [Fact]
    public void MeshData_RoundTrip_PreservesVolume()
    {
        MeshData soup = TestMeshes.Cube(1, 1, 1).ToTriangles();
        using ManifoldSolid s = ManifoldBridge.ToSolid(soup);
        Assert.Equal(ManifoldError.NoError, s.Status);

        MeshData back = s.ToMeshData();
        using ManifoldSolid s2 = ManifoldBridge.ToSolid(back);
        Assert.Equal(ManifoldError.NoError, s2.Status);
        CsgAssert.AssertNear(s2.Volume, 1.0, 1e-9, "объём после MeshData round-trip");
    }

    [Fact]
    public void Sphere_VolumeCloseToAnalytic()
    {
        // 16 128 треугольников (rings=64, segments=128).
        HalfEdgeMesh sphere = TestMeshes.Sphere(1.0, rings: 64, segments: 128);
        using ManifoldSolid s = ManifoldBridge.ToSolid(sphere);
        Assert.Equal(ManifoldError.NoError, s.Status);
        Assert.Equal(16128, s.TriangleCount);
        Assert.Equal(0, s.Genus);

        double analytic = TestMeshes.AnalyticSphereVolume(1.0);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Assert.True(relErr < 0.02, $"объём {s.Volume:F4} vs аналитический {analytic:F4}, ошибка {relErr:P3}");

        MeshMetrics m = CsgAssert.AssertValidResult(s.ToHalfEdgeMesh(), "sphere round-trip");
        Assert.Equal(0, m.Genus);
    }

    [Fact]
    public void Torus_GenusOne_EulerZero()
    {
        // Тор R=2, r=0.5: 4096 треугольников, объём 2π²Rr² = π².
        HalfEdgeMesh torus = TestMeshes.Torus(2.0, 0.5, majorSegments: 64, minorSegments: 32);
        using ManifoldSolid s = ManifoldBridge.ToSolid(torus);
        Assert.Equal(ManifoldError.NoError, s.Status);

        double analytic = TestMeshes.AnalyticTorusVolume(2.0, 0.5);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Assert.True(relErr < 0.02, $"объём {s.Volume:F4} vs аналитический {analytic:F4}, ошибка {relErr:P3}");

        MeshMetrics m = CsgAssert.AssertValidResult(s.ToHalfEdgeMesh(), "torus round-trip");
        Assert.Equal(1, m.Genus);
        Assert.Equal(0, m.EulerCharacteristic);
    }

    [Fact]
    public void EmptyHalfEdgeMesh_RoundTripsToEmpty()
    {
        using ManifoldSolid s = ManifoldBridge.ToSolid(new HalfEdgeMesh());
        Assert.True(s.IsEmpty);
        Assert.Equal(ManifoldError.NoError, s.Status);

        HalfEdgeMesh back = s.ToHalfEdgeMesh();
        Assert.Empty(back.Faces);
        Assert.Empty(back.Vertices);
    }

    [Fact]
    public void InvalidOpenMesh_ReportsNotManifoldWithoutCrash()
    {
        // Куб без одной грани (10 треугольников, граница открыта): диагностика C API, не падение.
        MeshData cubeSoup = TestMeshes.Cube(1, 1, 1).ToTriangles();
        var openIndices = new List<int>(cubeSoup.Indices.Take(cubeSoup.Indices.Count - 6));
        HalfEdgeMesh open = HalfEdgeMesh.FromTriangles(cubeSoup.Positions, openIndices);

        using ManifoldSolid solid = ManifoldBridge.ToSolid(open);
        Assert.Equal(ManifoldError.NotManifold, solid.Status);
    }

    [Fact]
    public void InvalidInput_CsgOperationsThrowsWithStatus()
    {
        MeshData cubeSoup = TestMeshes.Cube(1, 1, 1).ToTriangles();
        var openIndices = new List<int>(cubeSoup.Indices.Take(cubeSoup.Indices.Count - 6));
        HalfEdgeMesh open = HalfEdgeMesh.FromTriangles(cubeSoup.Positions, openIndices);
        HalfEdgeMesh cube = TestMeshes.Cube(1, 1, 1);

        ManifoldException ex = Assert.Throws<ManifoldException>(() => CsgOperations.Union(open, cube));
        Assert.Equal(ManifoldError.NotManifold, ex.Error);

        ManifoldException ex2 = Assert.Throws<ManifoldException>(() => CsgOperations.Difference(cube, open));
        Assert.Equal(ManifoldError.NotManifold, ex2.Error);
    }

    [Fact]
    public void OutOfRangeIndex_IsRejectedInManagedValidation()
    {
        var soup = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)],
            [0, 1, 2, 0, 1, 999]);

        Assert.Throws<ArgumentException>(() => ManifoldBridge.ToSolid(soup));
    }

    [Fact]
    public void SdfCallback_FromManagedCode_ProducesValidSphere()
    {
        // Конвенция Manifold::LevelSet: ПОЛОЖИТЕЛЬНОЕ значение SDF — внутри тела.
        long calls = 0;
        using ManifoldSolid s = ManifoldSolid.FromSdf(
            (x, y, z) =>
            {
                Interlocked.Increment(ref calls);
                return 1.0 - Math.Sqrt((x * x) + (y * y) + (z * z));
            },
            min: (-1.3, -1.3, -1.3),
            max: (1.3, 1.3, 1.3),
            edgeLength: 0.08);

        Assert.True(calls > 1000, $"колбэк должен вызываться из нативного кода, вызовов={calls}");
        Assert.Equal(ManifoldError.NoError, s.Status);

        double analytic = TestMeshes.AnalyticSphereVolume(1.0);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Assert.True(relErr < 0.05, $"объём {s.Volume:F4} vs аналитический {analytic:F4}, ошибка {relErr:P3}");

        MeshMetrics m = CsgAssert.AssertValidResult(s.ToHalfEdgeMesh(), "SDF-сфера");
        Assert.Equal(0, m.Genus);
    }
}
