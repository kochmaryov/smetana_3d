using SmetaNA.Core.Geometry;
using SmetaNA.Core.Tools.Editing;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Построение тела вращения профиля вокруг оси (revolve, задача 5.2):
/// плоский замкнутый профиль вращается вокруг оси (точка + направление) на
/// заданный угол с заданным числом сегментов. Полный угол (±360°) замыкается
/// без торцов; частичный — с торцевыми гранями в начале и в конце.
/// <para>
/// Вершины профиля на оси свариваются между кольцами (одна вершина на точку
/// профиля — иначе на полюсе тела получались бы щели); обход граней выбирается
/// по направлению хода, чтобы нормали смотрели наружу.
/// </para>
/// </summary>
public static class RevolveBuilder
{
    /// <summary>Допуск «точка на оси», мм.</summary>
    public const double OnAxisToleranceMm = 1e-6;

    /// <summary>Допуск совпадения точек профиля, мм.</summary>
    public const double ToleranceMm = 1e-9;

    /// <summary>Сегментов по умолчанию для полного оборота.</summary>
    public const int DefaultSegments = 24;

    /// <summary>Максимум сегментов (защита от случайно огромного ввода).</summary>
    public const int MaxSegments = 720;

    /// <summary>Число сегментов по умолчанию для угла (шаг ≈ 15°, минимум 2).</summary>
    public static int DefaultSegmentsFor(double angleDegrees)
    {
        double magnitude = Math.Abs(angleDegrees);
        if (!double.IsFinite(magnitude) || magnitude <= 0)
        {
            return DefaultSegments;
        }

        return Math.Clamp((int)Math.Ceiling(magnitude / 15.0), 2, MaxSegments);
    }

    /// <summary>
    /// Построить тело вращения.
    /// </summary>
    /// <param name="profile">Замкнутый плоский профиль (кольцо точек, минимум 3).</param>
    /// <param name="axisPoint">Точка оси.</param>
    /// <param name="axisDir">Направление оси (нормируется).</param>
    /// <param name="angleDegrees">Угол вращения (знак — по правой тройке вокруг оси).</param>
    /// <param name="segments">Число сегментов тесселяции (минимум 2).</param>
    public static HalfEdgeMesh Build(
        IReadOnlyList<Vector3d> profile,
        Vector3d axisPoint,
        Vector3d axisDir,
        double angleDegrees,
        int segments)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Count < 3)
        {
            throw new ArgumentException("Профиль требует минимум 3 точек.", nameof(profile));
        }

        if (!double.IsFinite(angleDegrees) || Math.Abs(angleDegrees) < ToleranceMm)
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees), angleDegrees, "Угол вращения должен быть ненулевым.");
        }

        if (segments < 2 || segments > MaxSegments)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, $"Число сегментов должно быть от 2 до {MaxSegments}.");
        }

        Vector3d axis = axisDir.Normalized();
        if (axis.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление оси вырождено.", nameof(axisDir));
        }

        IReadOnlyList<Vector3d> ring = ValidateProfile(profile);
        bool wraps = Math.Abs(Math.Abs(angleDegrees) - 360) <= 1e-9;
        if (wraps && segments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "Полный оборот требует минимум 3 сегментов.");
        }

        // «точки на оси» свариваются между кольцами (полюса тела)
        double[] radii = new double[ring.Count];
        int onAxisCount = 0;
        for (int j = 0; j < ring.Count; j++)
        {
            Vector3d offset = ring[j] - axisPoint;
            Vector3d radial = offset - (axis * Vector3d.Dot(offset, axis));
            radii[j] = radial.Length();
            if (radii[j] <= OnAxisToleranceMm)
            {
                onAxisCount++;
            }
        }

        if (onAxisCount == ring.Count)
        {
            throw new ArgumentException("Профиль целиком лежит на оси — тело вращения вырождено.", nameof(profile));
        }

        int refIndex = Array.FindIndex(radii, static radius => radius > OnAxisToleranceMm);
        Vector3d refOffset = ring[refIndex] - axisPoint;
        Vector3d refRadial = refOffset - (axis * Vector3d.Dot(refOffset, axis));
        Vector3d travel = Vector3d.Cross(axis, refRadial.Normalized()) * Math.Sign(angleDegrees);

        // обход CCW вокруг направления хода (иначе нормали смотрели бы внутрь);
        // радиусы точек реверсируются вместе с кольцом
        Vector3d ringNormal = Vector3d.Zero;
        for (int i = 0; i < ring.Count; i++)
        {
            ringNormal += Vector3d.Cross(ring[i], ring[(i + 1) % ring.Count]);
        }

        if (Vector3d.Dot(ringNormal, travel) < 0)
        {
            ring = [.. Enumerable.Reverse(ring)];
            Array.Reverse(radii);
        }

        int ringCount = wraps ? segments : segments + 1;
        double stepDegrees = angleDegrees / segments;

        var mesh = new HalfEdgeMesh();
        var shared = new MeshVertex?[ring.Count];
        for (int j = 0; j < ring.Count; j++)
        {
            if (radii[j] <= OnAxisToleranceMm)
            {
                Vector3d offset = ring[j] - axisPoint;
                shared[j] = mesh.AddVertex(axisPoint + (axis * Vector3d.Dot(offset, axis)));
            }
        }

        var rings = new List<MeshVertex[]>(ringCount);
        for (int k = 0; k < ringCount; k++)
        {
            Transform3d rotation = EditingTransforms.CreateRotation(
                axis,
                axisPoint,
                EditingMath.ToRadians(stepDegrees * k));
            var vertices = new MeshVertex[ring.Count];
            for (int j = 0; j < ring.Count; j++)
            {
                vertices[j] = shared[j] ?? mesh.AddVertex(rotation.TransformPoint(ring[j]));
            }

            rings.Add(vertices);
        }

        for (int k = 0; k < segments; k++)
        {
            MeshVertex[] current = rings[k];
            MeshVertex[] next = rings[wraps ? (k + 1) % ringCount : k + 1];
            for (int j = 0; j < ring.Count; j++)
            {
                int jNext = (j + 1) % ring.Count;
                TryAddFace(mesh, [current[j], current[jNext], next[jNext], next[j]]);
            }
        }

        if (!wraps)
        {
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

    private static IReadOnlyList<Vector3d> ValidateProfile(IReadOnlyList<Vector3d> profile)
    {
        var ring = new List<Vector3d>(profile.Count);
        foreach (Vector3d point in profile)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
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

        Vector3d normal = Vector3d.Zero;
        for (int i = 0; i < ring.Count; i++)
        {
            normal += Vector3d.Cross(ring[i], ring[(i + 1) % ring.Count]);
        }

        if (normal.LengthSquared() <= 0)
        {
            throw new ArgumentException("Профиль вырожден (нулевая площадь).", nameof(profile));
        }

        return ring;
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
}
