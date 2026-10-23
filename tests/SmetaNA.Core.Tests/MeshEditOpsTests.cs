using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Правки меша: планирование переноса, склеивание, каскад граней.</summary>
public class MeshEditOpsTests
{
    [Fact]
    public void PlanMove_MovesSeedsAndGluedVerticesOnly()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshVertex seed = mesh.Vertices[4]; // (0, 0, 10)
        MeshVertex glued = mesh.AddVertex(new Vector3d(0, 0, 10)); // дубль позиции (несваренная геометрия)
        MeshVertex distant = mesh.AddVertex(new Vector3d(50, 50, 50));

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanMove(
            [seed],
            mesh.Vertices,
            new Vector3d(0, 0, 3));

        Assert.Equal(2, moves.Count);
        Assert.Contains(moves, move => ReferenceEquals(move.Vertex, seed));
        Assert.Contains(moves, move => ReferenceEquals(move.Vertex, glued));
        Assert.DoesNotContain(moves, move => ReferenceEquals(move.Vertex, distant));
        Assert.Equal(new Vector3d(0, 0, 13), moves.First(move => ReferenceEquals(move.Vertex, seed)).To);
    }

    [Fact]
    public void PlanMove_DeduplicatesRepeatedSeeds()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(1, 1, 1);
        MeshVertex seed = mesh.Vertices[0];

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanMove(
            [seed, seed, seed],
            mesh.Vertices,
            Vector3d.UnitX);

        Assert.Single(moves);
    }

    [Fact]
    public void PlanMove_SkipsGluedVerticesOutsideTolerance()
    {
        var seed = new MeshVertex { Position = new Vector3d(0, 0, 0) };
        var near = new MeshVertex { Position = new Vector3d(0, 0, 1e-7) };
        var far = new MeshVertex { Position = new Vector3d(0, 0, 1e-3) };

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanMove(
            [seed],
            [near, far],
            Vector3d.UnitZ,
            tolerance: 1e-6);

        Assert.Equal(2, moves.Count);
        Assert.Contains(moves, move => ReferenceEquals(move.Vertex, near));
        Assert.DoesNotContain(moves, move => ReferenceEquals(move.Vertex, far));
    }

    [Fact]
    public void ApplyAndRevert_RoundTripPositions()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanMove(
            [mesh.Vertices[4]],
            mesh.Vertices,
            Vector3d.UnitZ);

        MeshEditOps.Apply(moves);
        Assert.Equal(new Vector3d(0, 0, 11), mesh.Vertices[4].Position);

        MeshEditOps.Revert(moves);
        Assert.Equal(new Vector3d(0, 0, 10), mesh.Vertices[4].Position);
    }

    [Fact]
    public void PlanFaceMoveAlongNormal_MovesRingAlongFaceNormal()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 20, 30);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 5);
        MeshEditOps.Apply(moves);

        Assert.Equal(4, moves.Count);
        foreach (MeshVertex vertex in mesh.GetRing(top))
        {
            Assert.Equal(35, vertex.Position.Z, 9);
        }
    }

    [Fact]
    public void CollectDependentFaces_ReturnsEveryFaceTouchingMovedSet()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        IReadOnlyList<MeshFace> dependents = MeshEditOps.CollectDependentFaces(mesh, mesh.GetRing(top));

        // верхняя грань + четыре боковые (каскадное обновление соседних граней)
        Assert.Equal(5, dependents.Count);
        Assert.Contains(top, dependents);
    }

    [Fact]
    public void FaceCentroid_AveragesRingPositions()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 20, 30);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        Vector3d centroid = MeshEditOps.FaceCentroid(top);

        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(5, 10, 30), centroid));
    }
}
