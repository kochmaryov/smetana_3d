using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Spike.Manifold;

/// <summary>
/// Spike 0.4: проверка риска «Manifold C API из C#» (фаза 0, план 3d-editor.plan.md).
/// Критерий зелёного: CSG через Manifold работает из C# (создание → булевы → чтение → валидность).
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Spike 0.4 — Manifold C API из C# (MANIFOLD_CBIND) ===");
        Console.WriteLine($"Manifold:       3.5.4 (сборка из исходников, MSVC x64, MANIFOLD_CBIND=ON, MANIFOLD_PAR=OFF)");
        Console.WriteLine($".NET:           {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"ОС:             {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Процессор:      {Environment.ProcessorCount} логических ядер");
        Console.WriteLine();

        Console.WriteLine("--- Корректность ---");
        T1_CubeRoundTrip();
        T2_SphereVolume();
        T3_TorusGenus();
        T4_UnionDisjointCubes();
        T5_DifferenceCubes();
        T6_IntersectionCubes();
        T7_UnionCubeSphere();
        T8_SdfCallback();
        T9_FloatVsDoubleIngest();
        T10_InvalidMeshErrorPropagation();

        Console.WriteLine();
        Console.WriteLine("--- Производительность (минимум из 5 прогонов после прогрева) ---");
        RunBenchmarks();

        Console.WriteLine();
        Console.WriteLine($"Итог: пройдено {_passed}, провалено {_failed}");
        if (_failed == 0)
        {
            Console.WriteLine("ЗЕЛЁНЫЙ: CSG через Manifold работает из C#.");
            return 0;
        }

        Console.WriteLine("КРАСНЫЙ: есть проваленные проверки.");
        return 1;
    }

    // ===================== корректность =====================

    private static void T1_CubeRoundTrip()
    {
        using Solid s = Solid.FromMesh(TestMeshes.Cube(1, 1, 1));
        Check("T1.1 куб из массивов: валиден", s.IsValid, $"status={s.Status}");
        Check("T1.2 куб: 12 треугольников", s.TriangleCount == 12, $"tris={s.TriangleCount}");
        Check("T1.3 куб: объём = 1.0", Near(s.Volume, 1.0, 1e-9), $"volume={s.Volume:G17}");
        Check("T1.4 куб: род = 0", s.Genus == 0, $"genus={s.Genus}");
        Check("T1.5 куб: эйлерова характеристика замкнута (V-E+F=2)",
            s.VertexCount - s.EdgeCount + s.TriangleCount == 2,
            $"V={s.VertexCount} E={s.EdgeCount} F={s.TriangleCount}");

        MeshData back = s.ToMesh();
        Check("T1.6 round-trip: треугольники сохранены", back.TriangleCount == 12, $"tris={back.TriangleCount}");
        using Solid s2 = Solid.FromMesh(back);
        Check("T1.7 round-trip: объём сохранён", Near(s2.Volume, 1.0, 1e-9), $"volume={s2.Volume:G17}");
    }

    private static void T2_SphereVolume()
    {
        // 16128 треугольников (rings=64, segments=128).
        using Solid s = Solid.FromMesh(TestMeshes.Sphere(1.0, rings: 64, segments: 128));
        double analytic = TestMeshes.AnalyticSphereVolume(1.0);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Check("T2.1 сфера: валидна", s.IsValid, $"status={s.Status}");
        Check("T2.2 сфера: 16128 треугольников", s.TriangleCount == 16128, $"tris={s.TriangleCount}");
        Check("T2.3 сфера: объём близок к аналитическому (<2%)", relErr < 0.02,
            $"volume={s.Volume:F4}, analytic={analytic:F4}, err={relErr:P3}");
        Check("T2.4 сфера: род = 0", s.Genus == 0, $"genus={s.Genus}");
    }

    private static void T3_TorusGenus()
    {
        // Тор («донат») R=2, r=0.5: 4096 треугольников, объём 2π²Rr² = π².
        using Solid s = Solid.FromMesh(TestMeshes.Torus(2.0, 0.5, majorSegments: 64, minorSegments: 32));
        double analytic = TestMeshes.AnalyticTorusVolume(2.0, 0.5);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Check("T3.1 тор: валиден", s.IsValid, $"status={s.Status}");
        Check("T3.2 тор: объём близок к аналитическому (<2%)", relErr < 0.02,
            $"volume={s.Volume:F4}, analytic={analytic:F4}, err={relErr:P3}");
        Check("T3.3 тор: род поверхности = 1 (дырка в «донате»)", s.Genus == 1, $"genus={s.Genus}");
        Check("T3.4 тор: эйлерова характеристика = 0 (V-E+F=2-2g)",
            s.VertexCount - s.EdgeCount + s.TriangleCount == 0,
            $"V={s.VertexCount} E={s.EdgeCount} F={s.TriangleCount}");
    }

    private static void T4_UnionDisjointCubes()
    {
        using Solid a = Solid.FromMesh(TestMeshes.Cube(1, 1, 1));
        using Solid b = Solid.FromMesh(TestMeshes.Cube(1, 1, 1)).Translate(3, 0, 0);
        using Solid u = a.Union(b);
        Check("T4.1 union непересекающихся кубов: валиден", u.IsValid, $"status={u.Status}");
        Check("T4.2 union: объём = 2.0 (точно)", Near(u.Volume, 2.0, 1e-9), $"volume={u.Volume:G17}");
        Check("T4.3 union: 24 треугольника", u.TriangleCount == 24, $"tris={u.TriangleCount}");
    }

    private static void T5_DifferenceCubes()
    {
        // A = куб 2x2x2 (центрирован), B = куб 1x1x1 со сдвигом (1,0,0): A-B = 8 - 0.5 = 7.5.
        using Solid a = Solid.FromMesh(TestMeshes.Cube(2, 2, 2));
        using Solid b = Solid.FromMesh(TestMeshes.Cube(1, 1, 1)).Translate(1, 0, 0);
        using Solid d = a.Difference(b);
        Check("T5.1 difference кубов: валиден", d.IsValid, $"status={d.Status}");
        Check("T5.2 difference: объём = 7.5 (точно)", Near(d.Volume, 7.5, 1e-9), $"volume={d.Volume:G17}");
    }

    private static void T6_IntersectionCubes()
    {
        using Solid a = Solid.FromMesh(TestMeshes.Cube(2, 2, 2));
        using Solid b = Solid.FromMesh(TestMeshes.Cube(1, 1, 1)).Translate(1, 0, 0);
        using Solid i = a.Intersection(b);
        Check("T6.1 intersection кубов: валиден", i.IsValid, $"status={i.Status}");
        Check("T6.2 intersection: объём = 0.5 (точно)", Near(i.Volume, 0.5, 1e-9), $"volume={i.Volume:G17}");
    }

    private static void T7_UnionCubeSphere()
    {
        // Сфера r=1.2 с центром на грани куба 2x2x2: частичное пересечение с кривой CSG.
        using Solid cube = Solid.FromMesh(TestMeshes.Cube(2, 2, 2));
        using Solid sphere = Solid.FromMesh(TestMeshes.Sphere(1.2, rings: 48, segments: 96)).Translate(1, 0, 0);
        using Solid u = cube.Union(sphere);
        double sphereVol = TestMeshes.AnalyticSphereVolume(1.2);
        Check("T7.1 union куб+сфера: валиден (замкнутая поверхность)", u.IsValid, $"status={u.Status}");
        Check("T7.2 union: объём в ожидаемых границах (8, 8+Vсферы)",
            u.Volume > 8.0 && u.Volume < 8.0 + sphereVol,
            $"volume={u.Volume:F4}, bounds=(8, {8.0 + sphereVol:F4})");
        Check("T7.3 union: род = 0 (результат гомеоморфен шару)", u.Genus == 0, $"genus={u.Genus}");
    }

    private static void T8_SdfCallback()
    {
        // Конвенция Manifold::LevelSet: ПОЛОЖИТЕЛЬНОЕ значение SDF — внутри тела
        // (в отличие от классической SDF-конвенции «отрицательное — внутри»).
        long calls = 0;
        using Solid s = Solid.FromSdf(
            (x, y, z) =>
            {
                Interlocked.Increment(ref calls);
                return 1.0 - Math.Sqrt(x * x + y * y + z * z); // единичная сфера
            },
            min: (-1.3, -1.3, -1.3),
            max: (1.3, 1.3, 1.3),
            edgeLength: 0.08);
        double analytic = TestMeshes.AnalyticSphereVolume(1.0);
        double relErr = Math.Abs(s.Volume - analytic) / analytic;
        Check("T8.1 SDF-колбэк: вызван из нативного кода", calls > 1000, $"calls={calls}");
        Check("T8.2 SDF-колбэк: результат валиден", s.IsValid, $"status={s.Status}");
        Check("T8.3 SDF-колбэк: объём сферы близок к аналитическому (<5%)", relErr < 0.05,
            $"volume={s.Volume:F4}, analytic={analytic:F4}, err={relErr:P3}");
    }

    private static void T9_FloatVsDoubleIngest()
    {
        MeshData cube = TestMeshes.Cube(1, 1, 1);
        using Solid viaDouble = Solid.FromMesh(cube).Translate(3, 0, 0);
        using Solid viaFloat = Solid.FromMeshFloat(cube);
        using Solid u = viaDouble.Union(viaFloat);
        Check("T9.1 float+double ingest: union валиден", u.IsValid, $"status={u.Status}");
        Check("T9.2 float+double ingest: объём = 2.0 (точно)", Near(u.Volume, 2.0, 1e-9), $"volume={u.Volume:G17}");

        MeshData backFloat = viaFloat.ToMeshFloat();
        using Solid r = Solid.FromMesh(backFloat);
        Check("T9.3 float round-trip: объём куба сохранён", Near(r.Volume, 1.0, 1e-6), $"volume={r.Volume:G17}");
    }

    private static void T10_InvalidMeshErrorPropagation()
    {
        // Невалидный меш передаётся мимо managed-валидации, чтобы проверить нативную диагностику.
        double[] verts = [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1];
        ulong[] badTris = [0, 1, 2, 0, 1, 999]; // индекс 999 вне диапазона
        unsafe
        {
            fixed (double* vp = verts)
            fixed (ulong* tv = badTris)
            {
                IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl64();
                IntPtr mesh = ManifoldNative.manifold_meshgl64(meshMem, vp, 4, 3, tv, 2);
                IntPtr solidMem = ManifoldNative.manifold_alloc_manifold();
                IntPtr solid = ManifoldNative.manifold_of_meshgl64(solidMem, mesh);
                ManifoldError status = ManifoldNative.manifold_status(solid);
                Check("T10.1 невалидный меш: ошибка из C API, а не падение", status != ManifoldError.NoError,
                    $"status={status}");
                ManifoldNative.manifold_delete_manifold(solid);
                ManifoldNative.manifold_delete_meshgl64(mesh);
            }
        }
    }

    // ===================== производительность =====================

    private sealed record BenchRow(string Name, int InTris, int OutTris, double IngestMs, double CsgMs, double ReadMs);

    private static void RunBenchmarks()
    {
        var rows = new List<BenchRow>();

        // Задержка маленькой операции (overhead P/Invoke + аллокации).
        {
            MeshData a = TestMeshes.Cube(1, 1, 1);
            MeshData b = TestMeshes.Cube(1, 1, 1);
            rows.Add(Measure("union двух кубов (24+24)", a, b, (x, y) => x.Union(y), offset: (3, 0, 0)));
        }

        // ~50k + ~50k треугольников → суммарно ~100k на входе.
        MeshData big1 = TestMeshes.Sphere(1.0, rings: 112, segments: 224);   // 49 728 треуг.
        MeshData big2 = TestMeshes.Sphere(1.0, rings: 112, segments: 224);
        Console.WriteLine($"Большие меши: сфера {big1.TriangleCount:N0} треуг. / {big1.VertexCount:N0} верш. каждая");

        rows.Add(Measure("union двух сфер ~100k", big1, big2, (x, y) => x.Union(y), offset: (1.2, 0, 0)));
        rows.Add(Measure("difference двух сфер ~100k", big1, big2, (x, y) => x.Difference(y), offset: (1.2, 0, 0)));
        rows.Add(Measure("intersection двух сфер ~100k", big1, big2, (x, y) => x.Intersection(y), offset: (1.2, 0, 0)));
        rows.Add(Measure("union сфер ~100k (без пересечения)", big1, big2, (x, y) => x.Union(y), offset: (5, 0, 0)));

        // Сравнение путей чтения: MeshGL64 (double) против MeshGL (float).
        {
            using Solid a = Solid.FromMesh(big1);
            using Solid b = Solid.FromMesh(big2).Translate(1.2, 0, 0);
            using Solid u = a.Union(b);
            double min64 = TimeMin(3, () => { MeshData m = u.ToMesh(); GC.KeepAlive(m); });
            double min32 = TimeMin(3, () => { MeshData m = u.ToMeshFloat(); GC.KeepAlive(m); });
            Console.WriteLine($"  readback union-сфер ({u.TriangleCount:N0} треуг.): MeshGL64 {min64:F2} мс, MeshGL(float) {min32:F2} мс");
        }

        Console.WriteLine();
        Console.WriteLine("| Сценарий | Вход, треуг. | Выход, треуг. | Ingest, мс | CSG (материализация), мс | Readback, мс |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (BenchRow r in rows)
            Console.WriteLine($"| {r.Name} | {r.InTris:N0} | {r.OutTris:N0} | {r.IngestMs:F2} | {r.CsgMs:F2} | {r.ReadMs:F2} |");
    }

    private static BenchRow Measure(
        string name,
        MeshData meshA,
        MeshData meshB,
        Func<Solid, Solid, Solid> op,
        (double X, double Y, double Z) offset)
    {
        const int iters = 5;

        // Ingest: mesh → Manifold (включая копии массивов в нативный код).
        double ingestMs = TimeMin(iters, () =>
        {
            using Solid a = Solid.FromMesh(meshA);
            using Solid b = Solid.FromMesh(meshB);
            GC.KeepAlive(a);
            GC.KeepAlive(b);
        });


        using Solid sa = Solid.FromMesh(meshA);
        using Solid sb = Solid.FromMesh(meshB).Translate(offset.X, offset.Y, offset.Z);

        // Булевы операции Manifold отложенные: op() лишь строит CSG-дерево (~0 мс),
        // реальная работа выполняется при первой eager-операции (здесь — Volume).
        double csgMs = TimeMin(iters, () =>
        {
            using Solid r = op(sa, sb);
            _ = r.Volume;
        });

        using Solid result = op(sa, sb);
        int outTris = (int)result.TriangleCount; // материализация + размер результата
        double readMs = TimeMin(iters, () => { MeshData m = result.ToMesh(); GC.KeepAlive(m); });

        return new BenchRow(name, meshA.TriangleCount + meshB.TriangleCount, outTris, ingestMs, csgMs, readMs);
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

    // ===================== утилиты =====================

    private static bool Near(double actual, double expected, double absTol) => Math.Abs(actual - expected) <= absTol;

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
            _passed++;
        else
            _failed++;
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name} — {detail}");
    }
}
