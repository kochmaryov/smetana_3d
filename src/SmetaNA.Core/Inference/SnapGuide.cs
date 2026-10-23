using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>Вид направляющей: линейка или луч транспортира.</summary>
public enum SnapGuideKind
{
    /// <summary>Линейка: прямая через точку вдоль направления.</summary>
    Line,

    /// <summary>Транспортир: направляющая под заданным углом (рисуется UI иначе).</summary>
    Angle,
}

/// <summary>
/// Направляющая (задача 2.6): прямая через точку вдоль единичного направления.
/// Источник — линейка или транспортир; геометрически это бесконечная прямая.
/// </summary>
public sealed class SnapGuide
{
    public SnapGuide(Vector3d point, Vector3d direction, SnapGuideKind kind = SnapGuideKind.Line, Guid? id = null)
    {
        if (!InferenceMath.IsFinite(point))
        {
            throw new ArgumentException("Точка направляющей должна быть конечной.", nameof(point));
        }

        if (!InferenceMath.IsFinite(direction) || direction.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление направляющей должно быть ненулевым.", nameof(direction));
        }

        Id = id ?? Guid.NewGuid();
        Point = point;
        Direction = direction.Normalized();
        Kind = kind;
    }

    public Guid Id { get; }

    /// <summary>Точка, через которую проходит направляющая.</summary>
    public Vector3d Point { get; }

    /// <summary>Направление направляющей, единичное.</summary>
    public Vector3d Direction { get; }

    public SnapGuideKind Kind { get; }

    /// <summary>Ближайшая точка направляющей к заданной точке.</summary>
    public Vector3d ClosestPointTo(Vector3d point)
        => InferenceMath.ClosestPointOnLine(Point, Direction, point);

    /// <summary>Пересечение с другой направляющей; <c>false</c> — параллельны.</summary>
    public bool TryIntersect(SnapGuide other, double tolerance, out Vector3d point)
    {
        ArgumentNullException.ThrowIfNull(other);
        return InferenceMath.TryIntersectLines(Point, Direction, other.Point, other.Direction, tolerance, out point);
    }
}

/// <summary>
/// Коллекция направляющих сцены: добавление (линейка/транспортир), удаление,
/// очистка и запросы для привязок.
/// </summary>
public sealed class GuideSet
{
    private readonly List<SnapGuide> _guides = [];

    public IReadOnlyList<SnapGuide> Guides => _guides;

    public int Count => _guides.Count;

    /// <summary>Добавить направляющую линейки (прямая через точку вдоль направления).</summary>
    public SnapGuide AddLine(Vector3d point, Vector3d direction)
        => Add(point, direction, SnapGuideKind.Line);

    /// <summary>Добавить направляющую транспортира (прямая через центр под углом).</summary>
    public SnapGuide AddAngle(Vector3d center, Vector3d direction)
        => Add(center, direction, SnapGuideKind.Angle);

    /// <summary>Добавить направляющую произвольного вида.</summary>
    public SnapGuide Add(Vector3d point, Vector3d direction, SnapGuideKind kind)
    {
        var guide = new SnapGuide(point, direction, kind);
        _guides.Add(guide);
        return guide;
    }

    /// <summary>Удалить направляющую по идентификатору.</summary>
    public bool Remove(Guid id) => _guides.RemoveAll(g => g.Id == id) > 0;

    /// <summary>Очистить все направляющие.</summary>
    public void Clear() => _guides.Clear();

    public SnapGuide? Find(Guid id) => _guides.FirstOrDefault(g => g.Id == id);

    /// <summary>Точки пересечения пар направляющих (ближайший подход в пределах допуска).</summary>
    public IReadOnlyList<Vector3d> Intersections(double tolerance)
    {
        var points = new List<Vector3d>();
        for (int i = 0; i < _guides.Count; i++)
        {
            for (int j = i + 1; j < _guides.Count; j++)
            {
                if (_guides[i].TryIntersect(_guides[j], tolerance, out Vector3d point))
                {
                    points.Add(point);
                }
            }
        }

        return points;
    }
}
