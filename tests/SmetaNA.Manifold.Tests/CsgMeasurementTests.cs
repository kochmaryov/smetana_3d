using System.Diagnostics;
using SmetaNA.Core.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Замеры конвейера CSG (для отчёта и регрессий производительности): ingest, построение
/// дерева, материализация, readback, сборка HalfEdgeMesh. Утверждения — только на
/// корректность; на время не смотрим (CI-стабильность), цифры пишутся в вывод теста.
/// </summary>
public class CsgMeasurementTests
{
    private readonly ITestOutputHelper _output;

    public CsgMeasurementTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Measure_IngestMaterializeReadback_OnSpheres()
    {
        HalfEdgeMesh s1 = TestMeshes.Sphere(1.0, rings: 112, segments: 224);   // 49 728 треуг.
        HalfEdgeMesh s2 = MeshTransforms.Transformed(
            TestMeshes.Sphere(1.0, rings: 112, segments: 224),
            Transform3d.CreateTranslation(new Vector3d(1.2, 0, 0)));
        _output.WriteLine($"Вход: сфера {s1.Faces.Count:N0} треуг. / {s1.Vertices.Count:N0} верш. каждая");

        // Ingest: HalfEdgeMesh → Manifold (с конвертацией в треугольный суп).
        double ingestMs = TimeMin(3, () =>
        {
            using ManifoldSolid a = ManifoldBridge.ToSolid(s1);
            using ManifoldSolid b = ManifoldBridge.ToSolid(s2);
            GC.KeepAlive(a);
            GC.KeepAlive(b);
        });

        using ManifoldSolid sa = ManifoldBridge.ToSolid(s1);
        using ManifoldSolid sb = ManifoldBridge.ToSolid(s2);

        // Построение дерева — отложенное (≈ 0 мс), материализация — явная.
        double treeMs = TimeMin(5, () =>
        {
            using ManifoldSolid r = sa.Union(sb);
            GC.KeepAlive(r);
        });

        double materializeMs = TimeMin(3, () =>
        {
            using ManifoldSolid r = sa.Union(sb);
            r.Materialize();
        });

        using ManifoldSolid result = sa.Union(sb);
        result.Materialize();
        int outTris = (int)result.TriangleCount;

        double readbackMs = TimeMin(3, () =>
        {
            MeshData m = result.ToMeshData();
            GC.KeepAlive(m);
        });

        double rebuildMs = TimeMin(3, () =>
        {
            HalfEdgeMesh m = result.ToHalfEdgeMesh();
            GC.KeepAlive(m);
        });

        _output.WriteLine("| Сценарий | Вход, треуг. | Выход, треуг. | Ingest, мс | CSG-дерево, мс | Материализация, мс | Readback, мс | HalfEdgeMesh, мс |");
        _output.WriteLine("|---|---|---|---|---|---|---|---|");
        _output.WriteLine(
            $"| union двух сфер ~100k | {s1.Faces.Count + s2.Faces.Count:N0} | {outTris:N0} | {ingestMs:F2} | {treeMs:F2} | {materializeMs:F2} | {readbackMs:F2} | {rebuildMs:F2} |");

        // Контроль корректности (без утверждений на время).
        MeshMetrics m = CsgAssert.AssertValidResult(result.ToHalfEdgeMesh(), "замер: union сфер");
        Assert.True(m.Volume > 0);
    }

    [Fact]
    public void Measure_BooleanOps_OnLargeSpheres()
    {
        HalfEdgeMesh s1 = TestMeshes.Sphere(1.0, rings: 112, segments: 224);
        HalfEdgeMesh s2 = MeshTransforms.Transformed(
            TestMeshes.Sphere(1.0, rings: 112, segments: 224),
            Transform3d.CreateTranslation(new Vector3d(1.2, 0, 0)));

        double unionMs = TimeMin(3, () => GC.KeepAlive(CsgOperations.Union(s1, s2)));
        double diffMs = TimeMin(3, () => GC.KeepAlive(CsgOperations.Difference(s1, s2)));
        double interMs = TimeMin(3, () => GC.KeepAlive(CsgOperations.Intersection(s1, s2)));

        HalfEdgeMesh u = CsgOperations.Union(s1, s2);
        HalfEdgeMesh d = CsgOperations.Difference(s1, s2);
        HalfEdgeMesh x = CsgOperations.Intersection(s1, s2);

        _output.WriteLine("| Операция (полный конвейер HalfEdgeMesh→HalfEdgeMesh) | Выход, треуг. | Время, мс |");
        _output.WriteLine("|---|---|---|");
        _output.WriteLine($"| union двух сфер ~100k | {u.Faces.Count:N0} | {unionMs:F1} |");
        _output.WriteLine($"| difference двух сфер ~100k | {d.Faces.Count:N0} | {diffMs:F1} |");
        _output.WriteLine($"| intersection двух сфер ~100k | {x.Faces.Count:N0} | {interMs:F1} |");

        CsgAssert.AssertValidResult(u, "замер: union");
        CsgAssert.AssertValidResult(d, "замер: difference");
        CsgAssert.AssertValidResult(x, "замер: intersection");
    }

    private static double TimeMin(int iters, Action action)
    {
        action(); // прогрев JIT/нативных путей
        var sw = new Stopwatch();
        double min = double.MaxValue;
        for (int i = 0; i < iters; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            min = Math.Min(min, sw.Elapsed.TotalMilliseconds);
        }

        return min;
    }
}
