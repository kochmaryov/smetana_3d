namespace Spike.Manifold;

/// <summary>
/// Тестовые меши, построенные из массивов (путь HalfEdgeMesh → вершины/треугольники → Manifold).
/// Обход граней — CCW снаружи (правая тройка), как требует Manifold.
/// </summary>
public static class TestMeshes
{
    /// <summary>Куб 12 треугольников (по 2 на грань).</summary>
    public static MeshData Cube(double sizeX, double sizeY, double sizeZ)
    {
        double hx = sizeX / 2, hy = sizeY / 2, hz = sizeZ / 2;
        double[] v =
        [
            -hx, -hy, -hz,  hx, -hy, -hz,  hx, hy, -hz,  -hx, hy, -hz,
            -hx, -hy,  hz,  hx, -hy,  hz,  hx, hy,  hz,  -hx, hy,  hz,
        ];
        ulong[] t =
        [
            0, 2, 1,  0, 3, 2,   // низ  (-z)
            4, 5, 6,  4, 6, 7,   // верх (+z)
            0, 1, 5,  0, 5, 4,   // -y
            2, 3, 7,  2, 7, 6,   // +y
            1, 2, 6,  1, 6, 5,   // +x
            0, 4, 7,  0, 7, 3,   // -x
        ];
        return new MeshData(v, t);
    }

    /// <summary>
    /// UV-сфера: rings — делений по широте (>= 2), segments — по долготе (>= 3).
    /// Треугольников: 2 * segments * (rings - 1).
    /// </summary>
    public static MeshData Sphere(double radius, int rings, int segments)
    {
        if (rings < 2) throw new ArgumentOutOfRangeException(nameof(rings));
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

        var v = new List<double>(checked((2 + (rings - 1) * segments) * 3));
        var t = new List<ulong>(checked(2 * segments * (rings - 1) * 3));

        // Северный полюс (индекс 0).
        v.AddRange([0.0, 0.0, radius]);
        for (int i = 1; i <= rings - 1; i++)
        {
            double theta = Math.PI * i / rings;
            double z = radius * Math.Cos(theta);
            double r = radius * Math.Sin(theta);
            for (int j = 0; j < segments; j++)
            {
                double phi = 2.0 * Math.PI * j / segments;
                v.AddRange([r * Math.Cos(phi), r * Math.Sin(phi), z]);
            }
        }

        // Южный полюс.
        ulong south = (ulong)(v.Count / 3);
        v.AddRange([0.0, 0.0, -radius]);

        ulong Ring(int i, int j) => (ulong)(1 + (i - 1) * segments + (j % segments + segments) % segments);

        // Верхний колпак.
        for (int j = 0; j < segments; j++)
            t.AddRange([0UL, Ring(1, j), Ring(1, j + 1)]);

        // Средние пояса.
        for (int i = 1; i <= rings - 2; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                ulong a = Ring(i, j), b = Ring(i, j + 1), c = Ring(i + 1, j + 1), d = Ring(i + 1, j);
                t.AddRange([a, d, c]);
                t.AddRange([a, c, b]);
            }
        }

        // Нижний колпак (обход обратен верхнему).
        for (int j = 0; j < segments; j++)
            t.AddRange([south, Ring(rings - 1, j + 1), Ring(rings - 1, j)]);

        return new MeshData([.. v], [.. t]);
    }

    /// <summary>
    /// Тор («донат»): majorRadius — радиус трубчатого центра, minorRadius — радиус трубки.
    /// Треугольников: 2 * majorSegments * minorSegments; род поверхности = 1.
    /// </summary>
    public static MeshData Torus(double majorRadius, double minorRadius, int majorSegments, int minorSegments)
    {
        if (majorSegments < 3) throw new ArgumentOutOfRangeException(nameof(majorSegments));
        if (minorSegments < 3) throw new ArgumentOutOfRangeException(nameof(minorSegments));

        var v = new double[majorSegments * minorSegments * 3];
        var t = new ulong[majorSegments * minorSegments * 6];

        for (int i = 0; i < majorSegments; i++)
        {
            double theta = 2.0 * Math.PI * i / majorSegments;
            double ct = Math.Cos(theta), st = Math.Sin(theta);
            for (int j = 0; j < minorSegments; j++)
            {
                double alpha = 2.0 * Math.PI * j / minorSegments;
                double ring = majorRadius + minorRadius * Math.Cos(alpha);
                int idx = (i * minorSegments + j) * 3;
                v[idx] = ring * ct;
                v[idx + 1] = ring * st;
                v[idx + 2] = minorRadius * Math.Sin(alpha);
            }
        }

        ulong Vert(int i, int j) =>
            (ulong)(((i % majorSegments + majorSegments) % majorSegments) * minorSegments
                    + ((j % minorSegments + minorSegments) % minorSegments));

        int tri = 0;
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                ulong a = Vert(i, j), b = Vert(i + 1, j), c = Vert(i + 1, j + 1), d = Vert(i, j + 1);
                t[tri++] = a; t[tri++] = b; t[tri++] = d;
                t[tri++] = b; t[tri++] = c; t[tri++] = d;
            }
        }

        return new MeshData(v, t);
    }

    /// <summary>Аналитический объём сферы — для проверки результата CSG.</summary>
    public static double AnalyticSphereVolume(double radius) => 4.0 / 3.0 * Math.PI * radius * radius * radius;

    /// <summary>Аналитический объём тора: 2π²Rr².</summary>
    public static double AnalyticTorusVolume(double majorRadius, double minorRadius) =>
        2.0 * Math.PI * Math.PI * majorRadius * minorRadius * minorRadius;
}
