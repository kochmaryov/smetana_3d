using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Построение тела выдавливания профиля по траектории (Follow Me, задача 5.2):
/// плоский замкнутый профиль «проводится» вдоль ломаной траектории (замкнутой
/// или незамкнутой) с безвинтовыми кадрами (rotation-minimizing frames), на
/// стыках — биссектрисные касательные. Незамкнутая траектория замыкается
/// торцевыми гранями (профиль в начале и в конце), замкнутая — стыкуется
/// без торцов (расхождение кадров скручивается равномерно).
/// <para>
/// Результат — новый замкнутый меш с наружными нормалями (обход граней выбран
/// так, чтобы ориентация профиля относительно направления обхода сохраняла
/// наружные нормали; при «обратном» ходе профиль реверсируется).
/// </para>
/// </summary>
public static class SweepBuilder
{
    /// <summary>Допуск совпадения точек (вырожденные сегменты), мм.</summary>
    public const double ToleranceMm = 1e-9;

    /// <summary>
    /// Построить тело выдавливания.
    /// </summary>
    /// <param name="profile">Замкнутый плоский профиль (кольцо точек, минимум 3).</param>
    /// <param name="profileNormal">Нормаль плоскости профиля (для ориентации обхода).</param>
    /// <param name="path">Траектория: точки ломаной (минимум 2; для замкнутой — минимум 3).</param>
    /// <param name="closed">Траектория замкнута (стыкуется без торцов).</param>
    public static HalfEdgeMesh Build(
        IReadOnlyList<Vector3d> profile,
        Vector3d profileNormal,
        IReadOnlyList<Vector3d> path,
        bool closed)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(path);
        IReadOnlyList<Vector3d> points = NormalizePath(path, closed);
        IReadOnlyList<Vector3d> ring = OrientProfile(profile, profileNormal, points, closed);

        IReadOnlyList<Vector3d> tangents = ComputeTangents(points, closed);
        IReadOnlyList<(Vector3d U, Vector3d W)> frames = ComputeFrames(ring, tangents, points, closed);

        var mesh = new HalfEdgeMesh();
        var rings = new List<MeshVertex[]>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            (Vector3d u, Vector3d w) = frames[i];
            var vertices = new MeshVertex[ring.Count];
            for (int j = 0; j < ring.Count; j++)
            {
                Vector3d offset = ring[j] - ring[0];
                vertices[j] = mesh.AddVertex(points[i] + (u * Vector3d.Dot(offset, frames[0].U)) + (w * Vector3d.Dot(offset, frames[0].W)));
            }

