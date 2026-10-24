using System.Text.Json;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.ToolLayout;
using SmetaNA.EditorUI.ToolPalette;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки хранилища раскладки палитры (трек C, T8): JSON в каталоге настроек
/// приложения (не в bin), round-trip своих групп и порядка, версия схемы,
/// миграция (риск R4) и откат к дефолту на повреждённом/чужом файле.
/// </summary>
public sealed class ToolLayoutStoreTests : IDisposable
{
    private readonly string _root;
    private readonly JsonToolLayoutStore _store;

    public ToolLayoutStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "smetana-toollayout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _store = new JsonToolLayoutStore(Path.Combine(_root, JsonToolLayoutStore.FileName));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // временный каталог подчищается при следующем запуске системы
        }
    }

    [Fact]
    public void DefaultFilePath_IsInAppSettings_NotInBin()
    {
        string path = JsonToolLayoutStore.DefaultFilePath;

        Assert.Equal(JsonToolLayoutStore.FileName, Path.GetFileName(path));
        Assert.Contains(Path.Combine("SmetaNA", "settings"), path);
        Assert.False(
            path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase),
            "Раскладка должна храниться в настройках пользователя, а не рядом с бинарниками.");
    }

    [Fact]
    public void Load_WithoutFile_ReturnsDefaultLayout()
    {
        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), _store.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_KeepsCustomGroupsOrderAndVisibility()
    {
        ToolLayoutModel custom = BuildCustomizedLayout();

        _store.Save(custom);
        JsonToolLayoutStore reopened = new(_store.FilePath);
        ToolLayoutModel loaded = reopened.Load();

        AssertLayoutsEqual(custom, loaded);
    }

    [Fact]
    public void RoundTrip_CustomGroup_SurvivesRestart()
    {
        ToolLayoutModel custom = BuildCustomizedLayout();
        _store.Save(custom);

        // «Перезапуск»: новое хранилище над тем же файлом.
        JsonToolLayoutStore restarted = new(_store.FilePath);
        ToolLayoutModel loaded = restarted.Load();

        ToolLayoutGroup myGroup = Assert.Single(loaded.Groups, group => group.CustomName == "Мои инструменты");
        Assert.Equal("custom.1", myGroup.Id);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolLine, ShortcutCatalog.ToolFreehand },
            myGroup.Items.Select(item => item.CommandId));
        Assert.True(myGroup.Items[0].Visible);
        Assert.False(myGroup.Items[1].Visible);
    }

    [Fact]
    public void Load_HiddenTools_KeepTheirPlaceInGroup()
    {
        ToolLayoutModel custom = BuildCustomizedLayout();
        _store.Save(custom);

        ToolLayoutGroup drawing = _store.Load().Groups.First(group => group.Id == ToolGroupRegistry.DrawingGroupId);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolSelect, ShortcutCatalog.ToolRectangle, ShortcutCatalog.ToolCircle },
            drawing.Items.Take(3).Select(item => item.CommandId));
        Assert.False(drawing.Items[1].Visible);
        Assert.True(drawing.Items[0].Visible);
        Assert.True(drawing.Items[2].Visible);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaultLayout()
    {
        File.WriteAllText(_store.FilePath, "{ это не json !!!");

        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), _store.Load());
    }

    [Fact]
    public void Load_ForeignFile_ReturnsDefaultLayout()
    {
        File.WriteAllText(_store.FilePath, JsonSerializer.Serialize(new { hello = "world", answer = 42 }));

        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), _store.Load());
    }

    [Fact]
    public void Load_NewerSchemaVersion_ReturnsDefaultLayout()
    {
        string json = JsonSerializer.Serialize(new
        {
            version = ToolLayoutModel.CurrentVersion + 1,
            groups = new[] { new { id = ToolGroupRegistry.DrawingGroupId, name = (string?)null, visible = true, items = Array.Empty<object>() } },
        });
        File.WriteAllText(_store.FilePath, json);

        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), _store.Load());
    }

    [Fact]
    public void Load_MigratesLegacyVersion_AndAppendsNewToolsToEndOfGroup()
    {
        // Файл до версионирования (version 0): только «Выбор» и своя группа с «Линией».
        string json = JsonSerializer.Serialize(new
        {
            version = 0,
            groups = new object[]
            {
                new
                {
                    id = ToolGroupRegistry.DrawingGroupId,
                    name = (string?)null,
                    visible = true,
                    items = new[] { new { commandId = ShortcutCatalog.ToolSelect, visible = false, order = 0 } },
                },
                new
                {
                    id = "custom.1",
                    name = "Мои инструменты",
                    visible = true,
                    items = new[] { new { commandId = ShortcutCatalog.ToolLine, visible = true, order = 0 } },
                },
            },
        });
        File.WriteAllText(_store.FilePath, json);

        ToolLayoutModel loaded = _store.Load();

        Assert.Equal(ToolLayoutModel.CurrentVersion, loaded.Version);
        Assert.Equal(2 + ToolGroupRegistry.DefaultGroups.Count - 1, loaded.Groups.Count);

        // Своя группа — на месте, инструмент из неё не дублируется в системных.
        ToolLayoutGroup custom = loaded.Groups.First(group => group.Id == "custom.1");
        Assert.Equal("Мои инструменты", custom.CustomName);
        Assert.Equal(ShortcutCatalog.ToolLine, Assert.Single(custom.Items).CommandId);

        // Новые инструменты группы добавлены в конец (риск R4), скрытый «Выбор» остался скрытым.
        ToolLayoutGroup drawing = loaded.Groups.First(group => group.Id == ToolGroupRegistry.DrawingGroupId);
        Assert.Equal(ShortcutCatalog.ToolSelect, drawing.Items[0].CommandId);
        Assert.False(drawing.Items[0].Visible);
        Assert.DoesNotContain(drawing.Items, item => item.CommandId == ShortcutCatalog.ToolLine);
        Assert.Equal(
            ToolGroupRegistry.DefaultGroups.First(group => group.Id == ToolGroupRegistry.DrawingGroupId).Buttons.Count - 1,
            drawing.Items.Count);
        for (int index = 1; index < drawing.Items.Count; index++)
        {
            Assert.True(drawing.Items[index].Visible);
            Assert.Equal(index, drawing.Items[index].Order);
        }
    }

    [Fact]
    public void Load_MapsFlyoutChildToItsButton_AndDropsUnknownCommands()
    {
        string json = JsonSerializer.Serialize(new
        {
            version = ToolLayoutModel.CurrentVersion,
            groups = new[]
            {
                new
                {
                    id = ToolGroupRegistry.DrawingGroupId,
                    name = (string?)null,
                    visible = true,
                    items = new object[]
                    {
                        new { commandId = ShortcutCatalog.ToolArc3Point, visible = true, order = 0 },
                        new { commandId = "tool.unknown", visible = true, order = 1 },
                    },
                },
            },
        });
        File.WriteAllText(_store.FilePath, json);

        ToolLayoutModel loaded = _store.Load();
        ToolLayoutGroup drawing = loaded.Groups.First(group => group.Id == ToolGroupRegistry.DrawingGroupId);

        // Под-инструмент вылета сводится к кнопке «Дуга», неизвестная команда отбрасывается.
        Assert.Equal(ShortcutCatalog.ToolArc, drawing.Items[0].CommandId);
        Assert.DoesNotContain(drawing.Items, item => item.CommandId == "tool.unknown");
    }

    [Fact]
    public void Save_WritesVersionedJson_AndCreatesSettingsDirectory()
    {
        _store.Save(ToolLayoutModel.CreateDefault());

        Assert.True(File.Exists(_store.FilePath));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_store.FilePath));
        Assert.Equal(ToolLayoutModel.CurrentVersion, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(
            ToolGroupRegistry.DefaultGroups.Count,
            document.RootElement.GetProperty("groups").GetArrayLength());
    }

    [Fact]
    public void Reset_DeletesUserLayout_AndReturnsDefault()
    {
        _store.Save(BuildCustomizedLayout());
        Assert.True(File.Exists(_store.FilePath));

        ToolLayoutModel reset = _store.Reset();

        Assert.False(File.Exists(_store.FilePath));
        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), reset);
        AssertLayoutsEqual(ToolLayoutModel.CreateDefault(), _store.Load());
    }

    /// <summary>
    /// Полная раскладка (инструмент живёт в одной группе): «Прямоугольник» скрыт
    /// в «Рисовании», «Линия» и «Кривая» переехали в свою группу «Мои инструменты»
    /// («Кривая» там скрыта).
    /// </summary>
    private static ToolLayoutModel BuildCustomizedLayout()
    {
        List<ToolLayoutGroup> groups = [];
        foreach (PaletteGroup registryGroup in ToolGroupRegistry.DefaultGroups)
        {
            List<ToolLayoutItem> items = registryGroup.Buttons
                .Select((button, index) => new ToolLayoutItem(button.Definition.CommandId, Visible: true, index))
                .ToList();

            if (registryGroup.Id == ToolGroupRegistry.DrawingGroupId)
            {
                items.RemoveAll(item =>
                    item.CommandId == ShortcutCatalog.ToolLine
                    || item.CommandId == ShortcutCatalog.ToolFreehand);
                int hiddenIndex = items.FindIndex(item => item.CommandId == ShortcutCatalog.ToolRectangle);
                items[hiddenIndex] = items[hiddenIndex] with { Visible = false };
                items = [.. items.Select((item, index) => item with { Order = index })];
            }

            groups.Add(new ToolLayoutGroup(registryGroup.Id, CustomName: null, Visible: true, items));
        }

        groups.Add(new ToolLayoutGroup(
            "custom.1",
            "Мои инструменты",
            Visible: true,
            [
                new ToolLayoutItem(ShortcutCatalog.ToolLine, Visible: true, Order: 0),
                new ToolLayoutItem(ShortcutCatalog.ToolFreehand, Visible: false, Order: 1),
            ]));

        return new ToolLayoutModel(ToolLayoutModel.CurrentVersion, groups);
    }

    private static void AssertLayoutsEqual(ToolLayoutModel expected, ToolLayoutModel actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Groups.Count, actual.Groups.Count);
        for (int groupIndex = 0; groupIndex < expected.Groups.Count; groupIndex++)
        {
            ToolLayoutGroup expectedGroup = expected.Groups[groupIndex];
            ToolLayoutGroup actualGroup = actual.Groups[groupIndex];
            Assert.Equal(expectedGroup.Id, actualGroup.Id);
            Assert.Equal(expectedGroup.CustomName, actualGroup.CustomName);
            Assert.Equal(expectedGroup.Visible, actualGroup.Visible);
            Assert.Equal(expectedGroup.Items.Count, actualGroup.Items.Count);
            for (int itemIndex = 0; itemIndex < expectedGroup.Items.Count; itemIndex++)
            {
                Assert.Equal(expectedGroup.Items[itemIndex], actualGroup.Items[itemIndex]);
            }
        }
    }
}
