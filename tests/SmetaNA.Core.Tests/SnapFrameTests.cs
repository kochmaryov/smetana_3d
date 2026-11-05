using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W5: кадр пользовательской точки сборки — нормализация, соединение
/// «лицевая к лицевой» и преобразование мировых координат.
/// </summary>
public sealed class SnapFrameTests
{
    [Fact]
    public void TryCreate_NormalizesVectors_AndOrthogonalizesUp()
    {
        Assert.True(SnapFrame.TryCreate(
            new Vector3d(1, 2, 3),
            new Vector3d(0, 0, 5),
            new Vector3d(1, 1, 4),
            out SnapFrame frame));

        Assert.Equal(1, frame.Direction.Length(), 12);
        Assert.Equal(1, frame.Up.Length(), 12);
        Assert.Equal(0, Vector3d.Dot(frame.Direction, frame.Up), 12);
        Assert.Equal(3, frame.Position.Z, 12);
    }

    [Fact]
    public void TryCreate_ParallelUp_FallsBackToAnotherAxis()
    {
        Assert.True(SnapFrame.TryCreate(Vector3d.Zero, Vector3d.UnitZ, Vector3d.UnitZ, out SnapFrame frame));

        Assert.Equal(0, Vector3d.Dot(frame.Direction, frame.Up), 12);
        Assert.Equal(1, frame.Up.Length(), 12);
    }

    [Fact]
    public void TryCreate_RejectsZeroDirection()
    {
        Assert.False(SnapFrame.TryCreate(Vector3d.Zero, Vector3d.Zero, Vector3d.UnitY, out _));
    }

    [Fact]
    public void Right_IsRightHandedBasis()
    {
        SnapFrame frame = new(Vector3d.Zero, Vector3d.UnitZ, Vector3d.UnitY);

        InferenceAssert.DirectionEqual(Vector3d.UnitX, frame.Right);
    }

    [Fact]
    public void FrontToFront_MirrorsTargetDirection_KeepsUp()
    {
        SnapFrame moving = new(new Vector3d(1, 1, 1), Vector3d.UnitZ, Vector3d.UnitY);
        SnapFrame target = new(new Vector3d(10, 0, 0), Vector3d.UnitY, Vector3d.UnitX);

        SnapFrame attached = SnapFrame.FrontToFront(moving, target);

        InferenceAssert.DirectionEqual(-Vector3d.UnitY, attached.Direction);
        InferenceAssert.DirectionEqual(Vector3d.UnitX, attached.Up);
        InferenceAssert.PointEqual(target.Position, attached.Position);
    }

    [Fact]
    public void Transformed_AppliesRotationAndTranslation()
    {
        SnapFrame frame = new(new Vector3d(1, 0, 0), Vector3d.UnitZ, Vector3d.UnitY);
        // x' = -y, y' = x (поворот 90° вокруг Z), z' = z + 5
        var world = new Transform3d(
            0, 1, 0, 0,
            -1, 0, 0, 0,
            0, 0, 1, 0,
            0, 0, 5, 1);

        SnapFrame transformed = frame.Transformed(world);

        InferenceAssert.DirectionEqual(Vector3d.UnitZ, transformed.Direction);
        InferenceAssert.DirectionEqual(Vector3d.UnitY, transformed.Right);
        Assert.Equal(5, transformed.Position.Z, 12);
        Assert.Equal(1, transformed.Position.Y, 12);
    }

    [Fact]
    public void ToTransform_MapsLocalAxesBackToFrame()
    {
        Assert.True(SnapFrame.TryCreate(new Vector3d(5, 6, 7), Vector3d.UnitX, Vector3d.UnitZ, out SnapFrame frame));
        Transform3d transform = frame.ToTransform();

        InferenceAssert.PointEqual(frame.Position, transform.TransformPoint(Vector3d.Zero));
        InferenceAssert.DirectionEqual(frame.Right, transform.TransformVector(Vector3d.UnitX));
        InferenceAssert.DirectionEqual(frame.Up, transform.TransformVector(Vector3d.UnitY));
        InferenceAssert.DirectionEqual(frame.Direction, transform.TransformVector(Vector3d.UnitZ));
    }
}
