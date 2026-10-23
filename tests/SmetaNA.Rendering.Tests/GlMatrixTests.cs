using SmetaNA.Core.Geometry;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Раскладка матриц для GL: column-major транспоната row-vector-матрицы —
/// это row-major-раскладка исходной (см. <see cref="GlMatrix"/>).
/// </summary>
public sealed class GlMatrixTests
{
    [Fact]
    public void ToGlMatrix_Identity_StaysIdentity()
    {
        float[] m = GlMatrix.ToGlMatrix(Transform3d.Identity);

        Assert.Equal(16, m.Length);
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(i % 5 == 0 ? 1f : 0f, m[i]);
        }
    }

    [Fact]
    public void ToGlMatrix_Translation_LandsInLastColumn()
    {
        float[] m = GlMatrix.ToGlMatrix(Transform3d.CreateTranslation(new Vector3d(7, 8, 9)));

        // column-major: трансляция — индексы 12..14.
        Assert.Equal(7f, m[12]);
        Assert.Equal(8f, m[13]);
        Assert.Equal(9f, m[14]);
        Assert.Equal(1f, m[15]);
    }

    [Fact]
    public void ToGlMatrix_MatchesRowMajorFlatten()
    {
        var transform = new Transform3d(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        float[] m = GlMatrix.ToGlMatrix(transform);

        float[] expected = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Assert.Equal(expected, m);
    }
}
