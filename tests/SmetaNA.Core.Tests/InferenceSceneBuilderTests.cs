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

    [Fact]
    public void FromScene_CircleRingFace_ProducesSnapCircle()
    {
        HalfEdgeMesh mesh = new();
        var ring = new List<MeshVertex>();
        for (int i = 0; i < 24; i++)
        {
            double angle = 2 * Math.PI * i / 24;
            ring.Add(mesh.AddVertex(new Vector3d(10 * Math.Cos(angle), 10 * Math.Sin(angle), 5)));
        }

        mesh.AddFace(ring);

        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        // Кольцо залитой окружности — 24 вершины на общей окружности: центр
        // восстанавливается circle-fit'ом для привязок центра и касательных.
        // Нормаль окружности — ось Z (знак от порядка точек кольца произвольный).
        SnapCircle circle = Assert.Single(snapshot.Circles);
        GeometryAssert.VectorEqual(new Vector3d(0, 0, 5), circle.Center, 6);
        Assert.Equal(10, circle.Radius, 6);
        Assert.Equal(1, Math.Abs(circle.Normal.Z), 6);
    }

    [Fact]
    public void FromScene_ClosedWireLoop_ProducesSnapCircle()
    {
        HalfEdgeMesh mesh = new();
        var loop = new List<MeshVertex>();
        for (int i = 0; i < 24; i++)
        {
            double angle = 2 * Math.PI * i / 24;
            loop.Add(mesh.AddVertex(new Vector3d(15 * Math.Cos(angle), 15 * Math.Sin(angle), 2)));
        }

        for (int i = 0; i < loop.Count; i++)
        {
            mesh.AddWireEdge(loop[i], loop[(i + 1) % loop.Count]);
        }

        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        SnapCircle circle = Assert.Single(snapshot.Circles);
        GeometryAssert.VectorEqual(new Vector3d(0, 0, 2), circle.Center, 6);
        Assert.Equal(15, circle.Radius, 6);
    }

    [Fact]
    public void FromScene_OpenArcWireChain_ProducesSnapCircle()
    {
        HalfEdgeMesh mesh = new();
        var chain = new List<MeshVertex>();
        for (int i = 0; i <= 8; i++)
        {
            double angle = Math.PI * i / 8;
            chain.Add(mesh.AddVertex(new Vector3d(20 * Math.Cos(angle), 20 * Math.Sin(angle), 0)));
        }

        for (int i = 0; i + 1 < chain.Count; i++)
        {
            mesh.AddWireEdge(chain[i], chain[i + 1]);
        }

        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh });

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        SnapCircle circle = Assert.Single(snapshot.Circles);
        GeometryAssert.VectorEqual(new Vector3d(0, 0, 0), circle.Center, 6);
        Assert.Equal(20, circle.Radius, 6);
    }

    [Fact]
    public void FromScene_QuadFaceAndRectWireLoop_ProduceNoCircles()
    {
        HalfEdgeMesh mesh = new();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(10, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(10, 6, 0));
        MeshVertex d = mesh.AddVertex(new Vector3d(0, 6, 0));
        mesh.AddFace([a, b, c, d]);

        // Незалитый прямоугольный контур: замкнутая цепь из 4 вершин — не кривая,
        // хотя углы прямоугольника и вписаны в окружность.
        MeshVertex e = mesh.AddVertex(new Vector3d(20, 0, 0));
        MeshVertex f = mesh.AddVertex(new Vector3d(30, 0, 0));
        MeshVertex g = mesh.AddVertex(new Vector3d(30, 6, 0));
        MeshVertex h = mesh.AddVertex(new Vector3d(20, 6, 0));
        mesh.AddWireEdge(e, f);
        mesh.AddWireEdge(f, g);
        mesh.AddWireEdge(g, h);
        mesh.AddWireEdge(h, e);

        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh });

        Assert.Empty(InferenceSceneBuilder.FromScene(scene).Circles);
    }

    [Fact]
    public void FromScene_BranchingWireComponent_ProducesNoCircles()
    {
        HalfEdgeMesh mesh = new();
        MeshVertex hub = mesh.AddVertex(new Vector3d(10, 0, 0));

        // Три цепи вершин ОДНОЙ окружности (радиус 10 вокруг начала) с общей
        // вершиной: точки укладываются в окружность, и circle-fit дал бы центр,
        // но компонента с ветвлением не является одиночной кривой.
        double step = Math.PI / 24;
        double[] armAngles =
        [
            4 * step,
            -4 * step,
            Math.PI,
        ];

        foreach (double armAngle in armAngles)
        {
            MeshVertex previous = hub;
            for (int i = 1; i <= 4; i++)
            {
                double angle = armAngle * i / 4;
                MeshVertex next = mesh.AddVertex(new Vector3d(
                    10 * Math.Cos(angle),
                    10 * Math.Sin(angle),
                    0));
                mesh.AddWireEdge(previous, next);
                previous = next;
            }
        }

        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh });

        Assert.Empty(InferenceSceneBuilder.FromScene(scene).Circles);
    }
}
