namespace SmetaNA.Core.Geometry;

/// <summary>
/// Математика фигур рисования (задача 2.1): кольца прямоугольника, окружности и
/// многоугольника в плоскости построения, дуги через три точки и по хорде
/// (радиус/угол), тесселяция дуг. Всё в мировых координатах, чистые функции.
/// </summary>
public static class DrawingShapes
{
    /// <summary>Число сегментов окружности по умолчанию (как в SketchUp).</summary>
    public const int DefaultCircleSegments = 24;

    /// <summary>Минимальное число сегментов дуги.</summary>
    public const int MinArcSegments = 2;

    /// <summary>Максимальный шаг тесселяции дуги, радиан (24 сегмента на полный круг).</summary>
    public const double MaxArcStepRad = Math.PI / 12;

    /// <summary>Минимальное число сторон многоугольника.</summary>
    public const int MinPolygonSides = 3;

    /// <summary>Максимальное число сторон многоугольника.</summary>
    public const int MaxPolygonSides = 64;

    /// <summary>
    /// Кольцо прямоугольника (CCW по нормали плоскости) из двух противоположных
    /// углов с координатами в базисе плоскости.
    /// </summary>
    public static IReadOnlyList<Vector3d> RectRing(DrawingPlane plane, double u1, double v1, double u2, double v2)
    {
        if (Math.Abs(u2 - u1) <= 0 || Math.Abs(v2 - v1) <= 0)
        {
            throw new ArgumentException("Прямоугольник вырожден: сторона равна нулю.");
        }

        return
        [
            plane.PointAt(u1, v1),
            plane.PointAt(u2, v1),
            plane.PointAt(u2, v2),
            plane.PointAt(u1, v2),
        ];
    }

    /// <summary>Кольцо окружности: вершины точно на радиусе, CCW по нормали плоскости.</summary>
    public static IReadOnlyList<Vector3d> CircleRing(
        DrawingPlane plane,
        Vector3d center,
        double radius,
        int segments = DefaultCircleSegments)
    {
        ValidateRadius(radius);
        ValidateSides(segments);
        return PolygonRing(plane, center, radius, segments, 0);
    }

    /// <summary>
    /// Кольцо правильного многоугольника: радиус — до вершин (описанная окружность),
    /// <paramref name="rotationRad"/> — поворот первой вершины от оси U.
    /// </summary>
    public static IReadOnlyList<Vector3d> PolygonRing(
        DrawingPlane plane,
        Vector3d center,
        double radius,
        int sides,
        double rotationRad = 0)
    {
        ValidateRadius(radius);
        ValidateSides(sides);
        Vector3d origin = plane.Project(center);
        var ring = new Vector3d[sides];
        for (int i = 0; i < sides; i++)
        {
            double angle = rotationRad + (Math.Tau * i / sides);
            ring[i] = origin + (plane.U * Math.Cos(angle) + plane.V * Math.Sin(angle)) * radius;
        }

        return ring;
    }

    /// <summary>
    /// Дуга по трём точкам (начало, точка на дуге, конец): окружность через точки,
    /// тесселяция дуги, обязательно проходящей через среднюю точку.
    /// Вырожденное расположение (на одной прямой) — <c>null</c>.
    /// </summary>
    public static IReadOnlyList<Vector3d>? ArcThrough3Points(Vector3d start, Vector3d through, Vector3d end)
    {
        if (!TryCircumcircle(start, through, end, out Vector3d center, out Vector3d normal, out double radius))
        {
            return null;
        }

        return TessellateArc(center, normal, radius, start, through, end);
    }

    /// <summary>
    /// Дуга по хорде и радиусу: выпуклость в сторону <paramref name="toward"/>.
    /// Радиус меньше половины хорды — исключение (инструмент ограничивает значение).
    /// </summary>
    public static IReadOnlyList<Vector3d> ArcChordRadius(
        Vector3d start,
        Vector3d end,
        double radius,
        Vector3d toward,
        Vector3d fallbackNormal)
    {
        double chord = (end - start).Length();
        if (chord <= 0)
        {
            throw new ArgumentException("Хорда дуги вырождена: точки совпадают.");
        }

        if (!double.IsFinite(radius) || radius < chord * 0.5)
        {
            throw new ArgumentException("Радиус дуги не меньше половины хорды.", nameof(radius));
        }

        Vector3d chordVector = end - start;
        Vector3d normal = Vector3d.Cross(chordVector, toward - start);
        if (normal.LengthSquared() <= 0)
        {
            normal = Vector3d.Cross(chordVector, fallbackNormal);
        }

        if (normal.LengthSquared() <= 0)
        {
            Vector3d axis = Math.Abs(chordVector.X) <= Math.Abs(chordVector.Y)
                && Math.Abs(chordVector.X) <= Math.Abs(chordVector.Z)
                    ? Vector3d.UnitX
                    : Math.Abs(chordVector.Y) <= Math.Abs(chordVector.Z)
                        ? Vector3d.UnitY
                        : Vector3d.UnitZ;
            normal = Vector3d.Cross(chordVector, axis);
        }

        normal = normal.Normalized();
        Vector3d bisector = Vector3d.Cross(normal, chordVector).Normalized();
        Vector3d midpoint = (start + end) * 0.5;
        double height = Math.Sqrt(Math.Max(0, (radius * radius) - (chord * 0.5 * chord * 0.5)));
        double side = Vector3d.Dot(toward - midpoint, bisector) >= 0 ? 1 : -1;
        Vector3d bulge = midpoint + bisector * (side * (radius - height));
        return TessellateArc(midpoint - bisector * (side * height), normal, radius, start, bulge, end);
    }

