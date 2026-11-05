using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Снимок W1: ссылки на сущности, начала координат контейнеров и середины дуг.</summary>
public sealed class InferenceSceneBuilderEntityTests
{
    [Fact]
    public void FromScene_SegmentsAndFaces_CarryEntityRefs()
    {
        var scene = new Scene();
        var node = new MeshNode { Mesh = TestMeshes.CubeQuads(), Name = "Cube" };
        scene.Root.AddChild(node);

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        Assert.All(snapshot.Segments, segment =>
        {
            Assert.Equal(node.Id, segment.Entity.NodeId);
            Assert.Equal(SnapEntityKind.Edge, segment.Entity.Kind);
            Assert.NotNull(segment.Entity.ElementIndex);
        });

        SnapFace first = snapshot.Faces[0];
        Assert.Equal(node.Id, first.Entity.NodeId);
        Assert.Equal(SnapEntityKind.Face, first.Entity.Kind);
        Assert.Equal(0, first.Entity.ElementIndex);
    }

    [Fact]
    public void FromScene_Group_ProducesInstanceOriginWithWorldAxes()
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            Name = "Detail",
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        group.AddChild(new MeshNode { Mesh = TestMeshes.CubeQuads() });
        scene.Root.AddChild(group);

        InferenceScene snapshot = InferenceSceneBuilder.FromScene(scene);

        SnapInstanceOrigin origin = Assert.Single(snapshot.InstanceOrigins);
        InferenceAssert.PointEqual(new Vector3d(100, 0, 0), origin.Origin);
        InferenceAssert.DirectionEqual(Vector3d.UnitX, origin.AxisX);
        InferenceAssert.DirectionEqual(Vector3d.UnitY, origin.AxisY);
        Assert.Equal("Detail", origin.Entity.DisplayName);
        Assert.Equal(SnapEntityKind.Node, origin.Entity.Kind);
    }

    [Fact]
    public void FromScene_OpenArcChain_ProducesArcMidpoint()
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

        SnapArcPoint midpoint = Assert.Single(snapshot.ArcPoints);
        InferenceAssert.PointEqual(new Vector3d(0, 20, 0), midpoint.Point);
        Assert.Single(snapshot.Circles);
    }

    [Fact]
    public void FromScene_OpenArcChain_ProducesTangentAtEachEnd()
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

        Assert.Equal(2, snapshot.ArcTangents.Count);
        SnapArcTangent atStart = Assert.Single(
            snapshot.ArcTangents, t => Math.Abs(t.Point.X - 20) < 1e-9);
        InferenceAssert.PointEqual(new Vector3d(20, 0, 0), atStart.Point);

        // Знак направления зависит от ориентации подобранной окружности: важна коллинеарность.
        Assert.Equal(1, Math.Abs(Vector3d.Dot(atStart.Direction, Vector3d.UnitY)), 6);

        SnapArcTangent atEnd = Assert.Single(
            snapshot.ArcTangents, t => Math.Abs(t.Point.X + 20) < 1e-9);
        Assert.Equal(1, Math.Abs(Vector3d.Dot(atEnd.Direction, Vector3d.UnitY)), 6);
    }
}
