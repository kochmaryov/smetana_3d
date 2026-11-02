using SmetaNA.EditorUI.CommandPalette;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Палитра команд (спека ui-redesign T4): фильтр по имени, запуск выбранной,
/// пересборка имён и подписей при смене языка.
/// </summary>
public sealed class CommandPaletteViewModelTests
{
    private static CommandPaletteViewModel Create(
        out List<string> ran,
        LocalizationService? localization = null,
        ShortcutMap? shortcuts = null)
    {
        ran = [];
        return new CommandPaletteViewModel(
            localization ?? new LocalizationService(),
            shortcuts ?? new ShortcutMap(ShortcutCatalog.Defaults),
            ran.Add);
    }

    private static string NameOf(ILocalizationService localization, string commandId)
    {
        var map = new ShortcutMap(ShortcutCatalog.Defaults);
        string nameKey = map.GetNameKey(commandId)!;
        return localization.Get(nameKey);
    }

    [Fact]
    public void EmptyQuery_ShowsAllCatalogCommands()
    {
        CommandPaletteViewModel viewModel = Create(out _);
        var map = new ShortcutMap(ShortcutCatalog.Defaults);

        Assert.Equal(map.CommandIds.Count, viewModel.Results.Count);
        Assert.NotNull(viewModel.SelectedItem);
    }

    [Fact]
    public void Query_FiltersByName_IgnoringCase()
    {
        var localization = new LocalizationService();
        CommandPaletteViewModel viewModel = Create(out _, localization);
        string name = NameOf(localization, ShortcutCatalog.ToolRectangle);

        viewModel.Query = name[..2].ToUpperInvariant();

        Assert.All(
            viewModel.Results,
            item => Assert.Contains(name[..2], item.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewModel.Results, item => item.CommandId == ShortcutCatalog.ToolRectangle);
    }

    [Fact]
    public void Query_NoMatches_YieldsEmptyResults()
    {
        CommandPaletteViewModel viewModel = Create(out _);

        viewModel.Query = "нет-такой-команды";

        Assert.Empty(viewModel.Results);
        Assert.Null(viewModel.SelectedItem);
    }

    [Fact]
    public void Results_IncludeGestureForEveryCommand()
    {
        CommandPaletteViewModel viewModel = Create(out _);

        Assert.All(viewModel.Results, item => Assert.False(string.IsNullOrWhiteSpace(item.Gesture)));
        Assert.Equal(
            Shortcut.Parse("Space").ToString(),
            viewModel.Results.Single(item => item.CommandId == ShortcutCatalog.ToolSelect).Gesture);
    }

    [Fact]
    public void RunSelected_InvokesRunnerWithCommandId()
    {
        CommandPaletteViewModel viewModel = Create(out List<string> ran);
        viewModel.SelectedItem = viewModel.Results
            .Single(item => item.CommandId == ShortcutCatalog.ToolMove);

        bool result = viewModel.RunSelected();

        Assert.True(result);
        Assert.Equal([ShortcutCatalog.ToolMove], ran);
    }

    [Fact]
    public void RunSelected_NoSelection_ReturnsFalse()
    {
        CommandPaletteViewModel viewModel = Create(out List<string> ran);
        viewModel.SelectedItem = null;

        Assert.False(viewModel.RunSelected());
        Assert.Empty(ran);
    }

    [Fact]
    public void SelectedItem_DefaultsToFirstResult()
    {
        CommandPaletteViewModel viewModel = Create(out _);

        Assert.Same(viewModel.Results[0], viewModel.SelectedItem);
    }

    [Fact]
    public void CultureChange_RebuildsNamesAndTitles()
    {
        var localization = new LocalizationService();
        CommandPaletteViewModel viewModel = Create(out _, localization);
        string russianName = viewModel.Results
            .Single(item => item.CommandId == ShortcutCatalog.ToolSelect).Name;

        localization.SetCulture(EditorCulture.English);

        string englishName = viewModel.Results
            .Single(item => item.CommandId == ShortcutCatalog.ToolSelect).Name;
        Assert.NotEqual(russianName, englishName);
        Assert.Equal(NameOf(localization, ShortcutCatalog.ToolSelect), englishName);
        Assert.Equal(
            CommandPaletteStrings.Get(CommandPaletteStrings.Title, EditorCulture.English),
            viewModel.Title);
        Assert.Equal(
            CommandPaletteStrings.Get(CommandPaletteStrings.Placeholder, EditorCulture.English),
            viewModel.Placeholder);
    }

    [Fact]
    public void CultureChange_PreservesQueryFilter()
    {
        var localization = new LocalizationService();
        CommandPaletteViewModel viewModel = Create(out _, localization);
        string englishName = new LocalizationService().Get(
            new ShortcutMap(ShortcutCatalog.Defaults).GetNameKey(ShortcutCatalog.ToolEraser)!,
            EditorCulture.English);

        localization.SetCulture(EditorCulture.English);
        viewModel.Query = englishName[..3];

        int filtered = viewModel.Results.Count;
        Assert.InRange(filtered, 1, new ShortcutMap(ShortcutCatalog.Defaults).CommandIds.Count);
    }
}
