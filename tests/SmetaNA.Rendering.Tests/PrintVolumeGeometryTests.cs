using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering.PrintVolume;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Геометрия зоны печати (задача 4.5): каркас зоны, сетка пола, подсветка
/// «за пределами зоны» и попадание модели в зону.
/// </summary>
public sealed class PrintVolumeGeometryTests
{
    private static readonly BoundingBox3d Volume = new(
        new Vector3d(-100, -100, 0),
        new Vector3d(100, 100, 200));

    private static RenderObject Triangle(Vector3d a, Vector3d b, Vector3d c)
    {
        var node = new MeshNode { Mesh = TestMeshes.Box(10), Name = "triangle" };
        var triangles = new MeshData([a, b, c], [0, 1, 2]);
        return new RenderObject(
            node,
            node,
            triangles,
            [],
            BoundingBox3d.FromMeshData(triangles));
    }

    [Fact]
    public void CreateFrameLines_OutputsTwelveBoxEdges()
    {
        float[] lines = PrintVolumeGeometry.CreateFrameLines(Volume);

        Assert.Equal(12 * 2 * 3, lines.Length);
        for (int i = 0; i < lines.Length; i += 3)
        {
            Assert.InRange(lines[i], -100, 100);
            Assert.InRange(lines[i + 1], -100, 100);
            Assert.InRange(lines[i + 2], 0, 200);
        }
    }

    [Fact]
    public void CreateFrameLines_RejectsEmptyVolume()
    {
        Assert.ThrowsAny<ArgumentException>(() => PrintVolumeGeometry.CreateFrameLines(BoundingBox3d.Empty));
    }

    [Fact]
    public void NiceStep_UsesOneTwoFiveSequence()
    {
        Assert.Equal(20, PrintVolumeGeometry.NiceStep(Volume), 9);
        Assert.Equal(2, PrintVolumeGeometry.NiceStep(new BoundingBox3d(
            new Vector3d(0, 0, 0), new Vector3d(20, 20, 10))), 9);
    }

    [Fact]
    public void CreateFloorGridLines_StaysInsideFootprintAtFloorLevel()
    {
        float[] lines = PrintVolumeGeometry.CreateFloorGridLines(Volume, out double step);

        Assert.Equal(20, step, 9);
        Assert.NotEmpty(lines);
        Assert.Equal(0, lines.Length % 6);
        for (int i = 0; i < lines.Length; i += 3)
        {
            Assert.InRange(lines[i], -100 - step, 100 + step);
            Assert.InRange(lines[i + 1], -100 - step, 100 + step);
            Assert.Equal(0, lines[i + 2], 9);
        }
    }

    [Fact]
    public void CreateOutOfVolumeTriangles_KeepsOnlyTrianglesOutside()
    {
        RenderObject inside = Triangle(
            new Vector3d(0, 0, 10), new Vector3d(10, 0, 10), new Vector3d(0, 10, 10));
        RenderObject outside = Triangle(
            new Vector3d(0, 0, 300), new Vector3d(10, 0, 300), new Vector3d(0, 10, 300));
        RenderObject straddling = Triangle(
            new Vector3d(0, 0, 10), new Vector3d(10, 0, 10), new Vector3d(0, 10, 500));

        float[] onlyInside = PrintVolumeGeometry.CreateOutOfVolumeTriangles([inside], Volume);
        Assert.Empty(onlyInside);

        float[] onlyOutside = PrintVolumeGeometry.CreateOutOfVolumeTriangles([outside], Volume);
        Assert.Equal(9, onlyOutside.Length);
        Assert.Equal(300, onlyOutside[2], 9);

        float[] mixed = PrintVolumeGeometry.CreateOutOfVolumeTriangles([inside, outside, straddling], Volume);
        Assert.Equal(18, mixed.Length);
    }

    [Fact]
    public void AnalyzeFit_DetectsOverflowPerAxis()
    {
        RenderObject inside = Triangle(
            new Vector3d(0, 0, 10), new Vector3d(10, 0, 10), new Vector3d(0, 10, 10));
        PrintVolumeFit fits = PrintVolumeGeometry.AnalyzeFit([inside], Volume);

        Assert.True(fits.Fits);
        Assert.Equal(0, fits.OutOfVolumeTriangleCount);

        RenderObject tooTall = Triangle(
            new Vector3d(0, 0, 10), new Vector3d(10, 0, 10), new Vector3d(0, 10, 250));
        PrintVolumeFit tall = PrintVolumeGeometry.AnalyzeFit([tooTall], Volume);

        Assert.False(tall.Fits);
        Assert.Equal(50, tall.OverflowZMm, 9);
        Assert.Equal(1, tall.OutOfVolumeTriangleCount);

        RenderObject tooWide = Triangle(
            new Vector3d(-150, 0, 10), new Vector3d(10, 0, 10), new Vector3d(0, 10, 10));
        PrintVolumeFit wide = PrintVolumeGeometry.AnalyzeFit([tooWide], Volume);

        Assert.False(wide.Fits);
        Assert.Equal(50, wide.OverflowXMm, 9);
    }

    [Fact]
    public void AnalyzeFit_EmptyModel_Fits()
    {
        PrintVolumeFit fit = PrintVolumeGeometry.AnalyzeFit([], Volume);

        Assert.True(fit.Fits);
        Assert.Equal(0, fit.OutOfVolumeTriangleCount);
    }

    [Fact]
    public void OverlayBuild_WithoutModel_ContainsFrameAndGridOnly()
    {
        PrinterProfile profile = PrinterProfileCatalog.Find(PrinterProfileCatalog.PrusaI3Mk3sId)!;

        PrintVolumeOverlay overlay = PrintVolumeOverlay.Build(profile, null);

        Assert.Same(profile, overlay.Profile);
        Assert.NotEmpty(overlay.FrameLines);
        Assert.NotEmpty(overlay.FloorGridLines);
        Assert.False(overlay.HasOutOfVolume);
        Assert.True(overlay.Fit.Fits);
    }

    [Fact]
    public void OverlayBuild_MarksGeometryOutsideVolume()
    {
        PrinterProfile profile = PrinterProfileCatalog.Find(PrinterProfileCatalog.BambuX1cId)!;
        RenderObject outside = Triangle(
            new Vector3d(0, 0, 500), new Vector3d(10, 0, 500), new Vector3d(0, 10, 500));
        SceneRenderModel model = SceneRenderModel.FromObjects([outside]);

        PrintVolumeOverlay overlay = PrintVolumeOverlay.Build(profile, model);

        Assert.True(overlay.HasOutOfVolume);
        Assert.False(overlay.Fit.Fits);
        Assert.True(overlay.Fit.OverflowZMm > 0);
    }
}
