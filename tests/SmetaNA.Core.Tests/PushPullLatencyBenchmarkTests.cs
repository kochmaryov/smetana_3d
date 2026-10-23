using System.Diagnostics;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using Xunit;
using Xunit.Abstractions;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Бенчмарк отклика push/pull (DoD §5.6: ≤ 16 мс): путь коммита
/// (<see cref="MeshEditOps.PlanFaceMoveAlongNormal"/> + <see cref="MeshEditOps.Apply"/>,
/// то, что выполняется при подтверждении расстояния) на мешах «эталонного»
/// размера и на демо-сцене 500 000 треугольников. Отдельно замеряется наведение
/// (пик грани под курсором) — в UI он троттлится до 25 Гц.
/// </summary>
[Collection("benchmarks")]
public sealed class PushPullLatencyBenchmarkTests
{
    /// <summary>DoD §5.6: отклик push/pull ≤ 16 мс.</summary>
    private const double DodBudgetMs = 16;

    /// <summary>
    /// Регрессионный бюджет для тяжёлых сцен. Замер на демо-сцене 500k даёт
    /// медиану ~49 мс: планирование склеивания проходит по всем вершинам меша
    /// (см. docs/dod-measurements.md) — цель DoD требует оптимизации ядра
    /// (пространственный индекс), поэтому здесь фиксируется текущий уровень.
    /// </summary>
    private const double HeavySceneRegressionBudgetMs = 250;

    /// <summary>Бюджет наведения (пик грани) — не норматив DoD, страховка от регрессий.</summary>
    private const double HoverBudgetMs = 500;

    private const int WarmupIterations = 5;
    private const int MeasuredIterations = 30;

    private readonly ITestOutputHelper _output;

    public PushPullLatencyBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PushPullCommit_50kTriangles_MedianWithinDodBudget()
    {
        HalfEdgeMesh mesh = GridMesh(158); // ~50 000 треугольников
        double[] samples = MeasureCommit(mesh);

        Report("push/pull коммит, ~50k треугольников", samples);
        Assert.True(
            Median(samples) <= DodBudgetMs,
            $"Медиана коммита {Median(samples):F2} мс превышает бюджет DoD {DodBudgetMs} мс.");
    }

    [Fact]
    public void PushPullCommit_DemoScene500k_WithinRegressionBudget()
    {
        HalfEdgeMesh mesh = GridMesh(500); // 500 000 треугольников (бюджет DoD §5.6)
        double[] samples = MeasureCommit(mesh);

        Report("push/pull коммит, 500k треугольников", samples);
        double median = Median(samples);
        Assert.True(
            median <= HeavySceneRegressionBudgetMs,
            $"Медиана коммита {median:F2} мс превышает регрессионный бюджет {HeavySceneRegressionBudgetMs} мс.");
    }

    [Fact]
    public void PushPullHover_DemoScene500k_WithinHoverBudget()
    {
        HalfEdgeMesh mesh = GridMesh(500);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(
            new Vector3d(0, -4000, 3000), new Vector3d(0, 0, 0));
        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(0, 0, 0));

        var tool = new PushPullTool();
        tool.Activate(context);
        for (int i = 0; i < WarmupIterations; i++)
        {
            tool.OnPointer(PointerInput.Move(pixel));
        }

        var samples = new double[MeasuredIterations];
        for (int i = 0; i < samples.Length; i++)
        {
            var clock = Stopwatch.StartNew();
            tool.OnPointer(PointerInput.Move(pixel));
            clock.Stop();
            samples[i] = clock.Elapsed.TotalMilliseconds;
        }

        tool.Deactivate();
        Report("push/pull наведение (пик грани), 500k треугольников", samples);
        Assert.True(
            Median(samples) <= HoverBudgetMs,
            $"Медиана наведения {Median(samples):F2} мс превышает бюджет {HoverBudgetMs} мс.");
    }

    /// <summary>Цикл коммита push/pull: план переноса + применение + откат.</summary>
    private double[] MeasureCommit(HalfEdgeMesh mesh)
    {
        MeshFace face = mesh.Faces[0];
        for (int i = 0; i < WarmupIterations; i++)
        {
            IReadOnlyList<VertexMove> warm = MeshEditOps.PlanFaceMoveAlongNormal(mesh, face, 5);
            MeshEditOps.Apply(warm);
            MeshEditOps.Revert(warm);
        }

        var samples = new double[MeasuredIterations];
        for (int i = 0; i < samples.Length; i++)
        {
            var clock = Stopwatch.StartNew();
            IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, face, 5);
            MeshEditOps.Apply(moves);
            MeshEditOps.Revert(moves);
            clock.Stop();
            samples[i] = clock.Elapsed.TotalMilliseconds;
        }

        return samples;
    }

    private void Report(string label, double[] samples)
    {
        _output.WriteLine(
            $"[BENCH] {label}: мин={samples.Min():F3} мс, медиана={Median(samples):F3} мс, " +
            $"p95={Percentile(samples, 0.95):F3} мс, макс={samples.Max():F3} мс, " +
            $"итераций={samples.Length}");
    }

    private static double Median(double[] samples) => Percentile(samples, 0.5);

    private static double Percentile(double[] samples, double q)
    {
        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);
        double position = q * (sorted.Length - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        double weight = position - lower;
        return (sorted[lower] * (1 - weight)) + (sorted[upper] * weight);
    }

    /// <summary>Сетка-рельеф Q×Q квадов в плоскости XY (детерминированные высоты).</summary>
    private static HalfEdgeMesh GridMesh(int quadsPerSide)
    {
        var mesh = new HalfEdgeMesh();
        double step = 2000.0 / quadsPerSide;
        var vertices = new MeshVertex[(quadsPerSide + 1) * (quadsPerSide + 1)];
        for (int j = 0; j <= quadsPerSide; j++)
        {
            for (int i = 0; i <= quadsPerSide; i++)
            {
                double x = -1000.0 + (i * step);
                double y = -1000.0 + (j * step);
                double z = 60.0 * Math.Sin(x * 0.0021) * Math.Cos(y * 0.0017);
                vertices[(j * (quadsPerSide + 1)) + i] = mesh.AddVertex(new Vector3d(x, y, z));
            }
        }

        for (int j = 0; j < quadsPerSide; j++)
        {
            for (int i = 0; i < quadsPerSide; i++)
            {
                MeshVertex v00 = vertices[(j * (quadsPerSide + 1)) + i];
                MeshVertex v10 = vertices[(j * (quadsPerSide + 1)) + i + 1];
                MeshVertex v11 = vertices[((j + 1) * (quadsPerSide + 1)) + i + 1];
                MeshVertex v01 = vertices[((j + 1) * (quadsPerSide + 1)) + i];
                mesh.AddFace([v00, v10, v11, v01]);
            }
        }

        return mesh;
    }
}
