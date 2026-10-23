using System;

namespace Spike.Gl;

/// <summary>Минимальная матричная математика 4×4 (column-major, как ждёт glUniformMatrix4fv).</summary>
internal static class Mat4
{
    public static float[] Perspective(float fovY, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovY / 2f);
        float nf = near - far;
        return new[]
        {
            f / aspect, 0f, 0f, 0f,
            0f, f, 0f, 0f,
            0f, 0f, (far + near) / nf, -1f,
            0f, 0f, (2f * far * near) / nf, 0f,
        };
    }

    public static float[] Translation(float x, float y, float z) => new[]
    {
        1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        x, y, z, 1f,
    };

    public static float[] RotationX(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new[]
        {
            1f, 0f, 0f, 0f,
            0f, c, s, 0f,
            0f, -s, c, 0f,
            0f, 0f, 0f, 1f,
        };
    }

    public static float[] RotationY(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new[]
        {
            c, 0f, -s, 0f,
            0f, 1f, 0f, 0f,
            s, 0f, c, 0f,
            0f, 0f, 0f, 1f,
        };
    }

    /// <summary>Умножение матриц: результат = a · b (column-major).</summary>
    public static float[] Multiply(float[] a, float[] b)
    {
        var r = new float[16];
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 4; row++)
            {
                float sum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    sum += a[(k * 4) + row] * b[(col * 4) + k];
                }

                r[(col * 4) + row] = sum;
            }
        }

        return r;
    }
}
