using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Рисование на эталонной сцене (задача 6.0): первый клик фиксирует плоскость
/// построения по грани под курсором — на плите это «земля» Z=0, и фигура должна
/// лечь строго в неё (не на наклонную плоскость и не в чужой меш).
/// </summary>
public sealed class ReferenceSceneDrawingTests
{
    [Fact]
    public void RectangleDrawnOnPlateTop_LiesOnGroundPlane()
    {
        var scene = new Scene();
        IReadOnlyList<MeshNode> nodes = ReferenceSceneFactory.AddToScene(scene);
        MeshNode plate = nodes.Single(node => node.Name == ReferenceSceneFactory.PlateNodeName);
        int verticesBefore = plate.Mesh.Vertices.Count;
        int facesBefore = plate.Mesh.Faces.Count;

        var context = new ToolContext(scene, new UndoStack());
        InferenceViewport viewport = InferenceViewport.CreatePerspective(
            new Vector3d(0, -400, 400),
            new Vector3d(0, 400, -400).Normalized(),
            Vector3d.UnitZ,
            Math.PI / 4,
            800,
            600);
        context.Viewport = viewport;

        var tool = new RectangleTool();
        tool.Activate(context);
        tool.OnPointer(PointerInput.Press(PixelOf(viewport, new Vector3d(-95, -30, 0)), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(PixelOf(viewport, new Vector3d(-70, 20, 0)), PointerButton.Left));

        // Фигура вписана в меш плиты (клик попал в её грань), а не в новый узел.
        Assert.Equal(3, scene.Root.Children.Count);
        Assert.Equal(verticesBefore + 4, plate.Mesh.Vertices.Count);
        Assert.Equal(facesBefore + 1, plate.Mesh.Faces.Count);
        foreach (MeshVertex vertex in plate.Mesh.Vertices.Skip(verticesBefore))
        {
            Assert.Equal(0, vertex.Position.Z, 9);
        }

        Vector3d normal = MeshNormals.ComputeFaceNormal(plate.Mesh.Faces[^1]);
        Assert.True(normal.Z > 0.99, $"Нормаль новой грани смотрит не вверх: {normal}");
    }

    private static Vector2d PixelOf(InferenceViewport viewport, Vector3d worldPoint)
        => viewport.TryProject(worldPoint)
            ?? throw new InvalidOperationException($"Точка {worldPoint} не проецируется в камеру.");
}
