using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Тестовые сцены проектного формата: сложный граф с компонентами (definition +
/// несколько instance), общим экземпляром меша, скрытыми узлами и «хитрыми»
/// double в трансформациях (денормализованные, π, ±0, границы диапазона).
/// </summary>
internal static class ProjectScenes
{
    /// <summary>
    /// Сложная сцена: группа «Сборка» (меш + два инстанса одного компонента «Ножка»),
    /// внутри определения «Ножка» — меш и вложенный инстанс компонента «Шляпка»;
    /// скрытая группа содержит второй узел с тем же экземпляром меша.
    /// </summary>
    public static ProjectDocument ComplexDocument()
    {
        var material = new ProjectMaterial
        {
            Name = "Красный",
            Red = 0.9,
            Green = 0.1,
            Blue = 0.2,
            Alpha = 1.0,
        };

        HalfEdgeMesh detailMesh = TriangleMesh(CubePositions, CubeIndices);
        foreach (MeshFace face in detailMesh.Faces)
        {
            face.MaterialIndex = 0;
        }

        var capDefinition = new ComponentDefinition { Name = "Шляпка" };
        capDefinition.Content = new MeshNode
        {
            Name = "Шляпка",
            Mesh = TriangleMesh(CapPositions, CapIndices),
        };

        var legDefinition = new ComponentDefinition { Name = "Ножка" };
        var legContent = new GroupNode { Name = "Ножка" };
        legContent.AddChild(new MeshNode
        {
            Name = "Стержень",
            Mesh = TriangleMesh(CubePositions, CubeIndices),
        });
        legContent.AddChild(new ComponentInstanceNode
        {
            Name = "Шляпка №1",
            Definition = capDefinition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 0, 5)),
        });
        legDefinition.Content = legContent;

        var document = new ProjectDocument
        {
            Unit = LengthUnit.Centimeter,
            Metadata = ProjectMetadata.CreateDefault() with { Title = "Тестовый проект" },
        };
        document.Materials.Add(material);

        var assembly = new GroupNode
        {
            Name = "Сборка",
            LocalTransform = new Transform3d(
                Math.PI, 0.1, 5e-324, 0,
                -0.0, 2.5, 1e-300, 0,
                0.25, -0.125, 1.7976931348623157E+308, 0,
                12.5, -7.25, 0.1, 1),
        };
        assembly.AddChild(new MeshNode
        {
            Name = "Деталь А",
            Mesh = detailMesh,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(0.1, -0.25, 1e300)),
        });
        assembly.AddChild(new ComponentInstanceNode
        {
            Name = "Ножка №1",
            Definition = legDefinition,
            LocalTransform = new Transform3d(
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                10, 20, 30.5, 1),
        });
        assembly.AddChild(new ComponentInstanceNode
        {
            Name = "Ножка №2",
            Definition = legDefinition,
            LocalTransform = new Transform3d(
                0.8660254037844387, -0.5, 0, 0,
                0.5, 0.8660254037844387, 0, 0,
                0, 0, 1, 0,
                -40.25, 15.125, -0.0, 1),
        });
        document.Scene.Root.AddChild(assembly);

        var hidden = new GroupNode
        {
            Name = "Скрытое",
            IsVisible = false,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(0.1, 0.2, 0.3)),
        };
        hidden.AddChild(new MeshNode
        {
            Name = "Общий меш",
            Mesh = detailMesh,
        });
        document.Scene.Root.AddChild(hidden);
        return document;
    }

    /// <summary>Меш-куб 100×100×100 мм (контур эталонного GoldenMeshes.Cube).</summary>
    public static HalfEdgeMesh Cube() => TriangleMesh(CubePositions, CubeIndices);

    /// <summary>Построить меш из треугольного супа.</summary>
    public static HalfEdgeMesh TriangleMesh(IReadOnlyList<Vector3d> positions, IReadOnlyList<int> indices) =>
        HalfEdgeMesh.FromTriangles(positions, indices);

    /// <summary>Узел сцены по имени (включая корень); не найден — null.</summary>
    public static SceneNode? Find(Scene scene, string name) =>
        scene.EnumerateNodes().FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.Ordinal));

    /// <summary>Узел сцены по имени; не найден — исключение.</summary>
    public static SceneNode Require(Scene scene, string name) =>
        Find(scene, name) ?? throw new InvalidOperationException($"Узел «{name}» не найден в сцене.");

    private static readonly Vector3d[] CubePositions =
    [
        new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), new(0, 100, 0),
        new(0, 0, 100), new(100, 0, 100), new(100, 100, 100), new(0, 100, 100),
    ];

    private static readonly int[] CubeIndices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
    ];

    private static readonly Vector3d[] CapPositions =
    [
        new(0, 0, 0), new(20, 0, 0), new(20, 20, 0), new(0, 20, 0),
        new(0, 0, 5), new(20, 0, 5), new(20, 20, 5), new(0, 20, 5),
    ];

    private static readonly int[] CapIndices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
    ];
}
