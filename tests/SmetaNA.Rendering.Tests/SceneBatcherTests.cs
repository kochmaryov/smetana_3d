using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class SceneBatcherTests
{
    private static RenderObject BoxObject(string name, Vector3d offset)
    {
        HalfEdgeMesh box = TestMeshes.Box();
        MeshData local = box.ToTriangles();
        var positions = new Vector3d[local.Positions.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = local.Positions[i] + offset;
        }

        var node = new MeshNode { Mesh = box, Name = name };
        var triangles = new MeshData(positions, [.. local.Indices]);
        return new RenderObject(
            node,
            node,
            triangles,
            EdgeExtractor.FromHalfEdgeMesh(box),
            BoundingBox3d.FromMeshData(triangles));
    }

    [Fact]
    public void Build_SingleObject_CountsAndOffsets()
    {
        RenderObject obj = BoxObject("a", Vector3d.Zero);

        RenderBatch batch = SceneBatcher.Build([obj]);

        Assert.Equal(8, batch.VertexCount);
        Assert.Equal(12, batch.TriangleCount);
        Assert.Equal(12, batch.EdgeCount);

        ObjectRange range = Assert.Single(batch.Ranges);
        Assert.Equal(0, range.VertexOffset);
        Assert.Equal(0, range.TriangleFirst);
        Assert.Equal(36, range.TriangleCount);
        Assert.Equal(24, range.EdgeCount);
    }

    [Fact]
    public void Build_TwoObjects_SecondRangeIsShifted()
    {
        RenderObject first = BoxObject("a", Vector3d.Zero);
        RenderObject second = BoxObject("b", new Vector3d(1000, 0, 0));

        RenderBatch batch = SceneBatcher.Build([first, second]);

        Assert.Equal(16, batch.VertexCount);
        Assert.Equal(24, batch.TriangleCount);
        Assert.Equal(24, batch.EdgeCount);
        Assert.Equal(2, batch.Ranges.Count);

        ObjectRange a = batch.Ranges[0];
        ObjectRange b = batch.Ranges[1];
        Assert.Equal(0, a.VertexOffset);
        Assert.Equal(8, b.VertexOffset);
        Assert.Equal(0, a.TriangleFirst);
        Assert.Equal(36, b.TriangleFirst);
        Assert.Equal(0, a.EdgeFirst);
        Assert.Equal(24, b.EdgeFirst);
    }

    [Fact]
    public void Build_IndicesAreGlobalAndInRange()
    {
        RenderObject first = BoxObject("a", Vector3d.Zero);
        RenderObject second = BoxObject("b", new Vector3d(1000, 0, 0));

        RenderBatch batch = SceneBatcher.Build([first, second]);

        foreach (uint index in batch.TriangleIndices.Concat(batch.EdgeIndices))
        {
            Assert.InRange(index, 0u, (uint)batch.VertexCount - 1);
        }
    }

    [Fact]
    public void Build_VertexPositionsAreWorldSpace()
    {
        RenderObject obj = BoxObject("a", new Vector3d(1000, 0, 0));

        RenderBatch batch = SceneBatcher.Build([obj]);

        // Вершина 0 куба (−50, −50, −50) со смещением по X.
        Assert.Equal(950f, batch.Vertices[0], 3);
        Assert.Equal(-50f, batch.Vertices[1], 3);
        Assert.Equal(-50f, batch.Vertices[2], 3);
    }
}
