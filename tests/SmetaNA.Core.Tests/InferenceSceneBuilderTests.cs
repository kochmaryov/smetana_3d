using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Снимок сцены из иерархии Model: рёбра и грани в мировых координатах.</summary>
public sealed class InferenceSceneBuilderTests
{
    [Fact]
    public void FromScene_CubeQuads_ProducesTwelveEdgesAndSixFaces()
    {
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = TestMeshes.CubeQuads() });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        Assert.Equal(12, snapshot.Segments.Count);
        Assert.Equal(6, snapshot.Faces.Count);
        Assert.Empty(snapshot.Circles);
    }

    [Fact]
    public void FromScene_TriangulatedCube_KeepsFaceDiagonalsAsEdges()
    {
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = TestMeshes.Cube() });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        // 12 рёбер куба плюс 6 диагоналей триангуляции граней.
        Assert.Equal(18, snapshot.Segments.Count);
        Assert.Equal(12, snapshot.Faces.Count);
    }

    [Fact]
    public void FromScene_NodeTransform_MovesGeometryToWorld()
    {
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode
        {
            Mesh = TestMeshes.CubeQuads(),
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        Assert.Equal(12, snapshot.Segments.Count);
        Assert.Contains(snapshot.Segments, s => s.Start.X >= 100 || s.End.X >= 100);
        Assert.DoesNotContain(snapshot.Segments, s => s.Start.X < 99 && s.End.X < 99);
    }

    [Fact]
    public void FromScene_ComponentInstances_FlattenPerInstance()
    {
        var definition = new ComponentDefinition
        {
            Name = "Cube",
            Content = new MeshNode { Mesh = TestMeshes.CubeQuads() },
        };
        var scene = new Scene();
        var group = new GroupNode();
        group.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 0, 0)),
        });
        group.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(50, 0, 0)),
        });
        scene.Root.AddChild(group);

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        Assert.Equal(24, snapshot.Segments.Count);
        Assert.Equal(12, snapshot.Faces.Count);
    }

    [Fact]
    public void FromScene_HiddenNode_SkippedUnlessRequested()
    {
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = TestMeshes.CubeQuads(), IsVisible = false });

        Assert.Empty(InferenceSceneBuilder.FromScene(scene).Segments);
        Assert.Equal(12, InferenceSceneBuilder.FromScene(scene, includeHidden: true).Segments.Count);
    }

    [Fact]
    public void FromNode_SubtreeWithOwnTransform_IsIsolated()
    {
        var group = new GroupNode
        {
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 0, 50)),
        };
        group.AddChild(new MeshNode { Mesh = TestMeshes.CubeQuads() });

        InferenceScene snapshot = InferenceSceneBuilder.FromNode(group, Transform3d.Identity);

        Assert.Equal(12, snapshot.Segments.Count);
        Assert.All(snapshot.Segments, s => Assert.True(s.Start.Z >= 50 && s.End.Z >= 50));
    }

    [Fact]
    public void FromScene_CyclicComponent_Throws()
    {
        var definition = new ComponentDefinition { Name = "Loop" };
        var content = new GroupNode();
        content.AddChild(new ComponentInstanceNode { Definition = definition });
        definition.Content = content;

        var scene = new Scene();
        scene.Root.AddChild(new ComponentInstanceNode { Definition = definition });

        // Содержимое определения ссылается на инстанс того же определения.
        Assert.Throws<InvalidOperationException>(() => InferenceSceneBuilder.FromScene(scene));
    }
}
