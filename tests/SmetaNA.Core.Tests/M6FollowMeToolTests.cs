using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.M6;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Follow Me (задача 5.2): выдавливание профиля по траектории из рёбер —
/// прямая и ломаная (точные объёмы), замкнутый путь (топология тора), цепочка
/// рёбер через ToolInput, undo/redo одной командой.
/// </summary>
public class M6FollowMeToolTests
{
    private static readonly Vector3d Eye = new(5, -40, 25);
    private static readonly Vector3d Target = new(5, 5, 10);

    /// <summary>Профиль: квадрат 10×10 в плоскости XZ (y=0).</summary>
    private static HalfEdgeMesh ProfileMesh(double size = 10)
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(size, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(size, 0, size));
        MeshVertex d = mesh.AddVertex(new Vector3d(0, 0, size));
        mesh.AddFace([a, b, c, d]);
        return mesh;
    }

    private static (ToolContext Context, UndoStack Undo) Setup(HalfEdgeMesh profile, HalfEdgeMesh path)
    {
        (Scene scene, _) = ToolsTestSupport.SceneWith(profile, "Profile");
        var pathNode = new MeshNode { Mesh = path, Name = "Path" };
        scene.Root.AddChild(pathNode);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(Eye, Target);
        return (context, undo);
    }

    private static HalfEdgeMesh WirePath(params Vector3d[] points)
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex[] vertices = [.. points.Select(mesh.AddVertex)];
        for (int i = 0; i + 1 < vertices.Length; i++)
        {
            mesh.AddWireEdge(vertices[i], vertices[i + 1]);
        }

        return mesh;
    }

    [Fact]
    public void FollowMe_StraightPath_BoxProfile_ExactVolume()
    {
        HalfEdgeMesh path = WirePath(new Vector3d(0, 0, 0), new Vector3d(0, 30, 0));
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        // клик по грани-профилю, затем по ребру траектории, Enter
        Assert.True(tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5)),
            PointerButton.Left)));
        Assert.True(tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 15, 0)),
            PointerButton.Left)));
        Assert.Equal(2, tool.Path.Count);
        tool.OnKey(new KeyInput(ToolKey.Enter));

        Assert.Equal(3, context.Scene.Root.Children.Count);
        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[2]);
        Assert.Equal(FollowMeTool.NodeName, result.Name);

        // призма 10×10×30: точный объём, замкнутость, топология коробки
        Assert.Equal(3000, ToolsTestSupport.SignedVolume(result.Mesh), 6);
        Assert.True(result.Mesh.IsWatertight);
        Assert.Equal((8, 12, 6), ToolsTestSupport.Topology(result.Mesh));

        undo.Undo();
        Assert.Equal(2, context.Scene.Root.Children.Count);
        undo.Redo();
        Assert.Equal(3, context.Scene.Root.Children.Count);
    }

    [Fact]
    public void FollowMe_TwoSegmentPath_ExactVolume()
    {
        HalfEdgeMesh path = WirePath(
            new Vector3d(0, 0, 0),
            new Vector3d(0, 15, 0),
            new Vector3d(0, 30, 0));
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 7, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 22, 0)),
            PointerButton.Left));
        Assert.Equal(3, tool.Path.Count);
        tool.OnKey(new KeyInput(ToolKey.Enter));

        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[2]);
        Assert.Equal(3000, ToolsTestSupport.SignedVolume(result.Mesh), 6);
        Assert.True(result.Mesh.IsWatertight);
        Assert.Equal((12, 20, 10), ToolsTestSupport.Topology(result.Mesh));
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void FollowMe_ClosedPath_TorusTopology()
    {
        HalfEdgeMesh path = WirePath(
            new Vector3d(0, 0, 0),
            new Vector3d(30, 0, 0),
            new Vector3d(30, 30, 0),
            new Vector3d(0, 30, 0),
            new Vector3d(0, 0, 0));
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(4), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 0, 2)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(15, 0, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(30, 15, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(15, 30, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 15, 0)),
            PointerButton.Left));

        Assert.True(tool.IsPathClosed);
        Assert.Equal(5, tool.Path.Count);
        tool.OnKey(new KeyInput(ToolKey.Enter));

        var result = Assert.IsType<MeshNode>(context.Scene.Root.Children[2]);
        Assert.True(result.Mesh.IsWatertight);

        // тело по замкнутой траектории — топология тора: V−E+F = 0
        (int vertices, int edges, int faces) = ToolsTestSupport.Topology(result.Mesh);
        Assert.Equal((16, 32, 16), (vertices, edges, faces));
        Assert.Equal(0, vertices - edges + faces);
        Assert.InRange(ToolsTestSupport.SignedVolume(result.Mesh), 1, 4 * 30 * 16);
    }

    [Fact]
    public void FollowMe_NonAdjacentEdge_IsIgnored()
    {
        HalfEdgeMesh path = WirePath(new Vector3d(0, 0, 0), new Vector3d(0, 15, 0));
        MeshVertex farA = path.AddVertex(new Vector3d(50, 0, 0));
        MeshVertex farB = path.AddVertex(new Vector3d(50, 10, 0));
        path.AddWireEdge(farA, farB);
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 7, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(50, 5, 0)),
            PointerButton.Left));

        // несвязанное ребро не добавляется в цепочку
        Assert.Equal(2, tool.Path.Count);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void FollowMe_DeleteRemovesLastEdge()
    {
        HalfEdgeMesh path = WirePath(
            new Vector3d(0, 0, 0),
            new Vector3d(0, 15, 0),
            new Vector3d(0, 30, 0));
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 7, 0)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 22, 0)),
            PointerButton.Left));
        Assert.Equal(3, tool.Path.Count);
        tool.OnKey(new KeyInput(ToolKey.Delete));
        Assert.Equal(2, tool.Path.Count);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void FollowMe_EscapeCancelsWithoutChanges()
    {
        HalfEdgeMesh path = WirePath(new Vector3d(0, 0, 0), new Vector3d(0, 30, 0));
        (ToolContext context, UndoStack undo) = Setup(ProfileMesh(), path);
        var tool = new FollowMeTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 0, 5)),
            PointerButton.Left));
        tool.OnPointer(PointerInput.Press(
            ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(0, 15, 0)),
            PointerButton.Left));
        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.Empty(tool.Profile);
        Assert.Equal(2, context.Scene.Root.Children.Count);
        Assert.False(undo.CanUndo);
    }
}
