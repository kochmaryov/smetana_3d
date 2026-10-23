using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Tools.M6;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Математика M6: мировые трансформации узлов (композиция предков), отражение
/// точки, шаги и нормализация массивов.
/// </summary>
public class M6MathTests
{
    [Fact]
    public void WorldTransformOf_ComposesLocalBeforeAncestors()
    {
        var group = new GroupNode
        {
            LocalTransform = EditingTransforms.CreateRotation(Vector3d.UnitZ, Vector3d.Zero, Math.PI / 2),
        };
        var scene = new Scene();
        scene.Root.AddChild(group);
        var node = new MeshNode
        {
            Mesh = ToolsTestSupport.Box(1, 1, 1),
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(10, 0, 0)),
        };
        group.AddChild(node);

        // (1;0;0) → сдвиг узла на 10 по X → поворот группы: (11;0;0) → (0;11;0)
        Vector3d point = M6Transforms.WorldTransformOf(node).TransformPoint(new Vector3d(1, 0, 0));
        Assert.Equal(0, point.X, 9);
        Assert.Equal(11, point.Y, 9);
        Assert.Equal(0, point.Z, 9);
    }

    [Fact]
    public void CreateReflection_MapsPointAcrossPlane()
    {
        Transform3d mirror = M6Transforms.CreateReflection(new Vector3d(5, 0, 0), Vector3d.UnitX);

        Vector3d mapped = mirror.TransformPoint(new Vector3d(8, 3, -2));
        Assert.Equal(2, mapped.X, 9);
        Assert.Equal(3, mapped.Y, 9);
        Assert.Equal(-2, mapped.Z, 9);

        // точка на плоскости неподвижна
        Vector3d onPlane = mirror.TransformPoint(new Vector3d(5, 7, 9));
        Assert.Equal(5, onPlane.X, 9);
        Assert.Equal(7, onPlane.Y, 9);
    }

    [Fact]
    public void TryComposeCopyTransform_MovesWorldPositionByDelta()
    {
        Transform3d parentWorld = EditingTransforms.CreateRotation(Vector3d.UnitZ, Vector3d.Zero, Math.PI / 2);
        Transform3d nodeWorld = parentWorld;
        Transform3d delta = Transform3d.CreateTranslation(new Vector3d(25, 0, 0));

        Assert.True(M6Transforms.TryComposeCopyTransform(nodeWorld, parentWorld, delta, out Transform3d newLocal));
        Vector3d worldPoint = Transform3d.Multiply(newLocal, parentWorld).TransformPoint(new Vector3d(1, 2, 3));
        Vector3d expected = parentWorld.TransformPoint(new Vector3d(1, 2, 3)) + new Vector3d(25, 0, 0);
        Assert.Equal(expected.X, worldPoint.X, 9);
        Assert.Equal(expected.Y, worldPoint.Y, 9);
        Assert.Equal(expected.Z, worldPoint.Z, 9);
    }

    [Fact]
    public void NormalizeCount_RoundsAndClamps()
    {
        Assert.Equal(2, ArrayMath.NormalizeCount(2.4));
        Assert.Equal(3, ArrayMath.NormalizeCount(2.5));
        Assert.Equal(1, ArrayMath.NormalizeCount(0.6));
        Assert.Equal(ArrayMath.MaxCount, ArrayMath.NormalizeCount(1e9));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArrayMath.NormalizeCount(double.NaN));
    }

    [Fact]
    public void LinearSteps_AreCumulativeTranslations()
    {
        IReadOnlyList<Transform3d> steps = ArrayMath.LinearSteps(new Vector3d(10, 0, 0), 3);
        Assert.Equal(3, steps.Count);
        Assert.Equal(new Vector3d(10, 0, 0), steps[0].TransformPoint(Vector3d.Zero));
        Assert.Equal(new Vector3d(30, 0, 0), steps[2].TransformPoint(Vector3d.Zero));
    }

    [Fact]
    public void PolarSteps_RotateAroundAxisThroughPivot()
    {
        IReadOnlyList<Transform3d> steps = ArrayMath.PolarSteps(
            Vector3d.UnitZ,
            new Vector3d(5, 5, 0),
            90,
            2);

        Vector3d first = steps[0].TransformPoint(new Vector3d(10, 5, 0));
        Assert.Equal(5, first.X, 9);
        Assert.Equal(10, first.Y, 9);
        Vector3d second = steps[1].TransformPoint(new Vector3d(10, 5, 0));
        Assert.Equal(0, second.X, 9);
        Assert.Equal(5, second.Y, 9);
    }

    [Fact]
    public void MirroredMesh_KeepsOutwardNormals()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 10, 10);
        Transform3d mirror = M6Transforms.CreateReflection(new Vector3d(0, 0, 0), Vector3d.UnitX);
        HalfEdgeMesh mirrored = M6NodeCloner.MirroredMesh(box, mirror.TransformPoint);

        Assert.Equal(ToolsTestSupport.Topology(box), ToolsTestSupport.Topology(mirrored));
        Assert.Equal(1000, ToolsTestSupport.SignedVolume(mirrored), 6);
    }
}
