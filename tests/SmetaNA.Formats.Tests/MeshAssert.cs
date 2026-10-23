using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>Сравнение мешей для round-trip тестов: по углам треугольников с допуском.</summary>
internal static class MeshAssert
{
    /// <summary>
    /// Меши равны, если совпадают порядок треугольников и их углы
    /// с точностью до <paramref name="tolerance"/> (мм; DoD §5.4 — 0,01 мм).
    /// </summary>
    public static void EqualCorners(MeshData expected, MeshData actual, double tolerance)
    {
        Assert.Equal(0, expected.Indices.Count % 3);
        Assert.Equal(expected.Indices.Count, actual.Indices.Count);
        for (int i = 0; i < expected.Indices.Count; i++)
        {
            Vector3d e = expected.Positions[expected.Indices[i]];
            Vector3d a = actual.Positions[actual.Indices[i]];
            Assert.True(Math.Abs(e.X - a.X) <= tolerance, $"угол {i}: X {e.X} != {a.X}");
            Assert.True(Math.Abs(e.Y - a.Y) <= tolerance, $"угол {i}: Y {e.Y} != {a.Y}");
            Assert.True(Math.Abs(e.Z - a.Z) <= tolerance, $"угол {i}: Z {e.Z} != {a.Z}");
        }
    }

    /// <summary>
    /// Сравнение текстовых файлов форматов с нормализацией переводов строк:
    /// git autocrlf может хранить текстовые фикстуры с CRLF, тогда как экспорт
    /// детерминированно пишет LF. Строгая байтовая проверка применяется к binary STL.
    /// </summary>
    public static void EqualNormalizedText(byte[] expected, byte[] actual)
    {
        Assert.Equal(Normalize(expected), Normalize(actual));
        static string Normalize(byte[] bytes) =>
            System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Ориентированный объём меша (мм³ при базе в мм).</summary>
    public static double SignedVolume(MeshData mesh)    {
        double volume = 0;
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            Vector3d a = mesh.Positions[mesh.Indices[i]];
            Vector3d b = mesh.Positions[mesh.Indices[i + 1]];
            Vector3d c = mesh.Positions[mesh.Indices[i + 2]];
            volume += Vector3d.Dot(a, Vector3d.Cross(b, c)) / 6.0;
        }

        return volume;
    }
}
