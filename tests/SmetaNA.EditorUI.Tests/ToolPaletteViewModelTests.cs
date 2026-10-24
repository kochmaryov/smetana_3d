using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки палитры инструментов (трек B, T1/T2/T4/T7): состав из
/// <see cref="ToolGroupRegistry"/>, активный инструмент, вылеты «Дуга»/«Зеркало»,
/// тултипы «Имя · жест» и адаптив 2 колонки / 1 колонка / рейл (спека §4).
/// </summary>
public sealed class ToolPaletteViewModelTests
{
    private static ToolPaletteViewModel Create(
        List<ToolStubViewModel>? activated = null,
        Func<bool>? showHints = null,
        ILocalizationService? localization = null) =>
        new(
            localization ?? new LocalizationService(),
            new ShortcutMap(ShortcutCatalog.Defaults),
            tool => activated?.Add(tool),
            showHints);

    // --- B1: состав групп и кнопок -------------------------------------

    [Fact]
    public void Groups_MatchToolGroupRegistry()
    {
        ToolPaletteViewModel viewModel = Create();

        Assert.Equal(
            ToolGroupRegistry.DefaultGroups.Select(group => group.Id),
            viewModel.Groups.Select(group => group.Id));

        foreach ((PaletteGroup expected, ToolPaletteGroupViewModel actual) in
                 ToolGroupRegistry.DefaultGroups.Zip(viewModel.Groups))
        {
            Assert.Equal(expected.NameKey, actual.NameKey);
            Assert.Equal(
                expected.Buttons.Select(button => button.Definition.CommandId),
                actual.Buttons.Select(button => button.CommandId));
        }
    }

    [Fact]
    public void AllRegistryCommands_HaveButtons()
    {
        ToolPaletteViewModel viewModel = Create();

        Assert.Equal(24, viewModel.Groups.Sum(group => group.Buttons.Count));
        Assert.Equal(5, viewModel.Groups.Sum(group => group.Buttons.Sum(button => button.FlyoutItems.Count)));
        Assert.Equal(29, viewModel.AllButtons.Count);

        List<string> registryCommands = [.. ToolGroupRegistry.AllDefinitions().Select(definition => definition.CommandId)];
        Assert.Equal(27, registryCommands.Count);
        Assert.Equal(registryCommands.Count, viewModel.AllButtons.Select(button => button.CommandId).Distinct().Count());
        foreach (string commandId in registryCommands)
        {
            Assert.NotNull(viewModel.FindButton(commandId));
        }
    }

    // --- B1: активный инструмент ---------------------------------------

    [Fact]
    public void ActivatingButton_SetsActiveCommand_AndReportsToHost()
    {
        List<ToolStubViewModel> activated = [];
        ToolPaletteViewModel viewModel = Create(activated);
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;

        line.ActivateCommand.Execute(null);

        Assert.Equal(ShortcutCatalog.ToolLine, viewModel.ActiveCommandId);
        Assert.True(line.IsActive);
        Assert.Single(activated);
        Assert.Same(line.Tool, activated[0]);
    }

    [Fact]
    public void ActivatingDefaultVariant_SelectsParentCommand()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;

        arc.ActivateCommand.Execute(null);

