using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile.Templates;

/// <summary>
/// Геометрия шаблонов-примеров (модуль M9). Контуры совпадают с эталонными моделями
/// тестов (tests/SmetaNA.Formats.Tests/GoldenMeshes.cs): «стакан» 60×60×80 мм со
/// стенкой 5 мм и «подставка» 100×60×50 мм. Меши замкнуты (watertight) и набраны
/// полигонами (quad-грани) — типичная топология моделей редактора.
/// </summary>
internal static class TemplateMeshes
{
    /// <summary>«Стакан» 60×60×80 мм: стенка 5 мм, дно 5 мм.</summary>
    public static HalfEdgeMesh Cup() => Build(CupPositions, CupRings);

    /// <summary>«Подставка»: ступенька 100×60×50 мм (база 100×60×20 + верх 60×60×30).</summary>
    public static HalfEdgeMesh Stand() => Build(StandPositions, StandRings);

    private static HalfEdgeMesh Build(IReadOnlyList<Vector3d> positions, int[][] rings)
    {
        var mesh = new HalfEdgeMesh();
        var vertices = new MeshVertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            vertices[i] = mesh.AddVertex(positions[i]);
        }

        foreach (int[] ring in rings)
        {
            var face = new MeshVertex[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                face[i] = vertices[ring[i]];
            }

            mesh.AddFace(face);
        }

        return mesh;
    }

    private static readonly Vector3d[] CupPositions =
    [
        new(0, 0, 0), new(60, 0, 0), new(60, 60, 0), new(0, 60, 0),
        new(0, 0, 80), new(60, 0, 80), new(60, 60, 80), new(0, 60, 80),
        new(5, 5, 80), new(55, 5, 80), new(55, 55, 80), new(5, 55, 80),
        new(5, 5, 5), new(55, 5, 5), new(55, 55, 5), new(5, 55, 5),
    ];

    private static readonly int[][] CupRings =
    [
        [0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
        [4, 5, 9, 8], [5, 6, 10, 9], [6, 7, 11, 10], [7, 4, 8, 11],
        [12, 8, 9, 13], [13, 9, 10, 14], [15, 14, 10, 11], [12, 15, 11, 8],
        [12, 13, 14, 15],
    ];

    private static readonly Vector3d[] StandPositions =
    [
        new(0, 0, 0), new(100, 0, 0), new(100, 60, 0), new(0, 60, 0),
        new(0, 0, 20), new(20, 0, 20), new(80, 0, 20), new(100, 0, 20),
        new(100, 60, 20), new(80, 60, 20), new(20, 60, 20), new(0, 60, 20),
        new(20, 0, 50), new(80, 0, 50), new(80, 60, 50), new(20, 60, 50),
    ];

    private static readonly int[][] StandRings =
    [
        [0, 3, 2, 1], [12, 13, 14, 15], [4, 5, 10, 11], [6, 7, 8, 9],
        [0, 1, 7, 6], [0, 6, 5, 4], [5, 6, 13, 12],
        [2, 3, 11, 10], [2, 10, 9, 8], [9, 10, 15, 14],
        [3, 0, 4, 11], [1, 2, 8, 7], [10, 5, 12, 15], [6, 9, 14, 13],
    ];
}
