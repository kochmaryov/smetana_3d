using SmetaNA.Core.Geometry;
using SmetaNA.Core.Materials;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Команды материалов: назначение, смена и снятие материала граней/объектов,
/// undo/redo (задача 5.4).
/// </summary>
public class MaterialCommandTests
{
    [Fact]
    public void SetFaceMaterialsCommand_AssignsAndChangesMaterial()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace face = mesh.Faces[0];
        var undo = new UndoStack();

        undo.Push(new SetFaceMaterialsCommand("Материал: Red",
        [
            new FaceMaterialAssignment(face, face.MaterialIndex, 1),
        ]));
        Assert.Equal(1, face.MaterialIndex);
        Assert.Equal(MaterialPalette.Red, MaterialResolver.Resolve(mesh, face));

        undo.Push(new SetFaceMaterialsCommand("Материал: Blue",
        [
            new FaceMaterialAssignment(face, face.MaterialIndex, 6),
        ]));
        Assert.Equal(6, face.MaterialIndex);
    }

    [Fact]
    public void SetFaceMaterialsCommand_Remove_ReturnsInheritedMaterial()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace face = mesh.Faces[0];
        mesh.DefaultMaterialIndex = 2;
        face.MaterialIndex = 5;
        var undo = new UndoStack();

        undo.Push(new SetFaceMaterialsCommand("Снятие материала",
        [
            new FaceMaterialAssignment(face, face.MaterialIndex, -1),
        ]));

        Assert.Equal(-1, face.MaterialIndex);
        Assert.Equal(MaterialPalette.Orange, MaterialResolver.Resolve(mesh, face));
    }

    [Fact]
    public void SetFaceMaterialsCommand_UndoRedo_RestoresAssignments()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace first = mesh.Faces[0];
        MeshFace second = mesh.Faces[1];
        first.MaterialIndex = 1;
        var undo = new UndoStack();

        undo.Push(new SetFaceMaterialsCommand("Материал: Red",
        [
            new FaceMaterialAssignment(first, first.MaterialIndex, 3),
            new FaceMaterialAssignment(second, second.MaterialIndex, 4),
        ]));
        Assert.Equal(3, first.MaterialIndex);
        Assert.Equal(4, second.MaterialIndex);

        undo.Undo();
        Assert.Equal(1, first.MaterialIndex);
        Assert.Equal(-1, second.MaterialIndex);

        undo.Redo();
        Assert.Equal(3, first.MaterialIndex);
        Assert.Equal(4, second.MaterialIndex);
    }

    [Fact]
    public void SetMeshMaterialCommand_AppliesToObjectAndUndoes()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        var undo = new UndoStack();

        undo.Push(new SetMeshMaterialCommand(mesh, 7));
        Assert.Equal(7, mesh.DefaultMaterialIndex);
        Assert.Equal(MaterialPalette.Purple, MaterialResolver.Resolve(mesh, mesh.Faces[0]));

        undo.Undo();
        Assert.Equal(-1, mesh.DefaultMaterialIndex);

        undo.Redo();
        Assert.Equal(7, mesh.DefaultMaterialIndex);
    }

    [Fact]
    public void ObjectPaint_Composite_UndoRestoresFacesAndObject()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace painted = mesh.Faces[0];
        painted.MaterialIndex = 5;
        var undo = new UndoStack();

        undo.Push(new CompositeEditCommand("Материал: Green",
        [
            new SetMeshMaterialCommand(mesh, 4),
            new SetFaceMaterialsCommand("Материал объекта",
            [
                new FaceMaterialAssignment(painted, painted.MaterialIndex, -1),
            ]),
        ]));

        Assert.Equal(4, mesh.DefaultMaterialIndex);
        Assert.Equal(-1, painted.MaterialIndex);

        undo.Undo();
        Assert.Equal(-1, mesh.DefaultMaterialIndex);
        Assert.Equal(5, painted.MaterialIndex);
    }
}
