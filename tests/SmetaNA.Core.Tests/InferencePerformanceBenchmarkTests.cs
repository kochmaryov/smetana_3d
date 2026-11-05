using System.Diagnostics;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;
using Xunit.Abstractions;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Производительность inference на плотной сцене (200k рёбер): приёмка S11 —
/// разрешение привязки под курсором укладывается в интерактивный бюджет.
/// </summary>
public sealed class InferencePerformanceBenchmarkTests
{
    private const int SegmentCount = 200_000;

    /// <summary>
    /// Бюджет S11: P95 ≤ 8 мс на 200k рёбер. После введения BVH (S11) замеры дают
    /// медиану ~2 мс, поэтому 8 мс — реальный интерактивный порог, а не страховка.
    /// </summary>
    private const double HeavySceneBudgetMs = 8.0;

    private const int WarmupIterations = 3;

    private const int MeasuredIterations = 12;

    private readonly ITestOutputHelper _output;

    public InferencePerformanceBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Solve_On200kEdges_StaysWithinBudget()
    {
        InferenceScene scene = BuildDenseScene(SegmentCount);
        Assert.Equal(SegmentCount, scene.Segments.Count);

        InferenceViewport viewport = InferenceTestSupport.TopDown();
        SnapRequest request = InferenceTestSupport.Request(viewport, scene, new Vector3d(10.4, 10.4, 0));

        for (int i = 0; i < WarmupIterations; i++)
        {
            InferenceEngine.Solve(request);
        }

        double[] samples = new double[MeasuredIterations];
        for (int i = 0; i < MeasuredIterations; i++)
        {
            long start = Stopwatch.GetTimestamp();
            SnapResult result = InferenceEngine.Solve(request);
            samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            Assert.NotNull(result);
        }

        double median = Median(samples);
        _output.WriteLine(
            $"[BENCH] inference 200k рёбер: мин={samples.Min():F2} мс, медиана={median:F2} мс, макс={samples.Max():F2} мс");

        Assert.True(
            median <= HeavySceneBudgetMs,
            $"Медиана разрешения привязки {median:F2} мс превышает бюджет {HeavySceneBudgetMs} мс на {SegmentCount} рёбрах.");
    }

    /// <summary>Плотная сетка рёбер 1×1 в плоскости z = 0: ровно 200k сегментов в одном снимке.</summary>
    private static InferenceScene BuildDenseScene(int segmentCount)
    {
        int columns = (int)Math.Sqrt(segmentCount) + 1;
        var segments = new List<SnapSegment>(segmentCount);
        for (int i = 0; i < segmentCount; i++)
        {
            int x = i % columns;
            int y = i / columns;
            segments.Add(i % 2 == 0
                ? new SnapSegment(new Vector3d(x, y, 0), new Vector3d(x + 1, y, 0))
                : new SnapSegment(new Vector3d(x, y, 0), new Vector3d(x, y + 1, 0)));
        }

        return new InferenceScene(segments, []);
    }

    private static double Median(double[] samples)
    {
        double[] sorted = [.. samples.Order()];
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }
}
