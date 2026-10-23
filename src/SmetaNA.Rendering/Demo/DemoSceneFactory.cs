using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Rendering;

/// <summary>
/// Демо-сцена для замеров производительности (задача 1.6): рельеф из квад-чанков,
/// ~500 000 треугольников (бюджет DoD §5.6). Геометрия детерминирована (без Random),
/// строится как настоящая иерархия сцены — MeshNode на чанк, чтобы работали
/// пиккинг и пообъектное выделение.
/// </summary>
public static class DemoSceneFactory
{
    public const int DefaultTargetTriangles = 500_000;

    /// <summary>Создать чанки рельефа (не добавляет в сцену).</summary>
    /// <param name="targetTriangles">Целевое число треугольников (с точностью до чанка).</param>
    /// <param name="chunksPerSide">Чанков по стороне квадрата рельефа.</param>
    public static IReadOnlyList<MeshNode> CreateTerrainChunks(int targetTriangles = DefaultTargetTriangles, int chunksPerSide = 4)
    {
        if (targetTriangles < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTriangles), "Нужно минимум 2 треугольника.");
        }

        if (chunksPerSide < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chunksPerSide), "Нужно минимум один чанк.");
        }

        int chunkCount = chunksPerSide * chunksPerSide;
        int quadsPerSide = Math.Max(1, (int)Math.Round(Math.Sqrt(targetTriangles / (2.0 * chunkCount))));
        const double chunkSize = 5_000.0; // мм — 5 м на чанк
        double step = chunkSize / quadsPerSide;

        var nodes = new List<MeshNode>(chunkCount);
        for (int chunkY = 0; chunkY < chunksPerSide; chunkY++)
        {
            for (int chunkX = 0; chunkX < chunksPerSide; chunkX++)
            {
                double originX = (chunkX * chunkSize) - (chunksPerSide * chunkSize * 0.5);
                double originY = (chunkY * chunkSize) - (chunksPerSide * chunkSize * 0.5);
                nodes.Add(CreateChunk(originX, originY, step, quadsPerSide, nodes.Count + 1));
            }
        }

        return nodes;
    }

    /// <summary>Добавить демо-рельеф в сцену и вернуть созданные узлы.</summary>
    public static IReadOnlyList<MeshNode> AddTerrainToScene(Scene scene, int targetTriangles = DefaultTargetTriangles, int chunksPerSide = 4)
    {
        ArgumentNullException.ThrowIfNull(scene);

        IReadOnlyList<MeshNode> nodes = CreateTerrainChunks(targetTriangles, chunksPerSide);
        foreach (MeshNode node in nodes)
        {
            scene.Root.AddChild(node);
        }

        return nodes;
    }

    private static MeshNode CreateChunk(double originX, double originY, double step, int quadsPerSide, int number)
    {
        var mesh = new HalfEdgeMesh();
        var vertices = new MeshVertex[(quadsPerSide + 1) * (quadsPerSide + 1)];
        for (int j = 0; j <= quadsPerSide; j++)
        {
            for (int i = 0; i <= quadsPerSide; i++)
            {
                double x = originX + (i * step);
                double y = originY + (j * step);
                vertices[(j * (quadsPerSide + 1)) + i] = mesh.AddVertex(new Vector3d(x, y, Height(x, y)));
            }
        }

        for (int j = 0; j < quadsPerSide; j++)
        {
            for (int i = 0; i < quadsPerSide; i++)
            {
                MeshVertex v00 = vertices[(j * (quadsPerSide + 1)) + i];
                MeshVertex v10 = vertices[(j * (quadsPerSide + 1)) + i + 1];
                MeshVertex v11 = vertices[((j + 1) * (quadsPerSide + 1)) + i + 1];
                MeshVertex v01 = vertices[((j + 1) * (quadsPerSide + 1)) + i];
                mesh.AddFace([v00, v10, v11, v01]);
            }
        }

        return new MeshNode
        {
            Mesh = mesh,
            Name = $"Terrain {number}",
        };
    }

    /// <summary>Детерминированная высота рельефа (мм) — две синусоиды.</summary>
    private static double Height(double x, double y) =>
        (600.0 * Math.Sin(x * 0.0011) * Math.Cos(y * 0.0013)) +
        (220.0 * Math.Sin((x + y) * 0.0007));
}
