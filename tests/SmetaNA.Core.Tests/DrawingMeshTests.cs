using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Топология рисования: проволочные рёбра, заполнение контура гранью, снятие
/// грани и удаление элементов — плюс команда истории <see cref="AddDrawingShapeCommand"/>
/// (execute/undo/redo).
/// </summary>
public class DrawingMeshTests
{
    private static (HalfEdgeMesh Mesh, MeshVertex[] Vertices) SquareWire()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex[] vertices =
        [
            mesh.AddVertex(new Vector3d(0, 0, 0)),
            mesh.AddVertex(new Vector3d(10, 0, 0)),
            mesh.AddVertex(new Vector3d(10, 10, 0)),
            mesh.AddVertex(new Vector3d(0, 10, 0)),
        ];
        return (mesh, vertices);
    }

    private static MeshHalfEdge[] WireLoop(HalfEdgeMesh mesh, MeshVertex[] vertices)
    {
        var forwards = new MeshHalfEdge[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            forwards[i] = mesh.AddWireEdge(vertices[i], vertices[(i + 1) % vertices.Length]).Forward;
        }

        return forwards;
    }

    [Fact]
    public void AddWireEdge_CreatesTwinPairWithCycles_NoFaces()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(5, 0, 0));

        (MeshHalfEdge forward, MeshHalfEdge reverse) = mesh.AddWireEdge(a, b);

        Assert.Equal(2, mesh.HalfEdges.Count);
        Assert.Empty(mesh.Faces);
        Assert.Same(reverse, forward.Twin);
        Assert.Same(forward, reverse.Twin);
        Assert.Same(reverse, forward.Next);
        Assert.Same(forward, reverse.Next);
        Assert.Equal(b, forward.Next!.Origin);
        Assert.Equal(a, reverse.Next!.Origin);
        Assert.Null(forward.Face);
        Assert.False(mesh.IsWatertight, "Проволочное ребро не делает меш замкнутым.");
    }

    [Fact]
    public void AddWireEdge_DuplicateDirectedEdge_Throws()
    {
        (HalfEdgeMesh mesh, MeshVertex[] vertices) = SquareWire();
        mesh.AddWireEdge(vertices[0], vertices[1]);

        Assert.Throws<InvalidOperationException>(() => mesh.AddWireEdge(vertices[0], vertices[1]));
        Assert.Throws<InvalidOperationException>(() => mesh.AddWireEdge(vertices[1], vertices[0]));
    }

    [Fact]
    public void FillLoop_MakesFace_AndKeepsReverseAsBoundary()
    {
        (HalfEdgeMesh mesh, MeshVertex[] vertices) = SquareWire();
        MeshHalfEdge[] ring = WireLoop(mesh, vertices);

        MeshFace face = mesh.FillLoop(ring);

        Assert.Single(mesh.Faces);
        Assert.Same(face, mesh.Faces[0]);
        Assert.Equal(4, mesh.GetRing(face).Count);
        Assert.All(ring, edge => Assert.Same(face, edge.Face));
        Assert.All(ring, edge => Assert.Null(edge.Twin!.Face));
        Assert.Same(ring[1], ring[0].Next);
        Assert.Same(ring[0], ring[1].Prev);
        MeshValidationResult validation = MeshValidator.Validate(mesh);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Defects);
    }

    [Fact]
    public void FillLoop_RejectsOpenOrTwistedRings()
    {
        (HalfEdgeMesh mesh, MeshVertex[] vertices) = SquareWire();
        MeshHalfEdge[] ring = WireLoop(mesh, vertices);

        Assert.Throws<ArgumentException>(() => mesh.FillLoop([ring[0], ring[1]]));
        Assert.Throws<ArgumentException>(() => mesh.FillLoop([ring[0], ring[2], ring[1], ring[3]]));
    }

    [Fact]
    public void DetachFace_RestoresWirePairs()
    {
        (HalfEdgeMesh mesh, MeshVertex[] vertices) = SquareWire();
        MeshHalfEdge[] ring = WireLoop(mesh, vertices);
        MeshFace face = mesh.FillLoop(ring);

        mesh.DetachFace(face);

        Assert.Empty(mesh.Faces);
        Assert.False(mesh.IsWatertight);
        foreach (MeshHalfEdge edge in ring)
        {
            Assert.Null(edge.Face);
            Assert.Same(edge.Twin, edge.Next);
            Assert.Same(edge.Twin, edge.Prev);
        }

        MeshValidationResult validation = MeshValidator.Validate(mesh);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void RemoveWireEdge_RemovesPair_AndVertexRemovalNeedsIsolation()
    {
        (HalfEdgeMesh mesh, MeshVertex[] vertices) = SquareWire();
        MeshHalfEdge[] ring = WireLoop(mesh, vertices);

        Assert.Throws<ArgumentException>(() => mesh.RemoveVertex(vertices[0]));
        mesh.RemoveWireEdge(ring[0]);
        Assert.Equal(6, mesh.HalfEdges.Count);
        mesh.RemoveWireEdge(ring[1]);
        mesh.RemoveWireEdge(ring[2]);
        mesh.RemoveWireEdge(ring[3]);
        Assert.Empty(mesh.HalfEdges);
        foreach (MeshVertex vertex in vertices)
        {
            mesh.RemoveVertex(vertex);
        }

        Assert.Empty(mesh.Vertices);
    }

    [Fact]
    public void AddDrawingShapeCommand_OpenPolyline_ExecuteUndoRedo()
    {
        var mesh = new HalfEdgeMesh();
        DrawingShapeSpec spec = DrawingShapeSpec.Open(
        [
            new Vector3d(0, 0, 0),
            new Vector3d(10, 0, 0),
            new Vector3d(10, 10, 0),
        ]);
        var command = new AddDrawingShapeCommand(mesh, spec, "Линия");

        command.Execute();
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(4, mesh.HalfEdges.Count);
        Assert.Empty(mesh.Faces);

        command.Undo();
        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.HalfEdges);

        command.Execute();
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(4, mesh.HalfEdges.Count);
    }

    [Fact]
    public void AddDrawingShapeCommand_ClosedShape_UndoRemovesFaceAndWires()
    {
        var mesh = new HalfEdgeMesh();
        DrawingShapeSpec spec = DrawingShapeSpec.Closed(
        [
            new Vector3d(0, 0, 0),
            new Vector3d(10, 0, 0),
            new Vector3d(10, 10, 0),
            new Vector3d(0, 10, 0),
        ]);
        var command = new AddDrawingShapeCommand(mesh, spec, "Прямоугольник");

        command.Execute();
        Assert.Single(mesh.Faces);
        Assert.Equal(8, mesh.HalfEdges.Count);

        command.Undo();
        Assert.Empty(mesh.Faces);
        Assert.Empty(mesh.HalfEdges);
        Assert.Empty(mesh.Vertices);
    }

    [Fact]
    public void AddDrawingShapeCommand_RejectsDegenerateSpecs()
    {
        var mesh = new HalfEdgeMesh();

        Assert.Throws<ArgumentException>(() => new AddDrawingShapeCommand(
            mesh,
            DrawingShapeSpec.Open([new Vector3d(0, 0, 0)]),
            "Линия"));
        Assert.Throws<ArgumentException>(() => new AddDrawingShapeCommand(
            mesh,
            DrawingShapeSpec.Open([new Vector3d(0, 0, 0), new Vector3d(0, 0, 0)]),
            "Линия"));
        Assert.Throws<ArgumentException>(() => new AddDrawingShapeCommand(
            mesh,
            new DrawingShapeSpec([new DrawingContour(
                [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(2, 0, 0)],
                IsClosed: true,
                CreateFace: true)]),
            "Грань"));
    }

    [Fact]
    public void AddDrawingShapeCommand_ExecuteTwice_Throws()
    {
        var mesh = new HalfEdgeMesh();
        var command = new AddDrawingShapeCommand(
            mesh,
            DrawingShapeSpec.Open([new Vector3d(0, 0, 0), new Vector3d(5, 0, 0)]),
            "Линия");

        command.Execute();
        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void BuiltContour_ExposesCreatedElements()
    {
        var mesh = new HalfEdgeMesh();
        var command = new AddDrawingShapeCommand(
            mesh,
            DrawingShapeSpec.Closed(
            [
                new Vector3d(0, 0, 0),
                new Vector3d(4, 0, 0),
                new Vector3d(4, 4, 0),
            ]),
            "Треугольник");

        command.Execute();

        AddDrawingShapeCommand.BuiltContour built = Assert.Single(command.Built);
        Assert.Equal(3, built.Vertices.Count);
        Assert.Equal(3, built.ForwardEdges.Count);
        Assert.NotNull(built.Face);
    }
}
