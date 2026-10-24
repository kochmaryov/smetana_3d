using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.ToolLayout;
using SmetaNA.EditorUI.ToolPalette;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки модели раскладки палитры (волна 0 контрактов tool-palette):
/// дефолт из реестра, версия схемы, иммутабельные копии для миграции.
/// </summary>
public sealed class ToolLayoutModelTests
{
    [Fact]
    public void CreateDefault_MatchesRegistryGroups()
    {
        ToolLayoutModel layout = ToolLayoutModel.CreateDefault();

        Assert.Equal(ToolLayoutModel.CurrentVersion, layout.Version);
        Assert.Equal(ToolGroupRegistry.DefaultGroups.Count, layout.Groups.Count);

        for (int groupIndex = 0; groupIndex < layout.Groups.Count; groupIndex++)
        {
            ToolLayoutGroup group = layout.Groups[groupIndex];
            PaletteGroup source = ToolGroupRegistry.DefaultGroups[groupIndex];
            Assert.Equal(source.Id, group.Id);
            Assert.Null(group.CustomName);
            Assert.True(group.Visible);
            Assert.Equal(source.Buttons.Count, group.Items.Count);

            for (int itemIndex = 0; itemIndex < group.Items.Count; itemIndex++)
            {
                ToolLayoutItem item = group.Items[itemIndex];
                Assert.Equal(source.Buttons[itemIndex].Definition.CommandId, item.CommandId);
                Assert.True(item.Visible);
                Assert.Equal(itemIndex, item.Order);
            }
        }
    }

    [Fact]
    public void CreateDefault_SupportsEmptyGroups()
    {
        ToolLayoutModel layout = ToolLayoutModel.CreateDefault([]);
        Assert.Empty(layout.Groups);
    }

    [Fact]
    public void Model_IsImmutableRecord_MigrationCopiesKeepSource()
    {
        ToolLayoutModel layout = ToolLayoutModel.CreateDefault();
        ToolLayoutModel migrated = layout with { Version = ToolLayoutModel.CurrentVersion + 1 };

        Assert.Equal(ToolLayoutModel.CurrentVersion, layout.Version);
        Assert.Equal(ToolLayoutModel.CurrentVersion + 1, migrated.Version);
        Assert.Equal(layout.Groups, migrated.Groups);
    }

    [Fact]
    public void CustomGroup_CarriesUserVisibleName()
    {
        var custom = new ToolLayoutGroup("custom.1", "Мои инструменты", true,
            [new ToolLayoutItem(ShortcutCatalog.ToolLine, true, 0)]);
        Assert.Equal("Мои инструменты", custom.CustomName);
    }
}
