using SmetaNA.Core.Geometry;
using SmetaNA.Core.Materials;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Стирание граней/рёбер и восстановление через MeshEditOps, Offset-расщепление
/// грани, план трансформаций и двугранные углы (задачи 2.3 и 5.4).
/// </summary>
public class MeshEditOpsEraseTests
{
    [Fact]
    public void EraseFaces_RemovesFace_OpensHole()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        IReadOnlyList<MeshEditOps.FaceEraseRecord> records = MeshEditOps.EraseFaces(mesh, [top]);

        Assert.Single(records);
        Assert.Equal(5, mesh.Faces.Count);
        Assert.Equal(8, mesh.Vertices.Count);
        Assert.False(mesh.IsWatertight);
    }

    [Fact]
    public void EraseRestoreFaces_RestoresIdentityAndWatertight()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        MeshFace bottom = ToolsTestSupport.FaceWithNormal(mesh, -Vector3d.UnitZ);
        (int Vertices, int Edges, int Faces) before = ToolsTestSupport.Topology(mesh);

        IReadOnlyList<MeshEditOps.FaceEraseRecord> records = MeshEditOps.EraseFaces(mesh, [top, bottom]);
        MeshEditOps.RestoreFaces(records);

        Assert.Equal(before, ToolsTestSupport.Topology(mesh));
        Assert.True(mesh.IsWatertight);
        Assert.Contains(top, mesh.Faces);
        Assert.Contains(bottom, mesh.Faces);
    }

    [Fact]
    public void EraseEdges_RemovesBothAdjacentFaces()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        MeshHalfEdge edge = top.HalfEdge;

        IReadOnlyList<MeshEditOps.FaceEraseRecord> records = MeshEditOps.EraseEdges(mesh, [edge]);

        Assert.Equal(2, records.Count);
        Assert.Equal(4, mesh.Faces.Count);
    }

    [Fact]
    public void EraseFacesCommand_UndoRedo_RestoresTopology()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var undo = new UndoStack();

        undo.Push(new EraseFacesCommand("Стирание граней (1)", mesh, [top]));
        Assert.Equal(5, mesh.Faces.Count);
        Assert.False(mesh.IsWatertight);

        undo.Undo();
        Assert.Equal(6, mesh.Faces.Count);
        Assert.True(mesh.IsWatertight);
        Assert.Contains(top, mesh.Faces);

        undo.Redo();
        Assert.Equal(5, mesh.Faces.Count);
    }

    [Fact]
    public void RemoveVertex_WithIncidentEdge_Throws()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        Assert.Throws<InvalidOperationException>(() => mesh.RemoveVertex(mesh.Vertices[0]));
    }

    [Fact]
    public void RemoveRestoreVertex_Roundtrip()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex vertex = mesh.AddVertex(new Vector3d(1, 2, 3));

        Assert.True(mesh.RemoveVertex(vertex));
        Assert.Empty(mesh.Vertices);
        Assert.False(mesh.RemoveVertex(vertex));

        mesh.RestoreVertex(vertex);
        Assert.Single(mesh.Vertices);
        Assert.Equal(new Vector3d(1, 2, 3), vertex.Position);
    }

    [Fact]
    public void PlanTransform_AppliesMapToSeedsAndGluedVertices()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex seed = mesh.AddVertex(new Vector3d(1, 0, 0));
        MeshVertex glued = mesh.AddVertex(new Vector3d(1, 0, 0));
        MeshVertex other = mesh.AddVertex(new Vector3d(5, 0, 0));

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanTransform(
            [seed],
            mesh.Vertices,
            point => point * 2,
            MeshEditOps.DefaultToleranceMm);

        Assert.Equal(2, moves.Count);
        Assert.Equal(new Vector3d(2, 0, 0), moves[0].To);
        Assert.Contains(moves, move => ReferenceEquals(move.Vertex, glued) && move.To == new Vector3d(2, 0, 0));
        Assert.DoesNotContain(moves, move => ReferenceEquals(move.Vertex, other));
    }

    [Fact]
    public void TryGetDihedralAngleDegrees_BoxEdge_IsNinety()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        Assert.True(MeshEditOps.TryGetDihedralAngleDegrees(top.HalfEdge, out double degrees));
        Assert.Equal(90, degrees, 6);
    }

    [Fact]
    public void TryGetDihedralAngleDegrees_BoundaryEdge_ReturnsFalse()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(1, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(0, 1, 0));
        MeshFace face = mesh.AddFace([a, b, c]);

        Assert.False(MeshEditOps.TryGetDihedralAngleDegrees(face.HalfEdge, out _));
    }

    [Fact]
    public void CollectEdges_Box_ReturnsTwelveUniqueEdges()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        IReadOnlyList<MeshHalfEdge> edges = MeshEditOps.CollectEdges(mesh.Faces);
        Assert.Equal(12, edges.Count);
    }

    [Fact]
    public void MeshTransforms_PreservesMaterialsAndEdgeFlags()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        mesh.Faces[0].MaterialIndex = 3;
        mesh.DefaultMaterialIndex = 2;
        mesh.Faces[1].HalfEdge.IsSmooth = true;
        mesh.Faces[1].HalfEdge.IsSoft = true;

        HalfEdgeMesh copy = MeshTransforms.Transformed(mesh, Transform3d.Identity);

        Assert.Equal(3, copy.Faces[0].MaterialIndex);
        Assert.Equal(2, copy.DefaultMaterialIndex);
        Assert.Same(mesh.Materials, copy.Materials);
        Assert.True(copy.Faces[1].HalfEdge.IsSmooth);
        Assert.True(copy.Faces[1].HalfEdge.IsSoft);
    }

    [Fact]
    public void OffsetFaceCommand_SplitsFaceAndInheritsMaterial()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        top.MaterialIndex = 5;
        var undo = new UndoStack();

        undo.Push(new OffsetFaceCommand(mesh, top, 2));

        Assert.Equal(10, mesh.Faces.Count);
        Assert.Equal(12, mesh.Vertices.Count);
        Assert.True(mesh.IsWatertight);

        // материал исходной грани наследуют все 5 новых граней (кольцо + внутренняя)
        foreach (MeshFace face in mesh.Faces.Skip(5))
        {
            Assert.Equal(5, face.MaterialIndex);
        }

        Assert.Equal(-1, mesh.Faces[0].MaterialIndex);
    }

    [Fact]
    public void OffsetFaceCommand_UndoRedo_RestoresIdentity()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        (int Vertices, int Edges, int Faces) before = ToolsTestSupport.Topology(mesh);
        var undo = new UndoStack();

        undo.Push(new OffsetFaceCommand(mesh, top, 2));
        undo.Undo();

        Assert.Equal(before, ToolsTestSupport.Topology(mesh));
        Assert.True(mesh.IsWatertight);
        Assert.Contains(top, mesh.Faces);

        undo.Redo();
        Assert.Equal(12, mesh.Vertices.Count);
        Assert.Equal(10, mesh.Faces.Count);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void OffsetFaceCommand_ZeroDistance_Throws()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OffsetFaceCommand(mesh, mesh.Faces[0], 0));
    }
}
