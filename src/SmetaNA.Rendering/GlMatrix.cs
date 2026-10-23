using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Перенос <see cref="Transform3d"/> (row-vector-раскладка, v' = v · M) в
/// column-major float[16] для glUniformMatrix4fv. GL считает v' = Mgl · v, поэтому
/// в Mgl кладётся транспонированная матрица, а column-major-раскладка транспоната —
/// это просто row-major-раскладка исходной матрицы.
/// </summary>
public static class GlMatrix
{
    public static float[] ToGlMatrix(Transform3d m) =>
    [
        (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
        (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
        (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
        (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44,
    ];
}
