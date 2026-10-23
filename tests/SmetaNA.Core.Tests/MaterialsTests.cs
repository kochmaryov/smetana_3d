using SmetaNA.Core.Geometry;
using SmetaNA.Core.Materials;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Модель материалов (задача 5.4): цвет RGBA, простые свойства материала,
/// палитра по умолчанию, разрешение индексов граней/объектов.
/// </summary>
public class MaterialsTests
{
    [Fact]
    public void RgbaColor_FromHex_ParsesRgbAndRgba()
    {
        RgbaColor rgb = RgbaColor.FromHex("#C0392B");
        Assert.Equal(new RgbaColor(0xC0, 0x39, 0x2B), rgb);
        Assert.Equal(255, rgb.A);

        RgbaColor rgba = RgbaColor.FromHex("C0392B80");
        Assert.Equal(0x80, rgba.A);

        Assert.Equal("#C0392B", rgb.ToHex());
        Assert.Equal("#C0392B80", rgba.ToHex());
    }

    [Fact]
    public void RgbaColor_InvalidHex_Throws()
    {
        Assert.Throws<ArgumentException>(() => RgbaColor.FromHex("#12345"));
        Assert.Throws<ArgumentException>(() => RgbaColor.FromHex(""));
    }

    [Fact]
    public void Material_EffectiveColor_AppliesOpacityToAlpha()
    {
        var material = new Material("Glass", new RgbaColor(100, 150, 200, 255), opacity: 0.5);

        Assert.Equal(128, material.EffectiveColor.A);
        Assert.Equal(100, material.EffectiveColor.R);
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1.5, 0.5)]
    [InlineData(1.0, -0.1)]
    [InlineData(1.0, 1.1)]
    public void Material_InvalidProperties_Throw(double opacity, double shininess)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Material("Bad", RgbaColor.White, opacity, shininess));
    }

    [Fact]
    public void MaterialTable_IndexOfOrAdd_NoDuplicates()
    {
        var table = new MaterialTable();
        var red = new Material("Red", RgbaColor.FromHex("#FF0000"));

        int first = table.IndexOfOrAdd(red);
        int same = table.IndexOfOrAdd(new Material("Red", RgbaColor.FromHex("#FF0000")));

        Assert.Equal(first, same);
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void MaterialPalette_CreateDefault_HasStableIndices()
    {
        MaterialTable table = MaterialPalette.CreateDefault();

        Assert.Equal(MaterialPalette.Default, table[0]);
        Assert.Equal(MaterialPalette.Red, table[1]);
        Assert.Equal(MaterialPalette.CreateDefault().Count, table.Count);
        Assert.Equal(MaterialPalette.Standard.Count, table.Count);
    }

    [Fact]
    public void Mesh_GetsDefaultPalette()
    {
        var mesh = new HalfEdgeMesh();

        Assert.NotNull(mesh.Materials);
        Assert.Equal(MaterialPalette.Default, mesh.Materials[0]);
        Assert.Equal(-1, mesh.DefaultMaterialIndex);
    }

    [Fact]
    public void MaterialResolver_FaceIndex_OverridesMeshDefault()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace face = mesh.Faces[0];
        mesh.DefaultMaterialIndex = 1;
        face.MaterialIndex = 2;

        Assert.Equal(2, MaterialResolver.EffectiveIndex(mesh, face));
        Assert.Equal(MaterialPalette.Orange, MaterialResolver.Resolve(mesh, face));
    }

    [Fact]
    public void MaterialResolver_WithoutFaceIndex_InheritsMeshDefault()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace face = mesh.Faces[0];
        mesh.DefaultMaterialIndex = 3;

        Assert.Equal(3, MaterialResolver.EffectiveIndex(mesh, face));
        Assert.Equal(MaterialPalette.Yellow, MaterialResolver.Resolve(mesh, face));
    }

    [Fact]
    public void MaterialResolver_WithoutAnyAssignment_ReturnsDefaultMaterial()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);

        Assert.Equal(-1, MaterialResolver.EffectiveIndex(mesh, mesh.Faces[0]));
        Assert.Equal(MaterialPalette.Default, MaterialResolver.Resolve(mesh, mesh.Faces[0]));
        Assert.False(MaterialResolver.HasAnyAssignment(mesh));

        mesh.DefaultMaterialIndex = 1;
        Assert.True(MaterialResolver.HasAnyAssignment(mesh));
    }

    [Fact]
    public void MaterialResolver_CustomMaterial_ResolvedFromMeshPalette()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        var custom = new Material("Custom", RgbaColor.FromHex("#123456"));
        int index = mesh.Materials.IndexOfOrAdd(custom);
        mesh.Faces[0].MaterialIndex = index;

        Assert.Equal(custom, MaterialResolver.Resolve(mesh, mesh.Faces[0]));
    }
}
