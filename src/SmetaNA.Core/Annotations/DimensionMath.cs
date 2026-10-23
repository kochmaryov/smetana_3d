using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Annotations;

/// <summary>
/// Вычисления размеров в миллиметрах: точность ≤ 0,01 мм по DoD §5.4 (double).
/// </summary>
public static class DimensionMath
{
    /// <summary>Допуск точности размеров, мм (DoD §5.4).</summary>
    public const double ToleranceMm = 0.01;

    /// <summary>Минимальная осмысленная длина, мм (нулевой размер не создаётся).</summary>
    public const double MinLengthMm = 1e-6;

    /// <summary>Выровненная длина: расстояние между точками.</summary>
    public static double AlignedLength(Vector3d start, Vector3d end) => (end - start).Length();

    /// <summary>
    /// Линейная длина: модуль проекции отрезка на направление измерения;
    /// без направления — проекция на доминирующую ось отрезка.
    /// </summary>
    public static double LinearLength(Vector3d start, Vector3d end, Vector3d? direction)
    {
        Vector3d dir = DirectionOf(start, end, direction);
        return Math.Abs(Vector3d.Dot(end - start, dir));
    }

    /// <summary>Длина по виду размера.</summary>
    public static double MeasuredLength(DimensionKind kind, Vector3d start, Vector3d end, Vector3d? direction)
        => kind switch
        {
            DimensionKind.Linear => LinearLength(start, end, direction),
            DimensionKind.Aligned => AlignedLength(start, end),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    /// <summary>
    /// Фактическое направление измерения: заданное (нормализуется) или
    /// доминирующая ось отрезка; нулевой отрезок без направления — ось X.
    /// </summary>
    public static Vector3d DirectionOf(Vector3d start, Vector3d end, Vector3d? direction)
    {
        if (direction is { } explicitDirection && explicitDirection.LengthSquared() > 0)
        {
            return explicitDirection.Normalized();
        }

        Vector3d axis = PrincipalAxis(end - start);
        return axis.LengthSquared() > 0 ? axis : Vector3d.UnitX;
    }

    /// <summary>
    /// Конец измеряемого отрезка: для <see cref="DimensionKind.Linear"/> —
    /// проекция конца на направление измерения.
    /// </summary>
    public static Vector3d MeasuredEnd(DimensionKind kind, Vector3d start, Vector3d end, Vector3d? direction)
    {
        if (kind == DimensionKind.Aligned)
        {
            return end;
        }

        Vector3d dir = DirectionOf(start, end, direction);
        return start + dir * Vector3d.Dot(end - start, dir);
    }

    /// <summary>Доминирующая (по модулю компоненты) ось вектора, единичная; нулевой вектор — <see cref="Vector3d.Zero"/>.</summary>
    public static Vector3d PrincipalAxis(Vector3d vector)
    {
        double x = Math.Abs(vector.X);
        double y = Math.Abs(vector.Y);
        double z = Math.Abs(vector.Z);
        if (x <= 0 && y <= 0 && z <= 0)
        {
            return Vector3d.Zero;
        }

        if (x >= y && x >= z)
        {
            return Vector3d.UnitX;
        }

        return y >= z ? Vector3d.UnitY : Vector3d.UnitZ;
    }
}
