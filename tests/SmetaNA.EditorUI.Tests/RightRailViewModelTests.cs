using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Правый док (спека ui-redesign T2): выбор секций, свёртка повторным кликом,
/// адаптив ≤ 960 px и показ ровно одной панели.
/// </summary>
public sealed class RightRailViewModelTests
{
    private static RightRailViewModel CreateViewModel() =>
        new(
        [
            new RightRailSectionViewModel("tutorial", UiIconId.Book, "Tutorial"),
            new RightRailSectionViewModel("properties", UiIconId.Info, "Properties"),
            new RightRailSectionViewModel("history", UiIconId.History, "History"),
            new RightRailSectionViewModel("shortcuts", UiIconId.Keyboard, "Shortcuts"),
            new RightRailSectionViewModel("printVolume", UiIconId.Printer, "Print volume"),
        ]);

    [Fact]
    public void ToggleSection_OpensDockWithSelectedSection()
    {
        RightRailViewModel viewModel = CreateViewModel();

        viewModel.ToggleSection(viewModel.Sections[1]);

        Assert.True(viewModel.IsDockOpen);
        Assert.Same(viewModel.Sections[1], viewModel.OpenSection);
        Assert.True(viewModel.Sections[1].IsSelected);
        Assert.False(viewModel.Sections[0].IsSelected);
        Assert.Equal(RightRailViewModel.DockWidth, viewModel.PanelWidth);
    }

    [Fact]
    public void ToggleSection_SameSectionAgain_CollapsesDock()
    {
        RightRailViewModel viewModel = CreateViewModel();
        viewModel.ToggleSection(viewModel.Sections[0]);

        viewModel.ToggleSection(viewModel.Sections[0]);

        Assert.False(viewModel.IsDockOpen);
        Assert.Equal(0, viewModel.PanelWidth);
    }

    [Fact]
    public void ToggleSection_AnotherSection_SwitchesWithoutClose()
    {
        RightRailViewModel viewModel = CreateViewModel();
        viewModel.ToggleSection(viewModel.Sections[0]);

        viewModel.ToggleSection(viewModel.Sections[2]);

        Assert.True(viewModel.IsDockOpen);
        Assert.Same(viewModel.Sections[2], viewModel.OpenSection);
        Assert.False(viewModel.Sections[0].IsSelected);
        Assert.True(viewModel.Sections[2].IsSelected);
    }

    [Fact]
    public void ShowFlags_ExposeExactlyOnePanel()
    {
        RightRailViewModel viewModel = CreateViewModel();
        viewModel.ToggleSection(viewModel.Sections[3]);

        Assert.False(viewModel.ShowTutorial);
        Assert.False(viewModel.ShowProperties);
        Assert.False(viewModel.ShowHistory);
        Assert.True(viewModel.ShowShortcuts);
        Assert.False(viewModel.ShowPrintVolume);
    }

    [Fact]
    public void ApplyWindowWidth_Narrow_CollapsesDock()
    {
        RightRailViewModel viewModel = CreateViewModel();
        viewModel.ToggleSection(viewModel.Sections[1]);

        viewModel.ApplyWindowWidth(RightRailViewModel.CompactMaxWindowWidth - 1);

        Assert.False(viewModel.IsDockOpen);
    }

    [Fact]
    public void ApplyWindowWidth_Wide_KeepsDockState()
    {
        RightRailViewModel viewModel = CreateViewModel();

        viewModel.ApplyWindowWidth(RightRailViewModel.CompactMaxWindowWidth + 100);
        viewModel.ToggleSection(viewModel.Sections[1]);

        Assert.True(viewModel.IsDockOpen);
    }

    [Fact]
    public void CloseAllFlyouts_CollapsesDock()
    {
        RightRailViewModel viewModel = CreateViewModel();
        viewModel.ToggleSection(viewModel.Sections[0]);

        viewModel.CloseAllFlyouts();

        Assert.False(viewModel.IsDockOpen);
    }
}
