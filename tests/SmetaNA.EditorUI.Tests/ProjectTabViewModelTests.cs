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
    public void Strings_ExposeViewportHudKeys()
    {
        ProjectTabViewModel viewModel = CreateViewModel(new Scene());

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudStats));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudUnits));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Strings.ViewportHudSelectionNone));
    }
}
