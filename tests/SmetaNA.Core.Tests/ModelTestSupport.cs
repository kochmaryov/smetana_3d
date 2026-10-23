using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tests;

/// <summary>Общие помощники для тестов групп, компонентов и тегов (модуль M5).</summary>
internal static class ModelTestSupport
{
    public static MeshNode Mesh(string name, double x = 0, double y = 0, double z = 0) => new()
    {
        Mesh = TestMeshes.Cube(),
        Name = name,
        LocalTransform = Transform3d.CreateTranslation(new Vector3d(x, y, z)),
    };

    public static GroupNode Group(string name) => new() { Name = name };

    public static ComponentInstanceNode Instance(
        ComponentDefinition definition,
        string name,
        double x = 0,
        double y = 0,
        double z = 0) => new()
    {
        Definition = definition,
        Name = name,
        LocalTransform = Transform3d.CreateTranslation(new Vector3d(x, y, z)),
    };

    public static ComponentDefinition Definition(string name, string contentName, params SceneNode[] children)
    {
        var content = Group(contentName);
        foreach (SceneNode child in children)
        {
            content.AddChild(child);
        }

        return new ComponentDefinition { Name = name, Content = content };
    }

    /// <summary>Мировые позиции узла по всем веткам доступа (по одному разу на каждый инстанс).</summary>
    public static IReadOnlyList<Vector3d> WorldPointsOf(Scene scene, SceneNode node) =>
        [.. SceneFlattener.Expand(scene)
            .Where(p => ReferenceEquals(p.Node, node))
            .Select(p => p.WorldTransform.TransformPoint(new Vector3d(0, 0, 0)))];

    /// <summary>Мировые трансформации узла по всем веткам доступа.</summary>
    public static IReadOnlyList<Transform3d> WorldTransformsOf(Scene scene, SceneNode node) =>
        [.. SceneFlattener.Expand(scene)
            .Where(p => ReferenceEquals(p.Node, node))
            .Select(p => p.WorldTransform)];
}
