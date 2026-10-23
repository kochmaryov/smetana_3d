using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.M6;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Вращение профиля вокруг оси (revolve, задача 5.2): точные объёмы тел
/// (конус, труба, четверть трубы) с учётом тесселяции, управление сегментами
/// через VCB, полный и частичный угол, undo/redo одной командой.
/// </summary>
public class M6RevolveToolTests
{
    private static readonly Vector3d Eye = new(5, -40, 25);
    private static readonly Vector3d Target = new(5, 5, 10);

    /// <summary>Прямоугольный треугольник в плоскости XZ: катет 10 по X, катет 20 по Z.</summary>
    private static HalfEdgeMesh TriangleProfile()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(10, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(0, 0, 20));
        mesh.AddFace([a, b, c]);
        return mesh;
    }

    /// <summary>Прямоугольник в плоскости XZ: x∈[10;20], z∈[0;30] (круговая труба).</summary>
    private static HalfEdgeMesh RectangleProfile()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(10, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(20, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(20, 0, 30));
        MeshVertex d = mesh.AddVertex(new Vector3d(10, 0, 30));
        mesh.AddFace([a, b, c, d]);
        return mesh;
    }

    private static (ToolContext Context, UndoStack Undo) Setup(HalfEdgeMesh profile)
    {
        (Scene scene, _) = ToolsTestSupport.SceneWith(profile, "Profile");
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(Eye, Target);
        return (context, undo);
    }

    /// <summary>Сценарий: профиль → точка оси 1 → точка оси 2 (ось Z).</summary>
    private static void PickProfileAndZAxis(ToolContext context, RevolveTool tool, Vector3d profilePoint)
    {
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, profilePoint),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 0, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 0, 15)),
            PointerButton.Left));
    }

    [Fact]
    public void Revolve_Cone_FullTurn_ExactVolume()
    {
        (ToolContext context, UndoStack undo) = Setup(TriangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(3, 0, 5));

        Assert.Equal(ToolValueKind.Angle, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(360));
        Assert.Equal(ToolValueKind.Count, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(24));

        Assert.Equal(2, context.Scene.Root.Children.Count);
        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);
        Assert.Equal(RevolveTool.NodeName, result.Name);

        // конус: основание — правильный 24-угольник радиуса 10, высота 20
        double baseArea = (24 / 2.0) * 10 * 10 * Math.Sin((2 * Math.PI) / 24);
        Assert.Equal(baseArea * 20 / 3, ToolsTestSupport.SignedVolume(result.Mesh), 6);
        Assert.True(result.Mesh.IsWatertight);

        // вершины на оси сварены между кольцами: 2 общие + 24 кольцевые
        Assert.Equal((26, 72, 48), ToolsTestSupport.Topology(result.Mesh));

        undo.Undo();
        Assert.Single(context.Scene.Root.Children);
        undo.Redo();
        Assert.Equal(2, context.Scene.Root.Children.Count);
    }

    [Fact]
    public void Revolve_Tube_FullTurn_ExactVolume()
    {
        (ToolContext context, UndoStack undo) = Setup(RectangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(15, 0, 15));

        Assert.True(tool.TryApplyValue(360));
        Assert.True(tool.TryApplyValue(24));

        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);

        // труба: полигонное кольцо (R=20, r=10) × высота 30
        double annulusArea = (24 / 2.0) * Math.Sin((2 * Math.PI) / 24) * ((20 * 20) - (10 * 10));
        Assert.Equal(annulusArea * 30, ToolsTestSupport.SignedVolume(result.Mesh), 6);
        Assert.True(result.Mesh.IsWatertight);

        // замкнутое тело без торцов — топология тора
        Assert.Equal((96, 192, 96), ToolsTestSupport.Topology(result.Mesh));
    }

    [Fact]
    public void Revolve_QuarterTube_PartialAngle()
    {
        (ToolContext context, UndoStack undo) = Setup(RectangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(15, 0, 15));

        Assert.True(tool.TryApplyValue(90));
        Assert.True(tool.TryApplyValue(4));

        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);

        // четверть трубы: сектор кольца 90° из 4 сегментов × высота 30, с торцами
        double step = Math.PI / 2 / 4;
        double sectorArea = (4 / 2.0) * Math.Sin(step) * ((20 * 20) - (10 * 10));
        Assert.Equal(sectorArea * 30, ToolsTestSupport.SignedVolume(result.Mesh), 6);
        Assert.True(result.Mesh.IsWatertight);
        Assert.Equal((20, 36, 18), ToolsTestSupport.Topology(result.Mesh));
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Revolve_SegmentsControlTessellation()
    {
        (ToolContext context, UndoStack undo) = Setup(RectangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);

        PickProfileAndZAxis(context, tool, new Vector3d(15, 0, 15));
        tool.TryApplyValue(90);
        tool.TryApplyValue(8);
        var coarse = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);
        Assert.Equal(4 * 8 + 2, coarse.Mesh.Faces.Count);

        // повторный запуск с другими сегментами
        PickProfileAndZAxis(context, tool, new Vector3d(15, 0, 15));
        tool.TryApplyValue(90);
        tool.TryApplyValue(16);
        var fine = Assert.IsType<MeshNode>(context.Scene.Root.Children[2]);
        Assert.Equal(4 * 16 + 2, fine.Mesh.Faces.Count);
    }

    [Fact]
    public void Revolve_MouseAngle_DefaultSegments()
    {
        (ToolContext context, UndoStack undo) = Setup(RectangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(15, 0, 15));

        // перетаскивание задаёт угол (опорное направление — от оси к центру профиля)
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 30, 5))));
        Assert.Equal(90, tool.AngleDegrees, 6);

        // клик фиксирует тело с сегментами по умолчанию
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 30, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Release(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 30, 5)),
            PointerButton.Left));

        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[1]);
        Assert.Equal(4 * RevolveBuilder.DefaultSegmentsFor(90) + 2, result.Mesh.Faces.Count);
    }

    [Fact]
    public void Revolve_EscapeCancelsWithoutChanges()
    {
        (ToolContext context, UndoStack undo) = Setup(TriangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(3, 0, 5));
        tool.TryApplyValue(360);
        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.Empty(tool.Profile);
        Assert.Single(context.Scene.Root.Children);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Revolve_ZeroAngle_DoesNotCommit()
    {
        (ToolContext context, UndoStack undo) = Setup(TriangleProfile());
        var tool = new RevolveTool();
        tool.Activate(context);
        PickProfileAndZAxis(context, tool, new Vector3d(3, 0, 5));

        Assert.False(tool.TryApplyValue(0));
        tool.OnKey(new KeyInput(ToolKey.Enter));
        Assert.Single(context.Scene.Root.Children);
        Assert.False(undo.CanUndo);
    }
}
