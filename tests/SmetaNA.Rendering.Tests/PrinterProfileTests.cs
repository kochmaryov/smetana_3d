using SmetaNA.Core.Geometry;
using SmetaNA.Rendering.PrintVolume;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Профили принтеров (задача 4.5): каталог из 3–5 моделей, габариты зоны
/// печати, сопло и диапазон высот слоя, валидация входных данных.
/// </summary>
public sealed class PrinterProfileTests
{
    [Fact]
    public void Catalog_ContainsReferencePrinters()
    {
        IReadOnlyList<PrinterProfile> all = PrinterProfileCatalog.All;

        Assert.True(all.Count >= 3, "В каталоге должно быть минимум 3 профиля принтеров.");
        Assert.NotNull(PrinterProfileCatalog.Find(PrinterProfileCatalog.PrusaI3Mk3sId));
        Assert.NotNull(PrinterProfileCatalog.Find(PrinterProfileCatalog.CrealityEnder3Id));
        Assert.NotNull(PrinterProfileCatalog.Find(PrinterProfileCatalog.BambuX1cId));
        Assert.Same(all[0], PrinterProfileCatalog.Default);
    }

    [Fact]
    public void Catalog_ProfilesHaveSaneDimensions()
    {
        foreach (PrinterProfile profile in PrinterProfileCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.Id));
            Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
            Assert.True(profile.BuildVolumeMm.X is >= 100 and <= 1000, profile.Id);
            Assert.True(profile.BuildVolumeMm.Y is >= 100 and <= 1000, profile.Id);
            Assert.True(profile.BuildVolumeMm.Z is >= 50 and <= 1000, profile.Id);
            Assert.True(profile.NozzleDiameterMm is >= 0.1 and <= 2, profile.Id);
            Assert.True(profile.LayerHeightMinMm > 0 && profile.LayerHeightMinMm <= profile.LayerHeightMaxMm, profile.Id);
            Assert.True(profile.LayerHeightMaxMm <= 1.0, profile.Id);
            Assert.False(string.IsNullOrWhiteSpace(profile.Notes), profile.Id);
        }
    }

    [Fact]
    public void Catalog_IdsAreUnique()
    {
        string[] ids = PrinterProfileCatalog.All.Select(profile => profile.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void BuildVolumeBounds_IsCenteredOnOriginXY_AndSitsOnFloor()
    {
        PrinterProfile profile = PrinterProfileCatalog.Find(PrinterProfileCatalog.PrusaI3Mk3sId)!;

        BoundingBox3d bounds = profile.BuildVolumeBounds;

        Assert.Equal(profile.BuildVolumeMm, bounds.Size);
        Assert.Equal(0, bounds.Center.X, 9);
        Assert.Equal(0, bounds.Center.Y, 9);
        Assert.Equal(0, bounds.Min.Z, 9);
        Assert.Equal(profile.BuildVolumeMm.Z, bounds.Max.Z, 9);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownId()
    {
        Assert.Null(PrinterProfileCatalog.Find("нет-такого-принтера"));
    }

    [Fact]
    public void Constructor_RejectsInvalidProfileData()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PrinterProfile(
            "", "Vendor", "Model", new Vector3d(100, 100, 100), 0.4, 0.1, 0.3));
        Assert.ThrowsAny<ArgumentException>(() => new PrinterProfile(
            "id", "Vendor", "Model", new Vector3d(0, 100, 100), 0.4, 0.1, 0.3));
        Assert.ThrowsAny<ArgumentException>(() => new PrinterProfile(
            "id", "Vendor", "Model", new Vector3d(100, 100, 100), 0, 0.1, 0.3));
        Assert.ThrowsAny<ArgumentException>(() => new PrinterProfile(
            "id", "Vendor", "Model", new Vector3d(100, 100, 100), 0.4, 0.3, 0.1));
    }
}
