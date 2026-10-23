using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Отложенная материализация булевых операций: явный контроль фазы вычисления,
/// прогресс по стадиям, отмена на границах стадий (спик README: «отложенные булевы»).
/// </summary>
public class DeferredBooleanTests
{
    private sealed class ProgressCollector : IProgress<CsgProgress>
    {
        public List<CsgProgress> Reports { get; } = [];

        public void Report(CsgProgress value) => Reports.Add(value);
    }

    private static HalfEdgeMesh Moved(HalfEdgeMesh mesh, double x, double y, double z)
        => MeshTransforms.Transformed(mesh, Transform3d.CreateTranslation(new Vector3d(x, y, z)));

    [Fact]
    public void UnionDeferred_StaysUnmaterialized_UntilExplicitMaterialize()
    {
        HalfEdgeMesh a = TestMeshes.Cube(1, 1, 1);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 3, 0, 0);

        using ManifoldSolid deferred = CsgOperations.UnionDeferred(a, b);
        Assert.False(deferred.IsMaterialized);

        deferred.Materialize();
        Assert.True(deferred.IsMaterialized);
        CsgAssert.AssertNear(deferred.Volume, 2.0, 1e-9, "объём после явной материализации");

        // Повторная материализация — no-op, результат кешируется нативно.
        deferred.Materialize();
        Assert.True(deferred.IsMaterialized);
        CsgAssert.AssertNear(deferred.Volume, 2.0, 1e-9, "объём стабилен после повторной материализации");
    }

    [Fact]
    public void Boolean_ReportsAllPipelineStages_InOrder()
    {
        HalfEdgeMesh a = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 1, 0, 0);
        var progress = new ProgressCollector();

        HalfEdgeMesh result = CsgOperations.Difference(a, b, progress);

        Assert.Equal(
            [CsgStage.Ingest, CsgStage.BuildTree, CsgStage.Materialize, CsgStage.Readback, CsgStage.RebuildMesh],
            progress.Reports.Select(r => r.Stage).ToArray());
        Assert.True(progress.Reports.Zip(progress.Reports.Skip(1)).All(p => p.First.Fraction <= p.Second.Fraction),
            "доли прогресса должны не убывать");
        Assert.False(result.Faces.Count == 0);
    }

    [Fact]
    public void DeferredToHalfEdgeMesh_ReportsMaterializationStages()
    {
        HalfEdgeMesh a = TestMeshes.Cube(1, 1, 1);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 3, 0, 0);
        using ManifoldSolid deferred = CsgOperations.UnionDeferred(a, b);
        var progress = new ProgressCollector();

        HalfEdgeMesh result = deferred.ToHalfEdgeMesh(progress);

        Assert.Equal(
            [CsgStage.Materialize, CsgStage.Readback, CsgStage.RebuildMesh],
            progress.Reports.Select(r => r.Stage).ToArray());
        Assert.Equal(24, result.Faces.Count);
    }

    [Fact]
    public void Materialize_ReportsMaterializeStage_OnlyWhenWorkPending()
    {
        HalfEdgeMesh a = TestMeshes.Cube(1, 1, 1);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 3, 0, 0);
        using ManifoldSolid deferred = CsgOperations.UnionDeferred(a, b);

        var first = new ProgressCollector();
        deferred.Materialize(first);
        var second = new ProgressCollector();
        deferred.Materialize(second);

        Assert.Equal([CsgStage.Materialize], first.Reports.Select(r => r.Stage).ToArray());
        Assert.Empty(second.Reports);
    }

    [Fact]
    public void CancelledToken_ThrowsBeforeNativeWork()
    {
        HalfEdgeMesh a = TestMeshes.Cube(1, 1, 1);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 3, 0, 0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => CsgOperations.Union(a, b, cancellationToken: cts.Token));

        using ManifoldSolid deferred = CsgOperations.UnionDeferred(a, b);
        Assert.ThrowsAny<OperationCanceledException>(() => deferred.Materialize(cancellationToken: cts.Token));
        Assert.False(deferred.IsMaterialized);
    }

    [Fact]
    public void ChainedBooleans_StayDeferredUntilFinalReadback()
    {
        // Дерево из нескольких булевых: всё вычисляется одной материализацией.
        HalfEdgeMesh a = TestMeshes.Cube(2, 2, 2);
        HalfEdgeMesh b = Moved(TestMeshes.Cube(1, 1, 1), 1, 0, 0);
        HalfEdgeMesh c = Moved(TestMeshes.Cube(1, 1, 1), -1, 0, 0);

        using ManifoldSolid ab = CsgOperations.DifferenceDeferred(a, b);
        using ManifoldSolid abc = ab.Difference(ManifoldBridge.ToSolid(c));
        Assert.False(abc.IsMaterialized);

        HalfEdgeMesh result = abc.ToHalfEdgeMesh();
        MeshMetrics m = CsgAssert.AssertValidResult(result, "цепочка булевых");
        CsgAssert.AssertNear(m.Volume, 8.0 - 0.5 - 0.5, 1e-9, "объём цепочки A∖B∖C");
    }
}
