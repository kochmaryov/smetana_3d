using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки реестра групп палитры (волна 0 контрактов tool-palette): состав по
/// макету variant_a, полнота относительно ленты, имена/подсказки в RU/EN.
/// </summary>
public sealed class ToolGroupRegistryTests
{
    private static IEnumerable<ToolDefinition> RibbonDefinitions() =>
        MainWindowViewModel.PrimaryRibbonDefinitions.Concat(MainWindowViewModel.AdvancedRibbonDefinitions);

    [Fact]
    public void DefaultGroups_HaveExpectedShape()
    {
        Assert.Equal(
            new[]
            {
                ToolGroupRegistry.DrawingGroupId,
                ToolGroupRegistry.EditingGroupId,
                ToolGroupRegistry.OperationsGroupId,
                ToolGroupRegistry.DimensionsGroupId,
                ToolGroupRegistry.ViewGroupId,
            },
            ToolGroupRegistry.DefaultGroups.Select(group => group.Id));

        Assert.Equal(7, ToolGroupRegistry.DefaultGroups[0].Buttons.Count); // Рисование (Дуга — 1 кнопка с вылетом)
        Assert.Equal(8, ToolGroupRegistry.DefaultGroups[1].Buttons.Count); // Правка
        Assert.Equal(5, ToolGroupRegistry.DefaultGroups[2].Buttons.Count); // Операции (Зеркало — 1 кнопка с вылетом)
        Assert.Equal(2, ToolGroupRegistry.DefaultGroups[3].Buttons.Count); // Размеры
        Assert.Equal(2, ToolGroupRegistry.DefaultGroups[4].Buttons.Count); // Вид (служебные)
    }

    [Fact]
    public void AllDefinitions_AreUnique_AndCoverAllTools()
    {
        List<ToolDefinition> all = [.. ToolGroupRegistry.AllDefinitions()];
        Assert.Equal(27, all.Count); // 24 инструмента + Выбор + 2 служебные команды
        Assert.Equal(all.Count, all.Select(definition => definition.CommandId).Distinct().Count());

        string[] expectedToolIds =
        [
            ShortcutCatalog.ToolSelect,
            ShortcutCatalog.ToolLine,
            ShortcutCatalog.ToolRectangle,
            ShortcutCatalog.ToolCircle,
            ShortcutCatalog.ToolPolygon,
            ShortcutCatalog.ToolArc,
            ShortcutCatalog.ToolArc3Point,
            ShortcutCatalog.ToolFreehand,
            ShortcutCatalog.ToolPushPull,
            ShortcutCatalog.ToolOffset,
            ShortcutCatalog.ToolMove,
            ShortcutCatalog.ToolRotate,
            ShortcutCatalog.ToolScale,
            ShortcutCatalog.ToolEraser,
            ShortcutCatalog.ToolSoften,
            ShortcutCatalog.ToolPaintBucket,
            ShortcutCatalog.ToolArrayLinear,
            ShortcutCatalog.ToolArrayPolar,
            ShortcutCatalog.ToolMirrorAxis,
            ShortcutCatalog.ToolMirrorPoints,
            ShortcutCatalog.ToolMirrorFace,
            ShortcutCatalog.ToolFollowMe,
            ShortcutCatalog.ToolRevolve,
            ShortcutCatalog.ToolDimension,
            ShortcutCatalog.ToolScaleToDimension,
        ];
        foreach (string commandId in expectedToolIds)
        {
            Assert.Contains(all, definition => definition.CommandId == commandId);
        }

        Assert.Contains(all, definition => definition.CommandId == ToolPaletteCommandIds.PrintVolume);
        Assert.Contains(all, definition => definition.CommandId == ToolPaletteCommandIds.Tutorial);
    }

    [Fact]
    public void Flyouts_HoldRelatedTools_ArcAndMirror()
    {
        PaletteButton arc = ToolGroupRegistry.FindButton(ShortcutCatalog.ToolArc)!;
        Assert.NotNull(arc);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolArc, ShortcutCatalog.ToolArc3Point },
            arc.FlyoutItems.Select(item => item.Definition.CommandId));

        PaletteButton mirror = ToolGroupRegistry.FindButton(ShortcutCatalog.ToolMirrorAxis)!;
        Assert.NotNull(mirror);
        Assert.Equal(
            new[]
            {
                ShortcutCatalog.ToolMirrorAxis,
                ShortcutCatalog.ToolMirrorPoints,
                ShortcutCatalog.ToolMirrorFace,
            },
            mirror.FlyoutItems.Select(item => item.Definition.CommandId));

        Assert.Null(ToolGroupRegistry.FindButton(ShortcutCatalog.ToolMirrorPoints)!.FlyoutItems
            .FirstOrDefault());
    }

    [Fact]
    public void EveryCommand_HasIcon()
    {
        foreach (ToolDefinition definition in ToolGroupRegistry.AllDefinitions())
        {
            Assert.NotEqual(ToolIconId.None, ToolIconMap.ForCommand(definition.CommandId));
        }
    }

    [Fact]
    public void Registry_MatchesRibbon_CommandsAndVcbKinds()
    {
        Dictionary<string, ToolDefinition> registry = ToolGroupRegistry.AllDefinitions()
            .ToDictionary(definition => definition.CommandId);
        foreach (ToolDefinition ribbon in RibbonDefinitions())
        {
            Assert.True(registry.ContainsKey(ribbon.CommandId), $"нет в реестре: {ribbon.CommandId}");
            Assert.Equal(ribbon.VcbKind, registry[ribbon.CommandId].VcbKind);
        }

        foreach (ToolDefinition definition in registry.Values.Where(definition =>
                     !ToolGroupRegistry.IsServiceCommand(definition.CommandId)))
        {
            Assert.Contains(RibbonDefinitions(), ribbon => ribbon.CommandId == definition.CommandId);
        }
    }

    [Fact]
    public void EveryNameAndHint_ResolvesInBothCultures()
    {
        var service = new LocalizationService();
        foreach (ToolDefinition definition in ToolGroupRegistry.AllDefinitions())
        {
            foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
            {
                string name = service.Get(definition.NameKey, culture);
                Assert.False(string.IsNullOrWhiteSpace(name), $"{definition.CommandId}: имя пусто для {culture}");
                Assert.NotEqual(definition.NameKey, name);
            }
        }

        foreach (PaletteGroup group in ToolGroupRegistry.DefaultGroups)
        {
            foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
            {
                Assert.NotEqual(group.NameKey, service.Get(group.NameKey, culture));
            }
        }
    }
}
