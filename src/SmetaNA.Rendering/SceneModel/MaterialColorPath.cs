using SmetaNA.Core.Geometry;
using SmetaNA.Core.Materials;

namespace SmetaNA.Rendering;

/// <summary>
/// Путь цвета материалов в рендере: по 4 канала RGBA (0..1) на треугольник в
/// порядке <see cref="MeshData.Indices"/> — цвет берётся из материала грани
/// (<c>MeshFace.MaterialIndex</c> → палитра меша, наследование материала объекта).
/// </summary>
public static class MaterialColorPath
{
    /// <summary>
    /// Цвета треугольников меша; <c>null</c> — материалы не назначены
    /// (рисуется цвет по умолчанию, лишняя память не аллоцируется).
    /// </summary>
    public static float[]? Build(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!MaterialResolver.HasAnyAssignment(mesh))
        {
            return null;
        }

        MeshData data = mesh.ToTriangles();
        int triangleCount = data.Indices.Count / 3;
        var colors = new float[triangleCount * 4];

        // веерная триангуляция ToTriangles: грань i даёт (ring - 2) треугольников подряд
        int triangle = 0;
        foreach (MeshFace face in mesh.Faces)
        {
            (float R, float G, float B, float A) = MaterialResolver.Resolve(mesh, face).EffectiveColor.ToNormalized();
            int faceTriangles = FaceTriangleCount(face);
            for (int i = 0; i < faceTriangles; i++)
            {
                int offset = triangle * 4;
                colors[offset] = R;
                colors[offset + 1] = G;
                colors[offset + 2] = B;
                colors[offset + 3] = A;
                triangle++;
            }
        }

        return colors;
    }

    private static int FaceTriangleCount(MeshFace face)
    {
        int ring = 0;
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            ring++;
            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);
        return Math.Max(0, ring - 2);
    }
}