        Assert.Equal(ShortcutCatalog.ToolArc, viewModel.ActiveCommandId);
        Assert.True(arc.IsActive);
        Assert.True(arc.FlyoutItems[0].IsActive);
        Assert.False(arc.FlyoutItems[1].IsActive);
    }

    [Fact]
    public void ActivatingFlyoutVariant_SelectsIt()
    {
        List<ToolStubViewModel> activated = [];
        ToolPaletteViewModel viewModel = Create(activated);
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;
        ToolPaletteButtonViewModel arc3Point = arc.FlyoutItems[1];

        arc3Point.ActivateCommand.Execute(null);

        Assert.Equal(ShortcutCatalog.ToolArc3Point, viewModel.ActiveCommandId);
        Assert.True(arc3Point.IsActive);
        Assert.False(arc.IsActive);
        Assert.Single(activated);
        Assert.Same(arc3Point.Tool, activated[0]);
    }

    [Fact]
    public void ServiceCommands_DoNotChangeActiveCommand()
    {
        List<ToolStubViewModel> activated = [];
        ToolPaletteViewModel viewModel = Create(activated);
        ToolPaletteButtonViewModel tutorial = viewModel.FindButton(ToolPaletteCommandIds.Tutorial)!;

        tutorial.ActivateCommand.Execute(null);

        Assert.Null(viewModel.ActiveCommandId);
        Assert.Single(activated);
        Assert.Same(tutorial.Tool, activated[0]);
    }

    [Fact]
    public void ActiveCommandId_FromHost_MarksMatchingButtons()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;

        viewModel.ActiveCommandId = ShortcutCatalog.ToolLine;

        Assert.True(line.IsActive);
        Assert.False(viewModel.FindButton(ShortcutCatalog.ToolSelect)!.IsActive);

        viewModel.ActiveCommandId = null;
        Assert.False(line.IsActive);
    }

    // --- B1: подсветка шага урока (T7) ---------------------------------

    [Fact]
    public void HighlightedCommand_MarksButton_FlyoutVariantAndParent()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;
        ToolPaletteButtonViewModel arc3Point = arc.FlyoutItems[1];

        viewModel.HighlightedCommandId = ShortcutCatalog.ToolArc3Point;

        Assert.True(arc3Point.IsHighlighted);
        Assert.True(arc.IsHighlighted);
        Assert.False(viewModel.FindButton(ShortcutCatalog.ToolLine)!.IsHighlighted);
    }

    [Fact]
    public void HighlightedCommand_Clearing_RemovesHighlight()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;

        viewModel.HighlightedCommandId = ShortcutCatalog.ToolLine;
        viewModel.HighlightedCommandId = null;

        Assert.False(line.IsHighlighted);
        Assert.All(viewModel.AllButtons, button => Assert.False(button.IsHighlighted));
    }

    // --- B2: вылеты «Дуга» и «Зеркало» ---------------------------------

    [Fact]
    public void ArcButton_HasTwoFlyoutVariants_WithNamesAndGestures()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;

        Assert.True(arc.HasFlyout);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolArc, ShortcutCatalog.ToolArc3Point },
            arc.FlyoutItems.Select(item => item.CommandId));
        foreach (ToolPaletteButtonViewModel item in arc.FlyoutItems)
        {
            Assert.NotEmpty(item.Name);
            Assert.NotEmpty(item.Gesture);
            Assert.Contains("·", item.Label);
        }
    }

    [Fact]
    public void MirrorButton_HasThreeFlyoutVariants_WithNamesAndGestures()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel mirror = viewModel.FindButton(ShortcutCatalog.ToolMirrorAxis)!;

        Assert.True(mirror.HasFlyout);
        Assert.Equal(
            new[]
            {
                ShortcutCatalog.ToolMirrorAxis,
                ShortcutCatalog.ToolMirrorPoints,
                ShortcutCatalog.ToolMirrorFace,
            },
            mirror.FlyoutItems.Select(item => item.CommandId));
        foreach (ToolPaletteButtonViewModel item in mirror.FlyoutItems)
        {
            Assert.NotEmpty(item.Name);
            Assert.NotEmpty(item.Gesture);
            Assert.Contains("·", item.Label);
        }
    }

    [Fact]
    public void RailItems_ExpandFlyoutVariants_Flat()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteGroupViewModel drawing = viewModel.Groups[0];

        // 7 кнопок группы, «Дуга» разворачивается в 2 варианта
        Assert.Equal(8, drawing.RailItems.Count);
        Assert.Single(drawing.RailItems, item => item.CommandId == ShortcutCatalog.ToolArc);
        Assert.Contains(drawing.RailItems, item => item.CommandId == ShortcutCatalog.ToolArc3Point);
    }

    [Fact]
    public void Activation_ClosesOpenFlyouts()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;
        arc.IsFlyoutOpen = true;
        viewModel.Groups[0].IsFlyoutOpen = true;

        line.ActivateCommand.Execute(null);

        Assert.False(arc.IsFlyoutOpen);
        Assert.False(viewModel.Groups[0].IsFlyoutOpen);
    }

    [Fact]
    public void OpenFlyoutCommand_OpensFlyout()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel arc = viewModel.FindButton(ShortcutCatalog.ToolArc)!;

        arc.OpenFlyoutCommand.Execute(null);

        Assert.True(arc.IsFlyoutOpen);
    }

    // --- B3: тултипы «Имя · жест» + «Новичок» (T7) ----------------------

    [Fact]
    public void ToolTip_Expert_ShowsNameAndGesture()
    {
        ToolPaletteViewModel viewModel = Create(showHints: () => false);
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;

        Assert.NotEmpty(line.Gesture);
        Assert.Equal($"{line.Name} · {line.Gesture}", line.ToolTip);
    }

    [Fact]
    public void ToolTip_Novice_AddsHintKeyExplanation()
    {
        ToolPaletteViewModel viewModel = Create(showHints: () => true);
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;

        Assert.NotEmpty(line.Hint);
        Assert.Equal($"{line.Name} · {line.Gesture}{Environment.NewLine}{line.Hint}", line.ToolTip);
    }

    [Fact]
    public void ToolTip_WithoutGesture_ShowsNameOnly()
    {
        ToolPaletteViewModel viewModel = Create(showHints: () => true);
        ToolPaletteButtonViewModel tutorial = viewModel.FindButton(ToolPaletteCommandIds.Tutorial)!;

        Assert.Empty(tutorial.Gesture);
        Assert.Equal(tutorial.Name, tutorial.ToolTip);
    }

    [Fact]
    public void ToolTip_ReflectsCultureChange()
    {
        ILocalizationService localization = new LocalizationService();
        ToolPaletteViewModel viewModel = Create(localization: localization);
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;
        string russianName = line.Name;

        localization.SetCulture(EditorCulture.English);

        Assert.NotEqual(russianName, line.Name);
        Assert.Equal($"{line.Name} · {line.Gesture}", line.ToolTip);
    }

    [Fact]
    public void RefreshToolTips_RaisesToolTipPropertyChanged()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteButtonViewModel line = viewModel.FindButton(ShortcutCatalog.ToolLine)!;
        List<string> changed = [];
        line.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        viewModel.RefreshToolTips();

        Assert.Contains(nameof(ToolPaletteButtonViewModel.ToolTip), changed);
    }

    // --- B4: адаптив 2 колонки / 1 колонка / рейл (§4) -----------------

    [Theory]
    [InlineData(1920, PaletteLayoutMode.TwoColumns)]
    [InlineData(1440, PaletteLayoutMode.TwoColumns)]
    [InlineData(1280, PaletteLayoutMode.TwoColumns)]
    [InlineData(961, PaletteLayoutMode.TwoColumns)]
    [InlineData(960, PaletteLayoutMode.SingleColumn)]
    [InlineData(769, PaletteLayoutMode.SingleColumn)]
    [InlineData(768, PaletteLayoutMode.Rail)]
    [InlineData(600, PaletteLayoutMode.Rail)]
    public void ResolveMode_UsesSpecBreakpoints(double windowWidth, PaletteLayoutMode expected)
    {
        Assert.Equal(expected, ToolPaletteViewModel.ResolveMode(windowWidth));
    }

    [Fact]
    public void ApplyWindowWidth_UpdatesModeAndWidths()
    {
        ToolPaletteViewModel viewModel = Create();

        viewModel.ApplyWindowWidth(1280);
        Assert.True(viewModel.IsTwoColumns);
        Assert.Equal(ToolPaletteViewModel.WidePaletteWidth, viewModel.PaletteWidth);
        Assert.Equal(ToolPaletteViewModel.WideButtonsPanelWidth, viewModel.ButtonsPanelWidth);

        viewModel.ApplyWindowWidth(960);
        Assert.True(viewModel.IsSingleColumn);
        Assert.Equal(ToolPaletteViewModel.CompactPaletteWidth, viewModel.PaletteWidth);
        Assert.Equal(ToolPaletteViewModel.CompactButtonsPanelWidth, viewModel.ButtonsPanelWidth);

        viewModel.ApplyWindowWidth(768);
        Assert.True(viewModel.IsRailMode);
        Assert.Equal(ToolPaletteViewModel.RailPaletteWidth, viewModel.PaletteWidth);
    }

    // --- B4: свёртка групп при переполнении (риск R2) ------------------

    [Fact]
    public void ApplyViewportHeight_CollapsesTailGroups_KeepsBaseGroups()
    {
        ToolPaletteViewModel viewModel = Create();
        viewModel.ApplyWindowWidth(1280);
        viewModel.ApplyViewportHeight(1000);
        Assert.All(viewModel.Groups, group => Assert.True(group.IsExpanded));

        viewModel.ApplyViewportHeight(600);

        Assert.True(viewModel.Groups[0].IsExpanded); // Рисование — базовые инструменты
        Assert.True(viewModel.Groups[1].IsExpanded); // Правка — базовые инструменты
        Assert.True(viewModel.Groups[2].IsExpanded);
        Assert.False(viewModel.Groups[3].IsExpanded);
        Assert.False(viewModel.Groups[4].IsExpanded);
    }

    [Fact]
    public void ApplyViewportHeight_DoesNotCollapseProtectedGroups()
    {
        ToolPaletteViewModel viewModel = Create();
        viewModel.ApplyWindowWidth(1280);

        viewModel.ApplyViewportHeight(100);

        Assert.True(viewModel.Groups[0].IsExpanded);
        Assert.True(viewModel.Groups[1].IsExpanded);
        Assert.All(viewModel.Groups.Skip(2), group => Assert.False(group.IsExpanded));
    }

    [Fact]
    public void ApplyViewportHeight_SameHeight_DoesNotUndoManualExpansion()
    {
        ToolPaletteViewModel viewModel = Create();
        viewModel.ApplyWindowWidth(1280);
        viewModel.ApplyViewportHeight(600);
        viewModel.Groups[4].IsExpanded = true;

        viewModel.ApplyViewportHeight(600);

        Assert.True(viewModel.Groups[4].IsExpanded);
    }

    [Fact]
    public void ApplyViewportHeight_KeepsGroupsExpandedInRailMode()
    {
        ToolPaletteViewModel viewModel = Create();
        viewModel.ApplyWindowWidth(768);

        viewModel.ApplyViewportHeight(100);

        Assert.All(viewModel.Groups, group => Assert.True(group.IsExpanded));
    }

    [Fact]
    public void ToggleExpandedCommand_TogglesGroup()
    {
        ToolPaletteViewModel viewModel = Create();
        ToolPaletteGroupViewModel group = viewModel.Groups[0];

        group.ToggleExpandedCommand.Execute(null);
        Assert.False(group.IsExpanded);

        group.ToggleExpandedCommand.Execute(null);
        Assert.True(group.IsExpanded);
    }
}
