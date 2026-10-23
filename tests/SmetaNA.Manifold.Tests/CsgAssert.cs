using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Manifold.Tests;

/// <summary>Метрики меша для фузи-инвариантов CSG.</summary>
internal sealed record MeshMetrics(int VertexCount, int EdgeCount, int FaceCount, int ComponentCount, int EulerCharacteristic, int Genus, double Volume, double DivergenceVolume);

/// <summary>
/// Инварианты CSG-результата (митигация R2): manifold_status, watertight, отсутствие
/// дефектов, V−E+F = 2−2·genus, согласованность объёма с формулой дивергенций.
/// </summary>
internal static class CsgAssert
{
    /// <summary>
    /// Проверяет полный набор инвариантов результата и возвращает метрики.
    /// <paramref name="result"/> — меш из булевой операции; метрики Manifold берутся
    /// повторным ingest того же меша (перекрёстная проверка моста).
    /// </summary>
    public static MeshMetrics AssertValidResult(HalfEdgeMesh result, string because)
    {
        Assert.True(result.Faces.Count > 0, $"{because}: результат не должен быть пустым.");
        MeshValidationResult validation = MeshValidator.Validate(result);
        Assert.True(validation.IsWatertight, $"{because}: результат должен быть замкнутым (watertight).");
        Assert.True(validation.IsEulerValid, $"{because}: V−E+F={validation.EulerCharacteristic} не согласована с числом компонент.");
        Assert.Empty(validation.Defects);
        Assert.True(validation.IsValid, $"{because}: меш должен быть валидным.");

        using ManifoldSolid solid = ManifoldBridge.ToSolid(result);
        Assert.Equal(ManifoldError.NoError, solid.Status);
        Assert.False(solid.IsEmpty, $"{because}: нативный результат не должен быть пустым.");

        double volume = solid.Volume;
        Assert.Equal(2 - 2 * solid.Genus, validation.EulerCharacteristic);
        Assert.Equal((long)validation.VertexCount, solid.VertexCount);
        Assert.Equal((long)validation.EdgeCount, solid.EdgeCount);
        Assert.Equal((long)validation.FaceCount, solid.TriangleCount);

        double divergenceVolume = VolumeByDivergence(result);
        AssertNear(divergenceVolume, volume, 1e-9, $"{because}: объём по дивергенциям vs Manifold");

        return new MeshMetrics(
            validation.VertexCount,
            validation.EdgeCount,
            validation.FaceCount,
            validation.ComponentCount,
            validation.EulerCharacteristic,
            solid.Genus,
            volume,
            divergenceVolume);
    }

    /// <summary>Объём по формуле дивергенций (сумма ориентированных тетраэдров) — независимая перекрёстная проверка.</summary>
    public static double VolumeByDivergence(HalfEdgeMesh mesh)
    {
        MeshData soup = mesh.ToTriangles();
        double volume = 0.0;
        for (int i = 0; i < soup.Indices.Count; i += 3)
        {
            Vector3d a = soup.Positions[soup.Indices[i]];
            Vector3d b = soup.Positions[soup.Indices[i + 1]];
            Vector3d c = soup.Positions[soup.Indices[i + 2]];
            volume += Vector3d.Dot(a, Vector3d.Cross(b, c)) / 6.0;
        }

        return volume;
    }

    public static void AssertNear(double actual, double expected, double relTol, string because)
    {
        double scale = Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(actual - expected) <= relTol * scale,
            $"{because}: ожидалось {expected:G17}, получено {actual:G17} (допуск {relTol:G3} отн.).");
    }

    /// <summary>Объём тела через Manifold (для проверочных соотношений).</summary>
    public static double VolumeOf(HalfEdgeMesh mesh)
    {
        using ManifoldSolid solid = ManifoldBridge.ToSolid(mesh);
        Assert.Equal(ManifoldError.NoError, solid.Status);
        return solid.Volume;
    }
}
