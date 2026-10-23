using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Фузи-тесты CSG (митигация риска R2): случайные конфигурации с фиксированным seed
/// и инварианты каждого результата — manifold_status, watertight, отсутствие дефектов,
/// V−E+F = 2−2·genus, объёмные соотношения (включение-исключение).
/// </summary>
public class FuzzyCsgTests
{
    private const double VolumeTol = 1e-9;

    private static HalfEdgeMesh Moved(HalfEdgeMesh mesh, double x, double y, double z)
        => MeshTransforms.Transformed(mesh, Transform3d.CreateTranslation(new Vector3d(x, y, z)));

    [Fact]
    public void Fuzzy_CubePairs_AllOps_SatisfyInclusionExclusion()
    {
        var rng = new Random(20240611);
        for (int i = 0; i < 24; i++)
        {
            double ax = (rng.NextDouble() * 1.5) + 0.5;
            double ay = (rng.NextDouble() * 1.5) + 0.5;
            double az = (rng.NextDouble() * 1.5) + 0.5;
            double bx = (rng.NextDouble() * 1.5) + 0.5;
            double by = (rng.NextDouble() * 1.5) + 0.5;
            double bz = (rng.NextDouble() * 1.5) + 0.5;
            double ox = (rng.NextDouble() * 4.0) - 2.0;
            double oy = (rng.NextDouble() * 4.0) - 2.0;
            double oz = (rng.NextDouble() * 4.0) - 2.0;

            HalfEdgeMesh a = TestMeshes.Cube(ax, ay, az);
            HalfEdgeMesh b = Moved(TestMeshes.Cube(bx, by, bz), ox, oy, oz);
            double va = ax * ay * az;
            double vb = bx * by * bz;

            HalfEdgeMesh u = CsgOperations.Union(a, b);
            HalfEdgeMesh d = CsgOperations.Difference(a, b);
            HalfEdgeMesh x = CsgOperations.Intersection(a, b);

            double vU = VolumeOrZero(u);
            double vD = VolumeOrZero(d);
            double vX = VolumeOrZero(x);

            // Включение-исключение для произвольных измеримых объёмов.
            CsgAssert.AssertNear(vU + vX, va + vb, VolumeTol, $"[{i}] V(A∪B)+V(A∩B)=V(A)+V(B)");
            CsgAssert.AssertNear(vD + vX, va, VolumeTol, $"[{i}] V(A∖B)+V(A∩B)=V(A)");
            Assert.True(vU + VolumeTol >= Math.Max(va, vb), $"[{i}] V(A∪B) >= max(V(A), V(B))");
            Assert.True(vX <= Math.Min(va, vb) + VolumeTol, $"[{i}] V(A∩B) <= min(V(A), V(B))");
            Assert.True(vD >= -VolumeTol, $"[{i}] V(A∖B) >= 0");

            if (u.Faces.Count > 0)
            {
                MeshMetrics mu = CsgAssert.AssertValidResult(u, $"[{i}] union");
                if (mu.ComponentCount == 1)
                {
                    // Объединение двух выпуклых тел по выпуклому пересечению гомеоморфно шару.
                    Assert.Equal(0, mu.Genus);
                }

                Assert.True(mu.ComponentCount <= 2, $"[{i}] union: не более двух компонент");
            }

            if (d.Faces.Count > 0)
            {
                CsgAssert.AssertValidResult(d, $"[{i}] difference");
            }

            if (x.Faces.Count > 0)
            {
                CsgAssert.AssertValidResult(x, $"[{i}] intersection");
            }
        }
    }