            rings.Add(vertices);
        }

        // боковые грани: сегменты траектории × рёбра профиля
        int segments = closed ? points.Count : points.Count - 1;
        for (int i = 0; i < segments; i++)
        {
            MeshVertex[] current = rings[i];
            MeshVertex[] next = rings[(i + 1) % points.Count];
            for (int j = 0; j < ring.Count; j++)
            {
                int jNext = (j + 1) % ring.Count;
                TryAddFace(mesh, [current[j], current[jNext], next[jNext], next[j]]);
            }
        }

        if (!closed)
        {
            // торцы: конец — кольцо «по ходу», начало — реверс
            var endCap = new List<MeshVertex>(ring.Count);
            for (int j = 0; j < ring.Count; j++)
            {
                endCap.Add(rings[^1][j]);
            }

            TryAddFace(mesh, endCap);

            var startCap = new List<MeshVertex>(ring.Count);
            for (int j = ring.Count - 1; j >= 0; j--)
            {
                startCap.Add(rings[0][j]);
            }

            TryAddFace(mesh, startCap);
        }

        return mesh;
    }

    /// <summary>Точки траектории без вырожденных сегментов и дубля замыкания.</summary>
    private static IReadOnlyList<Vector3d> NormalizePath(IReadOnlyList<Vector3d> path, bool closed)
    {
        int minimum = closed ? 3 : 2;
        if (path.Count < minimum)
        {
            throw new ArgumentException($"Траектория требует минимум {minimum} точек.", nameof(path));
        }

        var points = new List<Vector3d>(path.Count);
        foreach (Vector3d point in path)
        {
            if (!IsFinite(point))
            {
                throw new ArgumentException("Траектория содержит нечисловые координаты.", nameof(path));
            }

            if (points.Count == 0 || (point - points[^1]).Length() > ToleranceMm)
            {
                points.Add(point);
            }
        }

        if (closed && points.Count > 1 && (points[0] - points[^1]).Length() <= ToleranceMm)
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < minimum)
        {
            throw new ArgumentException("Траектория вырождена (совпадающие точки).", nameof(path));
        }

        return points;
    }

    /// <summary>
    /// Ориентация профиля: обход должен быть CCW вокруг направления хода
    /// (иначе нормали граней смотрели бы внутрь).
    /// </summary>
    private static IReadOnlyList<Vector3d> OrientProfile(
        IReadOnlyList<Vector3d> profile,
        Vector3d profileNormal,
        IReadOnlyList<Vector3d> points,
        bool closed)
    {
        if (profile.Count < 3)
        {
            throw new ArgumentException("Профиль требует минимум 3 точек.", nameof(profile));
        }

        var ring = new List<Vector3d>(profile.Count);
        foreach (Vector3d point in profile)
        {
            if (!IsFinite(point))
            {
                throw new ArgumentException("Профиль содержит нечисловые координаты.", nameof(profile));
            }

            ring.Add(point);
        }

        for (int i = 0; i < ring.Count; i++)
        {
            if ((ring[(i + 1) % ring.Count] - ring[i]).Length() <= ToleranceMm)
            {
                throw new ArgumentException("Профиль содержит вырожденное ребро.", nameof(profile));
            }
        }

        Vector3d normal = NewellNormal(ring);
        if (normal.LengthSquared() <= 0)
        {
            // вырожденный профиль — используем заданную нормаль
            normal = profileNormal.Normalized();
        }

        if (normal.LengthSquared() <= 0)
        {
            throw new ArgumentException("Плоскость профиля не определена.", nameof(profileNormal));
        }

        Vector3d travel = FirstTravel(points, closed);
        return Vector3d.Dot(normal, travel) < 0 ? [.. Enumerable.Reverse(ring)] : ring;
    }

    private static Vector3d FirstTravel(IReadOnlyList<Vector3d> points, bool closed)
    {
        if (closed)
        {
            Vector3d previous = points[^1];
            Vector3d next = points[1];
            Vector3d tangent = ((points[0] - previous).Normalized() + (next - points[0]).Normalized()).Normalized();
            return tangent.LengthSquared() > 0 ? tangent : (next - previous).Normalized();
        }

        return (points[1] - points[0]).Normalized();
    }

    private static Vector3d NewellNormal(IReadOnlyList<Vector3d> ring)
    {
        Vector3d normal = Vector3d.Zero;
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d a = ring[i];
            Vector3d b = ring[(i + 1) % ring.Count];
            normal += Vector3d.Cross(a, b);
        }

        return normal;
    }

    private static IReadOnlyList<Vector3d> ComputeTangents(IReadOnlyList<Vector3d> points, bool closed)
    {
        var tangents = new List<Vector3d>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Vector3d incoming;
            Vector3d outgoing;
            if (closed)
            {
                incoming = (points[i] - points[(i - 1 + points.Count) % points.Count]).Normalized();
                outgoing = (points[(i + 1) % points.Count] - points[i]).Normalized();
            }
            else if (i == 0)
            {
                incoming = (points[1] - points[0]).Normalized();
                outgoing = incoming;
            }
            else if (i == points.Count - 1)
            {
                incoming = (points[i] - points[i - 1]).Normalized();
                outgoing = incoming;
            }
            else
            {
                incoming = (points[i] - points[i - 1]).Normalized();
                outgoing = (points[i + 1] - points[i]).Normalized();
            }

            Vector3d tangent = (incoming + outgoing).Normalized();
            tangents.Add(tangent.LengthSquared() > 0 ? tangent : incoming);
        }

        return tangents;
    }

    /// <summary>
    /// Кадры (u, w) вдоль траектории: старт — базис профиля, далее перенос
    /// без скручивания; для замкнутой траектории остаточное скручивание
    /// распределяется равномерно по маршруту.
    /// </summary>
    private static IReadOnlyList<(Vector3d U, Vector3d W)> ComputeFrames(
        IReadOnlyList<Vector3d> ring,
        IReadOnlyList<Vector3d> tangents,
        IReadOnlyList<Vector3d> points,
        bool closed)
    {
        Vector3d u = (ring[1] - ring[0]) - (tangents[0] * Vector3d.Dot(ring[1] - ring[0], tangents[0]));
        if (u.LengthSquared() <= ToleranceMm * ToleranceMm)
        {
            u = (ring[^1] - ring[0]) - (tangents[0] * Vector3d.Dot(ring[^1] - ring[0], tangents[0]));
        }

        if (u.LengthSquared() <= ToleranceMm * ToleranceMm)
        {
            u = AnyPerpendicular(tangents[0]);
        }

        u = u.Normalized();
        Vector3d w = Vector3d.Cross(tangents[0], u).Normalized();

        var frames = new List<(Vector3d U, Vector3d W)>(points.Count) { (u, w) };
        for (int i = 1; i < points.Count; i++)
        {
            (u, w) = Transport(u, w, tangents[i - 1], tangents[i]);
            frames.Add((u, w));
        }

        if (closed)
        {
            (Vector3d closeU, _) = Transport(u, w, tangents[^1], tangents[0]);
            double twist = SignedAngleDegrees(closeU, frames[0].U, tangents[0]);
            for (int i = 1; i < frames.Count; i++)
            {
                double angle = twist * (i / (double)points.Count);
                (Vector3d rotatedU, Vector3d rotatedW) = RotateFrame(frames[i].U, frames[i].W, tangents[i], angle);
                frames[i] = (rotatedU, rotatedW);
            }
        }

        return frames;
    }

    private static (Vector3d U, Vector3d W) Transport(
        Vector3d u,
        Vector3d w,
        Vector3d from,
        Vector3d to)
    {
        Vector3d axis = Vector3d.Cross(from, to);
        if (axis.LengthSquared() <= ToleranceMm * ToleranceMm)
        {
            return (u, w);
        }

        double angle = SignedAngleDegrees(from, to, axis.Normalized());
        return RotateFrame(u, w, axis.Normalized(), angle);
    }

    private static (Vector3d U, Vector3d W) RotateFrame(Vector3d u, Vector3d w, Vector3d axis, double degrees)
    {
        Transform3d rotation = Tools.Editing.EditingTransforms.CreateRotation(
            axis,
            Vector3d.Zero,
            Tools.Editing.EditingMath.ToRadians(degrees));
        return (rotation.TransformVector(u).Normalized(), rotation.TransformVector(w).Normalized());
    }

    private static double SignedAngleDegrees(Vector3d from, Vector3d to, Vector3d axis)
        => Tools.Editing.EditingMath.SignedAngleDegrees(from, to, axis);

    private static Vector3d AnyPerpendicular(Vector3d direction)
    {
        Vector3d candidate = Math.Abs(direction.X) < 0.9 ? Vector3d.UnitX : Vector3d.UnitY;
        Vector3d perpendicular = Vector3d.Cross(direction, candidate);
        return perpendicular.LengthSquared() > 0 ? perpendicular.Normalized() : Vector3d.UnitX;
    }

    /// <summary>Грань по кольцу с удалением подряд идущих совпадающих вершин.</summary>
    private static void TryAddFace(HalfEdgeMesh mesh, IReadOnlyList<MeshVertex> ring)
    {
        var cleaned = new List<MeshVertex>(ring.Count);
        foreach (MeshVertex vertex in ring)
        {
            if (cleaned.Count == 0 || !ReferenceEquals(cleaned[^1], vertex))
            {
                cleaned.Add(vertex);
            }
        }

        while (cleaned.Count > 1 && ReferenceEquals(cleaned[0], cleaned[^1]))
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count >= 3)
        {
            mesh.AddFace(cleaned);
        }
    }

    private static bool IsFinite(Vector3d point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
}
