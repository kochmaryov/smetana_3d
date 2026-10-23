using SmetaNA.Core.Geometry;

namespace SmetaNA.Manifold.Tests;

/// <summary>
/// Тестовые меши (перенесены из спика 0.4, путь HalfEdgeMesh → Manifold).
/// Обход треугольников — CCW снаружи (правая тройка), как требует Manifold.
/// </summary>
internal static class TestMeshes
{
    /// <summary>Куб 12 треугольников (по 2 на грань).</summary>
    public static HalfEdgeMesh Cube(double sizeX, double sizeY, double sizeZ)
    {
        double hx = sizeX / 2, hy = sizeY / 2, hz = sizeZ / 2;
        Vector3d[] v =
        [
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(-hx, hy, -hz),
            new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz),
        ];
        int[] t =
        [
            0, 2, 1, 0, 3, 2,   // низ  (-z)
            4, 5, 6, 4, 6, 7,   // верх (+z)
            0, 1, 5, 0, 5, 4,   // -y
            2, 3, 7, 2, 7, 6,   // +y
            1, 2, 6, 1, 6, 5,   // +x
            0, 4, 7, 0, 7, 3,   // -x
        ];
        return HalfEdgeMesh.FromTriangles(v, t);
    }

    /// <summary>
    /// UV-сфера: rings — делений по широте (>= 2), segments — по долготе (>= 3).
    /// Треугольников: 2 * segments * (rings - 1).
    /// </summary>
    public static HalfEdgeMesh Sphere(double radius, int rings, int segments)
    {
        if (rings < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(rings));
        }

        if (segments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(segments));
        }

        var v = new List<Vector3d>(checked(2 + (rings - 1) * segments)) { new(0, 0, radius) };
        for (int i = 1; i <= rings - 1; i++)
        {
            double theta = Math.PI * i / rings;
            double z = radius * Math.Cos(theta);
            double r = radius * Math.Sin(theta);
            for (int j = 0; j < segments; j++)
            {
                double phi = 2.0 * Math.PI * j / segments;
                v.Add(new Vector3d(r * Math.Cos(phi), r * Math.Sin(phi), z));
            }
        }

        int south = v.Count;
        v.Add(new Vector3d(0, 0, -radius));

        int Ring(int i, int j) => 1 + (i - 1) * segments + ((j % segments) + segments) % segments;

        var t = new List<int>(checked(2 * segments * (rings - 1) * 3));
        for (int j = 0; j < segments; j++)
        {
            t.AddRange([0, Ring(1, j), Ring(1, j + 1)]);
        }

        for (int i = 1; i <= rings - 2; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = Ring(i, j), b = Ring(i, j + 1), c = Ring(i + 1, j + 1), d = Ring(i + 1, j);
                t.AddRange([a, d, c]);
                t.AddRange([a, c, b]);
            }
        }

        for (int j = 0; j < segments; j++)
        {
            t.AddRange([south, Ring(rings - 1, j + 1), Ring(rings - 1, j)]);
        }

        return HalfEdgeMesh.FromTriangles(v, t);
    }

    /// <summary>
    /// Тор («донат»): majorRadius — радиус трубчатого центра, minorRadius — радиус трубки.
    /// Треугольников: 2 * majorSegments * minorSegments; род поверхности = 1.
    /// </summary>
    public static HalfEdgeMesh Torus(double majorRadius, double minorRadius, int majorSegments, int minorSegments)
    {
        if (majorSegments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(majorSegments));
        }

        if (minorSegments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(minorSegments));
        }

        var v = new Vector3d[checked(majorSegments * minorSegments)];
        for (int i = 0; i < majorSegments; i++)
        {
            double theta = 2.0 * Math.PI * i / majorSegments;
            double ct = Math.Cos(theta), st = Math.Sin(theta);
            for (int j = 0; j < minorSegments; j++)
            {
                double alpha = 2.0 * Math.PI * j / minorSegments;
                double ring = majorRadius + minorRadius * Math.Cos(alpha);
                v[i * minorSegments + j] = new Vector3d(
                    ring * ct,
                    ring * st,
                    minorRadius * Math.Sin(alpha));
            }
        }

        int Vert(int i, int j) =>
            (((i % majorSegments) + majorSegments) % majorSegments) * minorSegments
            + (((j % minorSegments) + minorSegments) % minorSegments);

        var t = new List<int>(checked(2 * majorSegments * minorSegments * 3));
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                int a = Vert(i, j), b = Vert(i + 1, j), c = Vert(i + 1, j + 1), d = Vert(i, j + 1);
                t.AddRange([a, b, d]);
                t.AddRange([b, c, d]);
            }
        }

        return HalfEdgeMesh.FromTriangles(v, t);
    }

    /// <summary>Аналитический объём сферы — для проверки результата CSG.</summary>
    public static double AnalyticSphereVolume(double radius) => 4.0 / 3.0 * Math.PI * radius * radius * radius;

    /// <summary>Аналитический объём тора: 2π²Rr².</summary>
    public static double AnalyticTorusVolume(double majorRadius, double minorRadius) =>
        2.0 * Math.PI * Math.PI * majorRadius * minorRadius * minorRadius;
}