    [Fact]
    public void Fuzzy_PerturbedCubes_ResultsStayValid()
    {
        // Возмущение вершин ±1e-6: проверка устойчивости к «разъехавшимся» граням (R2).
        var rng = new Random(987654321);
        for (int i = 0; i < 12; i++)
        {
            double sizeA = (rng.NextDouble() * 1.0) + 0.8;
            HalfEdgeMesh a = PerturbedCube(rng, sizeA, sizeA, sizeA, 1e-6);
            double sizeB = (rng.NextDouble() * 1.0) + 0.8;
            HalfEdgeMesh b = Moved(
                PerturbedCube(rng, sizeB, sizeB, sizeB, 1e-6),
                (rng.NextDouble() * 1.2) - 0.2,
                (rng.NextDouble() * 1.2) - 0.6,
                (rng.NextDouble() * 1.2) - 0.6);
            double va = CsgAssert.VolumeOf(a);
            double vb = CsgAssert.VolumeOf(b);

            HalfEdgeMesh u = CsgOperations.Union(a, b);
            HalfEdgeMesh d = CsgOperations.Difference(a, b);

            if (u.Faces.Count > 0)
            {
                CsgAssert.AssertValidResult(u, $"[{i}] perturbed union");
                double vU = CsgAssert.VolumeOf(u);
                Assert.True(vU + VolumeTol >= Math.Max(va, vb) - 1e-6, $"[{i}] V(A∪B) >= max(V(A), V(B))");
            }

            if (d.Faces.Count > 0)
            {
                CsgAssert.AssertValidResult(d, $"[{i}] perturbed difference");
                double vD = CsgAssert.VolumeOf(d);
                Assert.True(vD >= -1e-6, $"[{i}] V(A∖B) >= 0");
            }
        }
    }

    [Fact]
    public void Fuzzy_SphereCube_VaryingOffsets_StayValid()
    {
        var rng = new Random(271828182);
        HalfEdgeMesh sphere = TestMeshes.Sphere(1.0, rings: 24, segments: 48);
        double sphereVolume = CsgAssert.VolumeOf(sphere);
        for (int i = 0; i < 8; i++)
        {
            double offset = rng.NextDouble() * 2.0;
            HalfEdgeMesh cube = Moved(TestMeshes.Cube(1.2, 1.2, 1.2), offset, 0, 0);
            double cubeVolume = 1.2 * 1.2 * 1.2;

            HalfEdgeMesh u = CsgOperations.Union(sphere, cube);
            HalfEdgeMesh d = CsgOperations.Difference(sphere, cube);

            if (u.Faces.Count > 0)
            {
                MeshMetrics mu = CsgAssert.AssertValidResult(u, $"[{i}] sphere∪cube@{offset:F3}");
                if (mu.ComponentCount == 1)
                {
                    // Сфера и куб — выпуклые, пересечение выпукло: связное объединение гомеоморфно шару.
                    Assert.Equal(0, mu.Genus);
                }

                double vU = CsgAssert.VolumeOf(u);
                Assert.True(vU <= sphereVolume + cubeVolume + VolumeTol, $"[{i}] V(A∪B) <= V(A)+V(B)");
                Assert.True(vU + VolumeTol >= Math.Max(sphereVolume, cubeVolume), $"[{i}] V(A∪B) >= max(V(A), V(B))");
            }

            if (d.Faces.Count > 0)
            {
                CsgAssert.AssertValidResult(d, $"[{i}] sphere∖cube@{offset:F3}");
                double vD = CsgAssert.VolumeOf(d);
                Assert.True(vD <= sphereVolume + VolumeTol, $"[{i}] V(A∖B) <= V(A)");
            }
        }
    }

    private static double VolumeOrZero(HalfEdgeMesh mesh)
        => mesh.Faces.Count == 0 ? 0.0 : CsgAssert.VolumeOf(mesh);

    private static HalfEdgeMesh PerturbedCube(Random rng, double sizeX, double sizeY, double sizeZ, double amplitude)
    {
        HalfEdgeMesh cube = TestMeshes.Cube(sizeX, sizeY, sizeZ);
        MeshData soup = cube.ToTriangles();
        var positions = new Vector3d[soup.Positions.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3d p = soup.Positions[i];
            positions[i] = new Vector3d(
                p.X + ((rng.NextDouble() - 0.5) * 2.0 * amplitude),
                p.Y + ((rng.NextDouble() - 0.5) * 2.0 * amplitude),
                p.Z + ((rng.NextDouble() - 0.5) * 2.0 * amplitude));
        }

        return HalfEdgeMesh.FromTriangles(positions, soup.Indices);
    }
}
