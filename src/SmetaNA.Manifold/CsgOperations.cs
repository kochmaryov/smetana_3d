using SmetaNA.Core.Geometry;

namespace SmetaNA.Manifold;

/// <summary>
/// Булевы операции CSG (API фазы 5): объединение/разность/пересечение мешей
/// <see cref="HalfEdgeMesh"/> через Manifold (задача 5.3, гарантия manifold-результатов — R2).
/// </summary>
/// <remarks>
/// Два уровня API:
/// <list type="bullet">
/// <item>высокоуровневый конвейер (<see cref="Union"/> и др.) — принимает и возвращает
/// <see cref="HalfEdgeMesh"/>, материализует результат сразу, поддерживает прогресс и отмену;</item>
/// <item>отложенный (<see cref="UnionDeferred"/> и др.) — строит только CSG-дерево
/// (≈ 0 мс), возвращает <see cref="ManifoldSolid"/> с <see cref="ManifoldSolid.IsMaterialized"/> = false;
/// материализация выполняется явно (<see cref="ManifoldSolid.Materialize"/>,
/// <see cref="ManifoldSolid.ToHalfEdgeMesh"/>) — так прогресс/отмена UI-слоя коррелируют
/// с реальной работой (spike README: «отложенные булевы»).</item>
/// </list>
/// Невалидные входы и результаты — <see cref="ManifoldException"/> со статусом C API;
/// пустой результат (например, разность совпадающих тел) — корректный пустой меш.
/// </remarks>
public static class CsgOperations
{
    /// <summary>Объединение (A ∪ B).</summary>
    public static HalfEdgeMesh Union(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => Boolean(a, b, BooleanOp.Union, progress, cancellationToken);

    /// <summary>Разность (A ∖ B).</summary>
    public static HalfEdgeMesh Difference(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => Boolean(a, b, BooleanOp.Difference, progress, cancellationToken);

    /// <summary>Пересечение (A ∩ B).</summary>
    public static HalfEdgeMesh Intersection(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => Boolean(a, b, BooleanOp.Intersection, progress, cancellationToken);

    /// <summary>Булева операция: полный конвейер ingest → CSG-дерево → материализация → readback → HalfEdgeMesh.</summary>
    public static HalfEdgeMesh Boolean(
        HalfEdgeMesh a, HalfEdgeMesh b, BooleanOp op,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using ManifoldSolid result = BooleanDeferred(a, b, op, progress, cancellationToken);
        return result.ToHalfEdgeMesh(progress, cancellationToken);
    }

    /// <summary>Отложенное объединение: строит CSG-дерево, без материализации.</summary>
    public static ManifoldSolid UnionDeferred(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => BooleanDeferred(a, b, BooleanOp.Union, progress, cancellationToken);

    /// <summary>Отложенная разность: строит CSG-дерево, без материализации.</summary>
    public static ManifoldSolid DifferenceDeferred(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => BooleanDeferred(a, b, BooleanOp.Difference, progress, cancellationToken);

    /// <summary>Отложенное пересечение: строит CSG-дерево, без материализации.</summary>
    public static ManifoldSolid IntersectionDeferred(
        HalfEdgeMesh a, HalfEdgeMesh b,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => BooleanDeferred(a, b, BooleanOp.Intersection, progress, cancellationToken);

    /// <summary>
    /// Отложенная булева операция: ingest входов и построение CSG-дерева (≈ 0 мс).
    /// Дерево удерживает данные входов (shared-владение нативной стороны), поэтому
    /// входные меши после вызова можно освобождать.
    /// </summary>
    /// <exception cref="ManifoldException">Входной меш невалиден (статус C API не NoError).</exception>
    public static ManifoldSolid BooleanDeferred(
        HalfEdgeMesh a, HalfEdgeMesh b, BooleanOp op,
        IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(CsgProgress.For(CsgStage.Ingest));

        using ManifoldSolid sa = ManifoldBridge.ToSolid(a);
        EnsureValidInput(sa, "первый");
        cancellationToken.ThrowIfCancellationRequested();

        using ManifoldSolid sb = ManifoldBridge.ToSolid(b);
        EnsureValidInput(sb, "второй");
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(CsgProgress.For(CsgStage.BuildTree));
        return sa.Boolean(sb, op);
    }

    private static void EnsureValidInput(ManifoldSolid solid, string which)
    {
        ManifoldError status = solid.Status;
        if (status != ManifoldError.NoError)
        {
            throw new ManifoldException(status, $"Входной меш ({which} операнд) невалиден: {status}.");
        }
    }
}
