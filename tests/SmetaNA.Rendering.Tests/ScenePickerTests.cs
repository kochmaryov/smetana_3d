using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class ScenePickerTests
{
    private static RenderObject BoxObject(SceneNode node, Vector3d center, double size = 100)
    {
        HalfEdgeMesh box = TestMeshes.Box(size);
        MeshData local = box.ToTriangles();
        var positions = new Vector3d[local.Positions.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = local.Positions[i] + center;
        }

        var triangles = new MeshData(positions, [.. local.Indices]);
        return new RenderObject(
            node,
            node,
            triangles,
            EdgeExtractor.FromHalfEdgeMesh(box),
            BoundingBox3d.FromMeshData(triangles));
    }

    [Fact]
    public void Pick_NearestObjectWins()
    {
        var nearNode = new GroupNode { Name = "near" };
        var farNode = new GroupNode { Name = "far" };
        var near = BoxObject(nearNode, new Vector3d(0, 0, 0));
        var far = BoxObject(farNode, new Vector3d(0, 500, 0));
        var ray = new Ray3d(new Vector3d(0, -200, 0), new Vector3d(0, 1, 0));

        PickResult? hit = ScenePicker.Pick([far, near], ray);

        Assert.NotNull(hit);
        Assert.Same(nearNode, hit!.Object.SelectionNode);
        Assert.Equal(150, hit.Distance, 6);
    }

    [Fact]
    public void Pick_Miss_ReturnsNull()
    {
        var node = new GroupNode();
        var obj = BoxObject(node, Vector3d.Zero);
        var ray = new Ray3d(new Vector3d(500, -200, 0), new Vector3d(0, 1, 0));

        Assert.Null(ScenePicker.Pick([obj], ray));
    }

    [Fact]
    public void Pick_RespectsMaxDistance()
    {
        var node = new GroupNode();
        var obj = BoxObject(node, Vector3d.Zero);
        var ray = new Ray3d(new Vector3d(0, -200, 0), new Vector3d(0, 1, 0));

        Assert.Null(ScenePicker.Pick([obj], ray, maxDistance: 100));
        Assert.NotNull(ScenePicker.Pick([obj], ray, maxDistance: 1000));
    }

    [Fact]
    public void PickObject_ReturnsTriangleAndPoint()
    {
        var node = new GroupNode();
        var obj = BoxObject(node, Vector3d.Zero);
        var ray = new Ray3d(new Vector3d(0, -200, 0), new Vector3d(0, 1, 0));

        PickResult? hit = ScenePicker.PickObject(obj, ray);

        Assert.NotNull(hit);
        Assert.InRange(hit!.TriangleIndex, 0, obj.TriangleCount - 1);
        Assert.Equal(-50, hit.Point.Y, 6); // ближняя грань куба
    }
}
