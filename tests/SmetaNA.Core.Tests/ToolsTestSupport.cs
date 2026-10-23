using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tests;

/// <summary>Поддержка тестов инструментов: модели, объём, камеры, выбор граней.</summary>
internal static class ToolsTestSupport
{
    /// <summary>Прямоугольный параллелепипед (шесть квадов, нормали наружу).</summary>
    public static HalfEdgeMesh Box(double sizeX, double sizeY, double sizeZ, Vector3d origin = default)
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex v0 = mesh.AddVertex(origin);
        MeshVertex v1 = mesh.AddVertex(origin + new Vector3d(sizeX, 0, 0));
        MeshVertex v2 = mesh.AddVertex(origin + new Vector3d(sizeX, sizeY, 0));
        MeshVertex v3 = mesh.AddVertex(origin + new Vector3d(0, sizeY, 0));
        MeshVertex v4 = mesh.AddVertex(origin + new Vector3d(0, 0, sizeZ));
        MeshVertex v5 = mesh.AddVertex(origin + new Vector3d(sizeX, 0, sizeZ));
        MeshVertex v6 = mesh.AddVertex(origin + new Vector3d(sizeX, sizeY, sizeZ));
        MeshVertex v7 = mesh.AddVertex(origin + new Vector3d(0, sizeY, sizeZ));

        mesh.AddFace([v0, v3, v2, v1]);
        mesh.AddFace([v4, v5, v6, v7]);
        mesh.AddFace([v0, v1, v5, v4]);
        mesh.AddFace([v2, v3, v7, v6]);
        mesh.AddFace([v1, v2, v6, v5]);
        mesh.AddFace([v0, v4, v7, v3]);
        return mesh;
    }

    /// <summary>
    /// Призма по контуру (CCW в плоскости XY): контур снизу/сверху + боковые квады.
    /// </summary>
    public static HalfEdgeMesh Prism(IReadOnlyList<Vector3d> footprint, double height)
    {
        var mesh = new HalfEdgeMesh();
        var bottom = new MeshVertex[footprint.Count];
        var top = new MeshVertex[footprint.Count];
        for (int i = 0; i < footprint.Count; i++)
        {
            Vector3d p = footprint[i];
            bottom[i] = mesh.AddVertex(p);
            top[i] = mesh.AddVertex(new Vector3d(p.X, p.Y, p.Z + height));
        }

        var bottomRing = new MeshVertex[footprint.Count];
        var topRing = new MeshVertex[footprint.Count];
        for (int i = 0; i < footprint.Count; i++)
        {
            int reversed = footprint.Count - 1 - i;
            bottomRing[i] = bottom[reversed];
            topRing[i] = top[i];
        }

        mesh.AddFace(topRing);
        mesh.AddFace(bottomRing);
        for (int i = 0; i < footprint.Count; i++)
        {
            int next = (i + 1) % footprint.Count;
            mesh.AddFace([bottom[i], bottom[next], top[next], top[i]]);
        }

        return mesh;
    }

    /// <summary>L-образный контур (ступенька) с угловым ребром в точке (5, 5).</summary>
    public static IReadOnlyList<Vector3d> StepFootprint =>
    [
        new(0, 0, 0), new(10, 0, 0), new(10, 5, 0),
        new(5, 5, 0), new(5, 10, 0), new(0, 10, 0),
    ];

    /// <summary>Объём замкнутого меша (дивергенция по треугольникам).</summary>
    public static double SignedVolume(HalfEdgeMesh mesh)
    {
        MeshData data = mesh.ToTriangles();
        double volume = 0;
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            Vector3d a = data.Positions[data.Indices[i]];
            Vector3d b = data.Positions[data.Indices[i + 1]];
            Vector3d c = data.Positions[data.Indices[i + 2]];
            volume += Vector3d.Dot(a, Vector3d.Cross(b, c));
        }

        return volume / 6.0;
    }

    /// <summary>Топология меша: числа вершин, рёбер и граней.</summary>
    public static (int Vertices, int Edges, int Faces) Topology(HalfEdgeMesh mesh)
        => (mesh.Vertices.Count, mesh.HalfEdges.Count / 2, mesh.Faces.Count);

    /// <summary>Грань по направлению нормали (с допуском).</summary>
    public static MeshFace FaceWithNormal(HalfEdgeMesh mesh, Vector3d normal, double tolerance = 1e-9)
    {
        Vector3d expected = normal.Normalized();
        foreach (MeshFace face in mesh.Faces)
        {
            Vector3d candidate = MeshNormals.ComputeFaceNormal(face).Normalized();
            if ((candidate - expected).Length() <= tolerance)
            {
                return face;
            }
        }

        throw new InvalidOperationException($"Грань с нормалью {normal} не найдена.");
    }

    /// <summary>Перспективный вьюпорт, смотрящий из <paramref name="eye"/> на <paramref name="target"/>.</summary>
    public static InferenceViewport ViewportFrom(Vector3d eye, Vector3d target, double widthPx = 800, double heightPx = 600)
    {
        Vector3d forward = (target - eye).Normalized();
        return InferenceViewport.CreatePerspective(eye, forward, Vector3d.UnitZ, Math.PI / 4, widthPx, heightPx);
    }

    /// <summary>Пиксель вьюпорта для мировой точки (точка должна быть перед камерой).</summary>
    public static Vector2d PixelOf(InferenceViewport viewport, Vector3d worldPoint)
        => viewport.TryProject(worldPoint)
            ?? throw new InvalidOperationException($"Точка {worldPoint} не проецируется в камеру.");

    /// <summary>Сцена с одним меш-узлом.</summary>
    public static (Scene Scene, MeshNode Node) SceneWith(HalfEdgeMesh mesh, string name = "Mesh")
    {
        var scene = new Scene();
        var node = new MeshNode { Mesh = mesh, Name = name };
        scene.Root.AddChild(node);
        return (scene, node);
    }

    /// <summary>Контекст инструментов поверх сцены со своей историей.</summary>
    public static (ToolContext Context, UndoStack Undo) ContextFor(Scene scene)
    {
        var undo = new UndoStack();
        return (new ToolContext(scene, undo), undo);
    }

    /// <summary>Совпадение точек с допуском.</summary>
    public static bool NearlyEqual(Vector3d expected, Vector3d actual, double tolerance = 1e-9)
        => (expected - actual).Length() <= tolerance;
}
