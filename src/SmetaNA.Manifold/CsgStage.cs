namespace SmetaNA.Manifold;

/// <summary>Стадия конвейера булевой операции (для прогресса и отмены).</summary>
public enum CsgStage
{
    /// <summary>Конвертация входных мешей в нативное представление (ingest).</summary>
    Ingest,

    /// <summary>Построение CSG-дерева (отложенный результат булевой операции, ≈ 0 мс).</summary>
    BuildTree,

    /// <summary>Материализация CSG-дерева — главная дорогая фаза.</summary>
    Materialize,

    /// <summary>Чтение результата в массивы (readback).</summary>
    Readback,

    /// <summary>Сборка HalfEdgeMesh из треугольного супа.</summary>
    RebuildMesh,
}

/// <summary>
/// Прогресс конвейера булевой операции: стадия и доля выполнения (0..1).
/// <see cref="Fraction"/> — доля типового конвейера (Ingest → BuildTree → Materialize →
/// Readback → RebuildMesh), пройденная к началу стадии; стадии, не выполняемые в данном
/// вызове, не репортятся.
/// </summary>
public readonly record struct CsgProgress(CsgStage Stage, double Fraction)
{
    /// <summary>Каноническая точка прогресса для стадии.</summary>
    public static CsgProgress For(CsgStage stage) => new(stage, stage switch
    {
        CsgStage.Ingest => 0.0,
        CsgStage.BuildTree => 0.15,
        CsgStage.Materialize => 0.25,
        CsgStage.Readback => 0.8,
        CsgStage.RebuildMesh => 0.9,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    });
}
