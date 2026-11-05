using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>Вкладка проекта: единицы сцены и режимы отображения для HUD вьюпорта.</summary>
public sealed class ProjectTabViewModelTests
{
    private static ProjectTabViewModel CreateViewModel(Scene scene) =>
        new(new LocalizationService(), scene, 1);

    [Fact]
    public void SelectedUnit_WritesSceneUnit()
    {
        var scene = new Scene { Unit = LengthUnit.Millimeter };
        ProjectTabViewModel viewModel = CreateViewModel(scene);

        viewModel.SelectedUnit = viewModel.Units.First(option => option.Unit == LengthUnit.Inch);

        Assert.Equal(LengthUnit.Inch, scene.Unit);
        Assert.Equal(LengthUnit.Inch, viewModel.SelectedUnit.Unit);
    }

    [Fact]
    public void SetCursorPoint_FormatsSceneUnits_AndClearsOnNull()
    {
        var scene = new Scene { Unit = LengthUnit.Centimeter };
        ProjectTabViewModel viewModel = CreateViewModel(scene);

        viewModel.SetCursorPoint(new Vector3d(20, 30, 5));

        Assert.True(viewModel.HasCursorCoordinates);
        Assert.Contains("2", viewModel.CursorCoordinates);
        Assert.Contains("3", viewModel.CursorCoordinates);
        Assert.Contains(0.5.ToString("0.##", CultureInfo.CurrentCulture), viewModel.CursorCoordinates);

        viewModel.SetCursorPoint(null);

        Assert.False(viewModel.HasCursorCoordinates);
        Assert.Equal(string.Empty, viewModel.CursorCoordinates);
    }

    [Fact]
    public void Units_CoverEveryLengthUnit_WithLocalizedName()
    {
        ProjectTabViewModel viewModel = CreateViewModel(new Scene());

        foreach (LengthUnit unit in Enum.GetValues<LengthUnit>())
        {
            UnitOption option = Assert.Single(viewModel.Units, candidate => candidate.Unit == unit);
            Assert.False(string.IsNullOrWhiteSpace(option.Name));
        }
    }

    [Fact]
    public void RenderModes_CoverEveryMode_DefaultsToShaded()
    {
        ProjectTabViewModel viewModel = CreateViewModel(new Scene());

        foreach (RenderMode mode in Enum.GetValues<RenderMode>())
        {
            RenderModeOption option = Assert.Single(viewModel.RenderModes, candidate => candidate.Mode == mode);
            Assert.False(string.IsNullOrWhiteSpace(option.Name));
        }

        Assert.Equal(RenderMode.Shaded, viewModel.SelectedRenderMode.Mode);
    }

    [Fact]
    public void SelectedUnit_NullFromBinding_IsIgnored()
    {
        var scene = new Scene { Unit = LengthUnit.Millimeter };
        ProjectTabViewModel viewModel = CreateViewModel(scene);
        UnitOption millimeter = viewModel.SelectedUnit;

        // ComboBox при пересоздании контента вкладки пишет null в SelectedItem
        viewModel.SelectedUnit = null!;

        Assert.Same(millimeter, viewModel.SelectedUnit);
        Assert.Equal(LengthUnit.Millimeter, scene.Unit);
    }

    [Fact]
    public void SelectedRenderMode_NullFromBinding_IsIgnored()
    {
        ProjectTabViewModel viewModel = CreateViewModel(new Scene());
        RenderModeOption shaded = viewModel.SelectedRenderMode;

        viewModel.SelectedRenderMode = null!;

        Assert.Same(shaded, viewModel.SelectedRenderMode);
    }

    [Fact]
    public void Strings_ExposeViewportHudKeys()
    {
        ProjectTabViewModel viewModel = CreateViewModel(new Scene());

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudStats));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudUnits));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudSelectionNone));
    }
}
