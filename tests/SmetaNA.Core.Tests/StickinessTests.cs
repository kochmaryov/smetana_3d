using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Stickiness (склеивание) — ключевое поведение SketchUp: при деформации грани
/// прилегающие грани/рёбра остаются склеенными с деформируемой геометрией.
/// Сценарии: прилегающая грань, угловое ребро (в т.ч. между объектами),
/// позиционное склеивание несваренной геометрии.
/// </summary>
public class StickinessTests
{
    [Fact]
    public void AdjacentFaces_StretchWithPushedFace()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        IReadOnlyList<MeshFace> adjacent = mesh.GetAdjacentFaces(top);
        Assert.Equal(4, adjacent.Count);

        var undo = new UndoStack();
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 5);
        undo.Push(new MoveVerticesCommand("Push/Pull 5 mm", moves));

        foreach (MeshFace side in adjacent)
        {
            // прилегающая грань растянута: общее ребро ушло вместе с верхней,
            // нижнее ребро осталось; нормаль не изменилась (грань осталась плоской)
            Vector3d normalBefore = MeshNormals.ComputeFaceNormal(side).Normalized();
            IReadOnlyList<MeshVertex> ring = mesh.GetRing(side);
            int moved = ring.Count(vertex => Math.Abs(vertex.Position.Z - 15) < 1e-9);
            int fixedCount = ring.Count(vertex => Math.Abs(vertex.Position.Z) < 1e-9);
            Assert.Equal(2, moved);
            Assert.Equal(2, fixedCount);
            Assert.True(ToolsTestSupport.NearlyEqual(normalBefore, MeshNormals.ComputeFaceNormal(side).Normalized()));
        }

        Assert.True(mesh.IsWatertight);
        Assert.Equal(10 * 10 * 15, ToolsTestSupport.SignedVolume(mesh), 6);
    }

    [Fact]
    public void CornerEdge_StaysClosed_OnStepSolid()
    {
        // L-образная ступенька: грань-подъём (нормаль +Y) и примыкающая грань (+X)
        // встречаются в угловом ребре (5, 5, z)
        HalfEdgeMesh mesh = ToolsTestSupport.Prism(ToolsTestSupport.StepFootprint, 10);
        MeshFace riser = mesh.Faces.First(face =>
            ToolsTestSupport.NearlyEqual(MeshEditOps.FaceCentroid(face), new Vector3d(7.5, 5, 5)));
        MeshFace sideX = mesh.Faces.First(face =>
            ToolsTestSupport.NearlyEqual(MeshEditOps.FaceCentroid(face), new Vector3d(5, 7.5, 5)));
        Assert.Equal(4, mesh.GetAdjacentFaces(riser).Count);

        var undo = new UndoStack();
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, riser, 4);
        undo.Push(new MoveVerticesCommand("Push/Pull 4 mm", moves));

        // угол остаётся замкнутым: общие вершины углового ребра перенесены одинаково
        Assert.True(mesh.IsWatertight);
        foreach (MeshVertex vertex in mesh.GetRing(sideX))
        {
            // вершины углового ребра перенесены (y = 5 → 9), дальние остались (y = 10)
            bool moved = Math.Abs(vertex.Position.Y - 9) < 1e-9;
            bool fixedVertex = Math.Abs(vertex.Position.Y - 10) < 1e-9;
            Assert.True(moved || fixedVertex);
        }

        Assert.Equal(2, mesh.GetRing(sideX).Count(vertex => Math.Abs(vertex.Position.Y - 9) < 1e-9));

        // объём вырос на площадь грани-подъёма × расстояние (5 × 10 × 4)
        Assert.Equal((75 * 10) + (5 * 10 * 4), ToolsTestSupport.SignedVolume(mesh), 6);
    }

    [Fact]
    public void CornerEdge_StaysGlued_AcrossMeshes()
    {
        // два куба касаются угловым ребром x=10, y=10 (общая позиция вершин)
        HalfEdgeMesh boxA = ToolsTestSupport.Box(10, 10, 10);
        HalfEdgeMesh boxB = ToolsTestSupport.Box(10, 10, 10, new Vector3d(10, 10, 0));
        var scene = new Scene();
        var nodeA = new MeshNode { Mesh = boxA, Name = "A" };
        var nodeB = new MeshNode { Mesh = boxB, Name = "B" };
        scene.Root.AddChild(nodeA);
        scene.Root.AddChild(nodeB);

        MeshVertex cornerA = boxA.Vertices.First(v => ToolsTestSupport.NearlyEqual(v.Position, new Vector3d(10, 10, 10)));
        MeshVertex cornerB = boxB.Vertices.First(v => ToolsTestSupport.NearlyEqual(v.Position, new Vector3d(10, 10, 10)));

        MeshFace topA = ToolsTestSupport.FaceWithNormal(boxA, Vector3d.UnitZ);
        var undo = new UndoStack();
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(
            boxA,
            topA,
            5,
            boxA.Vertices.Concat(boxB.Vertices));
        undo.Push(new MoveVerticesCommand("Push/Pull 5 mm", moves));

        // склеенная вершина соседнего куба ушла вместе с деформируемой — угол замкнут
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(10, 10, 15), cornerA.Position));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(10, 10, 15), cornerB.Position));
        Assert.Equal(0, (cornerA.Position - cornerB.Position).Length(), 9);
        Assert.True(boxA.IsWatertight);
        Assert.True(boxB.IsWatertight);
    }

    [Fact]
    public void Tool_PushPull_PullsCoincidentVerticesOfNeighbourMesh()
    {
        HalfEdgeMesh boxA = ToolsTestSupport.Box(10, 10, 10);
        HalfEdgeMesh boxB = ToolsTestSupport.Box(10, 10, 10, new Vector3d(10, 10, 0));
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = boxA, Name = "A" });
        var nodeB = new MeshNode { Mesh = boxB, Name = "B" };
        scene.Root.AddChild(nodeB);

        MeshVertex cornerB = boxB.Vertices.First(v => ToolsTestSupport.NearlyEqual(v.Position, new Vector3d(10, 10, 10)));
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);

        // вид сверху (вдоль −Z): направление взгляда не параллельно «верху» камеры
        context.Viewport = InferenceViewport.CreatePerspective(
            new Vector3d(5, 5, 50), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), Math.PI / 4, 800, 600);
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left)));
        Assert.True(tool.TryApplyValue(5));

        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(10, 10, 15), cornerB.Position));
        Assert.Equal(10 * 10 * 15, ToolsTestSupport.SignedVolume(boxA), 6);
        Assert.True(boxA.IsWatertight);
        Assert.True(boxB.IsWatertight);
    }

    [Fact]
    public void UnweldedCoincidentVertices_MoveTogether()
    {
        // соседняя грань не делит вершины с деформируемой (несваренная геометрия) —
        // позиционное склеивание всё равно тянет её общее ребро
        var mesh = new HalfEdgeMesh();
        MeshVertex a0 = mesh.AddVertex(new Vector3d(0, 0, 10));
        MeshVertex a1 = mesh.AddVertex(new Vector3d(10, 0, 10));
        MeshVertex a2 = mesh.AddVertex(new Vector3d(10, 10, 10));
        MeshVertex a3 = mesh.AddVertex(new Vector3d(0, 10, 10));
        MeshFace top = mesh.AddFace([a0, a1, a2, a3]);

        MeshVertex b0 = mesh.AddVertex(new Vector3d(0, 10, 10));  // дубль a3
        MeshVertex b1 = mesh.AddVertex(new Vector3d(10, 10, 10)); // дубль a2
        MeshVertex b2 = mesh.AddVertex(new Vector3d(10, 10, 0));
        MeshVertex b3 = mesh.AddVertex(new Vector3d(0, 10, 0));
        mesh.AddFace([b0, b1, b2, b3]);

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 5);
        MeshEditOps.Apply(moves);

        Assert.Equal(new Vector3d(0, 10, 15), b0.Position);
        Assert.Equal(new Vector3d(10, 10, 15), b1.Position);
        Assert.Equal(new Vector3d(10, 10, 0), b2.Position);
        Assert.Equal(new Vector3d(0, 10, 0), b3.Position);
    }

    [Fact]
    public void DependentFaces_List_TracksCascade()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        IReadOnlyList<MeshFace> dependents = MeshEditOps.CollectDependentFaces(mesh, mesh.GetRing(top));

        Assert.Equal(5, dependents.Count); // верхняя + четыре прилегающие
    }
}
