using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Rendering;

/// <summary>
/// Лёгкая эталонная сцена (задача 6.0): плита, корпус и цилиндр — сотни
/// треугольников вместо демо-рельефа в 500 000. Верхняя грань плиты лежит на
/// Z=0 («земля» XY), корпус и цилиндр стоят на ней: есть грани для рисования и
/// Push/Pull, но нет шума привязок по вершинам рельефа и наклонных плоскостей
/// построения. Демо-рельеф для замеров остаётся в <see cref="DemoSceneFactory"/>
/// и сеется флагом <c>--demo-terrain</c>.
/// </summary>
public static class ReferenceSceneFactory
{
    /// <summary>Имя узла плиты (верхняя грань — плоскость рисования Z=0).</summary>
    public const string PlateNodeName = "Плита";

    /// <summary>Имя узла корпуса.</summary>
    public const string BodyNodeName = "Корпус";

    /// <summary>Имя узла цилиндра.</summary>
    public const string CylinderNodeName = "Цилиндр";

    /// <summary>Сегментов окружности у цилиндра (24 — как у CircleTool по умолчанию).</summary>
    public const int CylinderSegments = 24;

    /// <summary>Добавить эталонную сцену (плита + корпус + цилиндр) в сцену.</summary>
    public static IReadOnlyList<MeshNode> AddToScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        // Плита 200×200×10 с верхом на Z=0; корпус 100×80×40 и цилиндр R25×60
        // стоят на ней, не пересекая друг друга.
        MeshNode[] nodes =
        [
            Box(PlateNodeName, new Vector3d(-100, -100, -10), new Vector3d(200, 200, 10)),
            Box(BodyNodeName, new Vector3d(-50, -40, 0), new Vector3d(100, 80, 40)),
            Cylinder(CylinderNodeName, new Vector3d(65, -70, 0), 25, 60, CylinderSegments),
        ];

        foreach (MeshNode node in nodes)
        {
            scene.Root.AddChild(node);
        }

        return nodes;
    }

    /// <summary>Параллелепипед: начало и размер (мм), нормали граней — наружу.</summary>
    public static MeshNode Box(string name, Vector3d origin, Vector3d size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!IsFinitePositive(size))
        {
            throw new ArgumentException("Размер параллелепипеда должен быть положительным.", nameof(size));
        }

        var mesh = new HalfEdgeMesh();
        MeshVertex v000 = mesh.AddVertex(origin);
        MeshVertex v100 = mesh.AddVertex(origin + new Vector3d(size.X, 0, 0));
        MeshVertex v110 = mesh.AddVertex(origin + new Vector3d(size.X, size.Y, 0));
        MeshVertex v010 = mesh.AddVertex(origin + new Vector3d(0, size.Y, 0));
        MeshVertex v001 = mesh.AddVertex(origin + new Vector3d(0, 0, size.Z));
        MeshVertex v101 = mesh.AddVertex(origin + new Vector3d(size.X, 0, size.Z));
        MeshVertex v111 = mesh.AddVertex(origin + new Vector3d(size.X, size.Y, size.Z));
        MeshVertex v011 = mesh.AddVertex(origin + new Vector3d(0, size.Y, size.Z));

        mesh.AddFace([v000, v010, v110, v100]); // низ, −Z
        mesh.AddFace([v001, v101, v111, v011]); // верх, +Z
        mesh.AddFace([v000, v100, v101, v001]); // −Y
        mesh.AddFace([v010, v011, v111, v110]); // +Y
        mesh.AddFace([v000, v001, v011, v010]); // −X
        mesh.AddFace([v100, v110, v111, v101]); // +X

        return new MeshNode { Mesh = mesh, Name = name };
    }

    /// <summary>
    /// Цилиндр: центр основания, радиус и высота (мм), нормали граней — наружу.
    /// </summary>
    public static MeshNode Cylinder(string name, Vector3d baseCenter, double radius, double height, int segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Радиус должен быть положительным.");
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Высота должна быть положительной.");
        }

        if (segments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), "Цилиндру нужно минимум 3 сегмента.");
        }

        var mesh = new HalfEdgeMesh();
        var bottom = new MeshVertex[segments];
        var top = new MeshVertex[segments];
        for (int i = 0; i < segments; i++)
        {
            double angle = Math.Tau * i / segments;
            double x = baseCenter.X + (radius * Math.Cos(angle));
            double y = baseCenter.Y + (radius * Math.Sin(angle));
            bottom[i] = mesh.AddVertex(new Vector3d(x, y, baseCenter.Z));
            top[i] = mesh.AddVertex(new Vector3d(x, y, baseCenter.Z + height));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.AddFace([bottom[i], bottom[next], top[next], top[i]]);
        }

        var capTop = new MeshVertex[segments];
        var capBottom = new MeshVertex[segments];
        for (int i = 0; i < segments; i++)
        {
            capTop[i] = top[i];
            capBottom[i] = bottom[segments - 1 - i];
        }

        mesh.AddFace(capTop); // +Z
        mesh.AddFace(capBottom); // −Z

        return new MeshNode { Mesh = mesh, Name = name };
    }

    private static bool IsFinitePositive(Vector3d size)
        => double.IsFinite(size.X) && size.X > 0
            && double.IsFinite(size.Y) && size.Y > 0
            && double.IsFinite(size.Z) && size.Z > 0;
}