    /// <summary>
    /// Дуга по хорде и центральному углу (градусы, (0; 180]): выпуклость в сторону
    /// <paramref name="toward"/>.
    /// </summary>
    public static IReadOnlyList<Vector3d> ArcChordAngle(
        Vector3d start,
        Vector3d end,
        double angleDeg,
        Vector3d toward,
        Vector3d fallbackNormal)
    {
        if (!double.IsFinite(angleDeg) || angleDeg <= 0 || angleDeg > 180)
        {
            throw new ArgumentException("Угол дуги должен быть в диапазоне (0; 180] градусов.", nameof(angleDeg));
        }

        double chord = (end - start).Length();
        if (chord <= 0)
        {
            throw new ArgumentException("Хорда дуги вырождена: точки совпадают.");
        }

        double radius = chord / (2.0 * Math.Sin(angleDeg * Math.PI / 360.0));
        return ArcChordRadius(start, end, radius, toward, fallbackNormal);
    }

    /// <summary>Окружность через три точки; вырожденное расположение — <c>false</c>.</summary>
    public static bool TryCircumcircle(
        Vector3d a,
        Vector3d b,
        Vector3d c,
        out Vector3d center,
        out Vector3d normal,
        out double radius)
    {
        center = default;
        normal = default;
        radius = 0;

        Vector3d u = b - a;
        Vector3d v = c - a;
        Vector3d n = Vector3d.Cross(u, v);
        double uLength = u.Length();
        double vLength = v.Length();
        double nLength = n.Length();
        if (uLength <= 0 || vLength <= 0 || nLength <= 1e-12 * uLength * vLength)
        {
            return false;
        }

        double uSquared = u.LengthSquared();
        double vSquared = v.LengthSquared();
        double denominator = 2.0 * n.LengthSquared();
        center = a
            + (Vector3d.Cross(v, n) * uSquared + Vector3d.Cross(n, u) * vSquared) * (1.0 / denominator);
        normal = n * (1.0 / nLength);
        radius = (center - a).Length();
        return radius > 0;
    }

    /// <summary>
    /// Планарность контура (для замыкания линии/штриха гранью): все точки
    /// лежат в плоскости первых трёх неколлинеарных точек с допуском, мм.
    /// </summary>
    public static bool IsPlanar(IReadOnlyList<Vector3d> points, double tolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return true;
        }

        Vector3d normal = default;
        Vector3d origin = points[0];
        for (int i = 1; i + 1 < points.Count && normal.LengthSquared() <= 0; i++)
        {
            for (int j = i + 1; j < points.Count && normal.LengthSquared() <= 0; j++)
            {
                normal = Vector3d.Cross(points[i] - origin, points[j] - origin);
            }
        }

        if (normal.LengthSquared() <= 0)
        {
            return false;
        }

        normal = normal.Normalized();
        foreach (Vector3d point in points)
        {
            if (Math.Abs(Vector3d.Dot(point - origin, normal)) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Угол точки относительно центра в базисе (U, V) плоскости дуги.</summary>
    private static double AngleOf(Vector3d point, Vector3d center, Vector3d u, Vector3d v)
        => Math.Atan2(Vector3d.Dot(point - center, v), Vector3d.Dot(point - center, u));

    /// <summary>Свёртка угла в (-π; π].</summary>
    private static double WrapPi(double angle)
    {
        double wrapped = angle % Math.Tau;
        if (wrapped > Math.PI)
        {
            wrapped -= Math.Tau;
        }
        else if (wrapped <= -Math.PI)
        {
            wrapped += Math.Tau;
        }

        return wrapped;
    }

    /// <summary>
    /// Точки дуги от <paramref name="start"/> до <paramref name="end"/>, обязательно
    /// через <paramref name="through"/> (включая концы), вокруг центра/нормали.
    /// </summary>
    private static IReadOnlyList<Vector3d> TessellateArc(
        Vector3d center,
        Vector3d normal,
        double radius,
        Vector3d start,
        Vector3d through,
        Vector3d end)
    {
        Vector3d u = (start - center).Normalized();
        Vector3d v = Vector3d.Cross(normal, u).Normalized();
        double startAngle = AngleOf(start, center, u, v);
        double throughAngle = WrapPi(AngleOf(through, center, u, v) - startAngle);
        double endAngle = WrapPi(AngleOf(end, center, u, v) - startAngle);

        double sweep;
        if (throughAngle > 0)
        {
            sweep = endAngle <= 0 ? endAngle + Math.Tau : endAngle;
            if (sweep <= throughAngle)
            {
                sweep += Math.Tau;
            }
        }
        else if (throughAngle < 0)
        {
            sweep = endAngle >= 0 ? endAngle - Math.Tau : endAngle;
            if (sweep >= throughAngle)
            {
                sweep -= Math.Tau;
            }
        }
        else
        {
            sweep = endAngle == 0 ? Math.Tau : endAngle;
        }

        int segments = Math.Max(
            MinArcSegments,
            (int)Math.Ceiling((Math.Abs(sweep) / MaxArcStepRad) - 1e-9));
        var points = new Vector3d[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double angle = startAngle + (sweep * i / segments);
            points[i] = center + (u * Math.Cos(angle) + v * Math.Sin(angle)) * radius;
        }

        points[0] = start;
        points[^1] = end;
        return points;
    }

    private static void ValidateRadius(double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentException("Радиус должен быть положительным конечным числом.", nameof(radius));
        }
    }

    private static void ValidateSides(int sides)
    {
        if (sides < MinPolygonSides)
        {
            throw new ArgumentException("Фигуре нужно минимум 3 стороны.", nameof(sides));
        }
    }
}
