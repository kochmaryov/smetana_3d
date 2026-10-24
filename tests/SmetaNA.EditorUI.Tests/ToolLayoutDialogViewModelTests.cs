using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolLayout;
using SmetaNA.EditorUI.ToolPalette;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки диалога «Настройка панели инструментов» (трек C, T3/T8): дерево
/// «Все инструменты», порядок и видимость на «Панели», свои группы, свойства
/// кнопки, применение раскладки без перезапуска и round-trip через хранилище.
/// </summary>
public sealed class ToolLayoutDialogViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly LocalizationService _localization = new();
    private readonly ShortcutMap _shortcuts = new();

    public ToolLayoutDialogViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "smetana-toollayout-dlg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    private ToolLayoutDialogViewModel CreateDialog(ToolLayoutModel? layout = null) =>
        new(_localization, _shortcuts, layout ?? ToolLayoutModel.CreateDefault());

    private JsonToolLayoutStore CreateStore() =>
        new(Path.Combine(_root, JsonToolLayoutStore.FileName));

    [Fact]
    public void AllToolsTree_ShowsRegistryGroupsWithButtonsAndFlyoutChildren()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();

        Assert.Equal(ToolGroupRegistry.DefaultGroups.Count, dialog.AllGroups.Count);
        ToolLayoutAllGroupNode drawing = dialog.AllGroups[0];
        Assert.Equal(ToolGroupRegistry.DrawingGroupId, drawing.Id);
        Assert.Equal(
            ToolGroupRegistry.DefaultGroups[0].Buttons.Count,
            drawing.Items.Count);

        // «Дуга» раскрывается под-инструментами, которые следуют за своей кнопкой.
        ToolLayoutToolNode arc = drawing.Items.Single(item => item.CommandId == ShortcutCatalog.ToolArc);
        Assert.True(arc.HasFlyout);
        ToolLayoutToolNode arcChild = Assert.Single(arc.FlyoutItems);
        Assert.Equal(ShortcutCatalog.ToolArc3Point, arcChild.CommandId);
        Assert.Same(arc, arcChild.FlyoutOwner);
    }

    [Fact]
    public void SetToolVisible_HidesButton_KeepsItsPlace()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutPanelGroupNode drawing = dialog.PanelGroups[0];

        dialog.SetToolVisible(ShortcutCatalog.ToolLine, false);

        ToolLayoutToolNode line = drawing.Items.Single(item => item.CommandId == ShortcutCatalog.ToolLine);
        Assert.False(line.IsVisible);
        Assert.Equal(1, drawing.Items.IndexOf(line));
        Assert.Equal(
            ToolGroupRegistry.DefaultGroups[0].Buttons.Count - 1,
            drawing.Items.Count(item => item.IsVisible));
    }

    [Fact]
    public void HideButton_HidesItsFlyoutSubToolsToo()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();

        dialog.SetToolVisible(ShortcutCatalog.ToolArc, false);

        ToolLayoutToolNode arc = dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolArc);
        Assert.False(arc.IsVisible);
        Assert.All(arc.FlyoutItems, child => Assert.False(child.IsVisible));
    }

    [Fact]
    public void MoveUpAndDown_ReorderVisibleButtons()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutToolNode select = dialog.PanelGroups[0].Items[0];
        ToolLayoutToolNode line = dialog.PanelGroups[0].Items[1];

        dialog.SelectTool(line);
        dialog.MoveUpCommand.Execute(null);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolLine, ShortcutCatalog.ToolSelect },
            dialog.PanelGroups[0].Items.Take(2).Select(item => item.CommandId));

        dialog.MoveDownCommand.Execute(null);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolSelect, ShortcutCatalog.ToolLine },
            dialog.PanelGroups[0].Items.Take(2).Select(item => item.CommandId));
        Assert.Same(select, dialog.PanelGroups[0].Items[0]);
    }

    [Fact]
    public void MoveUp_SkipsHiddenButtons()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutPanelGroupNode drawing = dialog.PanelGroups[0];
        dialog.SetToolVisible(ShortcutCatalog.ToolRectangle, false); // скрыта, но остаётся на месте

        ToolLayoutToolNode circle = drawing.Items.Single(item => item.CommandId == ShortcutCatalog.ToolCircle);
        dialog.SelectTool(circle);
        dialog.MoveUpCommand.Execute(null);

        // «Круг» встаёт перед «Линией», минуя скрытый «Прямоугольник».
        Assert.Equal(
            new[] { ShortcutCatalog.ToolSelect, ShortcutCatalog.ToolCircle, ShortcutCatalog.ToolLine },
            drawing.Items.Take(3).Select(item => item.CommandId));
    }

    [Fact]
    public void MoveToolTo_MovesBetweenGroups()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutPanelGroupNode editing = dialog.PanelGroups[1];
        ToolLayoutToolNode line = dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolLine);

        dialog.MoveToolTo(line, editing, 0);

        Assert.Same(editing, line.Group);
        Assert.Equal(ShortcutCatalog.ToolLine, editing.Items[0].CommandId);
        Assert.DoesNotContain(dialog.PanelGroups[0].Items, item => item.CommandId == ShortcutCatalog.ToolLine);
    }

    [Fact]
    public void AddToPanel_MovesSelectedToolIntoSelectedGroup()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutPanelGroupNode custom = dialog.CreateGroup("Хлебные крошки");
        dialog.SelectGroup(custom);
        dialog.SelectTool(dialog.PanelGroups[0].Items[0]);

        dialog.AddToPanelCommand.Execute(null);

        ToolLayoutToolNode moved = Assert.Single(custom.Items);
        Assert.Equal(ShortcutCatalog.ToolSelect, moved.CommandId);
        Assert.True(moved.IsVisible);
    }

    [Fact]
    public void RemoveFromPanel_HidesTool_KeepsItInGroup()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutToolNode select = dialog.PanelGroups[0].Items[0];
        dialog.SelectTool(select);

        dialog.RemoveFromPanelCommand.Execute(null);

        Assert.False(select.IsVisible);
        Assert.Same(dialog.PanelGroups[0], select.Group);
    }

    [Fact]
    public void CreateRenameDelete_CustomGroup_SystemGroupsAreReadOnly()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        int systemGroups = dialog.PanelGroups.Count;

        ToolLayoutPanelGroupNode custom = dialog.CreateGroup("   ");
        Assert.Equal(systemGroups + 1, dialog.PanelGroups.Count);
        Assert.True(custom.IsCustom);
        Assert.Equal(dialog.DefaultGroupName, custom.DisplayText);

        dialog.RenameGroup(custom, "  Мои инструменты  ");
        Assert.Equal("Мои инструменты", custom.CustomName);
        Assert.Equal("Мои инструменты", custom.DisplayText);

        // Системную группу можно только скрыть.
        ToolLayoutPanelGroupNode drawing = dialog.PanelGroups[0];
        Assert.False(drawing.CanRename);
        Assert.False(drawing.CanDelete);
        dialog.RenameGroup(drawing, "Взлом");
        dialog.DeleteGroup(drawing);
        Assert.Equal(systemGroups + 1, dialog.PanelGroups.Count);
        Assert.Equal(ToolGroupRegistry.DrawingGroupId, dialog.PanelGroups[0].Id);

        drawing.IsVisible = false;
        Assert.False(dialog.BuildLayout().Groups[0].Visible);
    }

    [Fact]
    public void DeleteCustomGroup_ReturnsToolsToDefaultGroups()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutPanelGroupNode custom = dialog.CreateGroup("Мои инструменты");
        ToolLayoutToolNode line = dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolLine);
        dialog.MoveToolTo(line, custom, 0);
        Assert.Single(custom.Items);

        dialog.DeleteGroup(custom);

        Assert.DoesNotContain(dialog.PanelGroups, group => group.Id == custom.Id);
        Assert.Same(dialog.PanelGroups[0], line.Group);
        Assert.Contains(dialog.PanelGroups[0].Items, item => item.CommandId == ShortcutCatalog.ToolLine);
    }

    [Fact]
    public void Reset_RestoresDefaultLayout()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        dialog.SetToolVisible(ShortcutCatalog.ToolLine, false);
        dialog.CreateGroup("Мои инструменты");
        dialog.MoveGroupTo(dialog.PanelGroups[1], 0);

        dialog.Reset();

        Assert.Equal(ToolGroupRegistry.DefaultGroups.Count, dialog.PanelGroups.Count);
        Assert.Equal(ToolGroupRegistry.DrawingGroupId, dialog.PanelGroups[0].Id);
        Assert.All(
            ToolGroupRegistry.DefaultGroups.SelectMany(group => group.Buttons),
            button => Assert.True(
                dialog.PanelGroups.SelectMany(group => group.Items)
                    .Single(item => item.CommandId == button.Definition.CommandId).IsVisible));
    }

    [Fact]
    public void BuildLayout_KeepsOrderVisibilityAndCustomGroups()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        dialog.SetToolVisible(ShortcutCatalog.ToolRectangle, false);
        ToolLayoutToolNode circle = dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolCircle);
        dialog.MoveToolTo(circle, dialog.PanelGroups[0], 0);
        ToolLayoutPanelGroupNode custom = dialog.CreateGroup("Мои инструменты");
        dialog.MoveToolTo(
            dialog.PanelGroups[1].Items.Single(item => item.CommandId == ShortcutCatalog.ToolMove),
            custom,
            0);

        ToolLayoutModel layout = dialog.BuildLayout();

        Assert.Equal(ToolLayoutModel.CurrentVersion, layout.Version);
        ToolLayoutGroup drawing = layout.Groups[0];
        Assert.Equal(
            new[] { ShortcutCatalog.ToolCircle, ShortcutCatalog.ToolSelect, ShortcutCatalog.ToolLine },
            drawing.Items.Take(3).Select(item => item.CommandId));
        Assert.Equal(0, drawing.Items[0].Order);
        Assert.False(drawing.Items.Single(item => item.CommandId == ShortcutCatalog.ToolRectangle).Visible);
        ToolLayoutGroup myGroup = layout.Groups[^1];
        Assert.Equal("Мои инструменты", myGroup.CustomName);
        Assert.Equal(ShortcutCatalog.ToolMove, Assert.Single(myGroup.Items).CommandId);
    }

    [Fact]
    public void ApplyLayout_ResolvesPaletteGroupsWithoutRestart()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        dialog.SetToolVisible(ShortcutCatalog.ToolFreehand, false);
        ToolLayoutPanelGroupNode custom = dialog.CreateGroup("Мои инструменты");
        dialog.MoveToolTo(
            dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolArc),
            custom,
            0);
        ToolLayoutModel layout = dialog.BuildLayout();

        IReadOnlyList<PaletteGroup> palette = ToolLayoutPaletteResolver.Resolve(layout);

        ToolLayoutGroup? systemDrawing = layout.Groups[0];
        Assert.Equal(systemDrawing.Id, palette[0].Id);
        // Скрытая кнопка не показывается, порядок — из раскладки.
        Assert.DoesNotContain(palette[0].Buttons, button => button.Definition.CommandId == ShortcutCatalog.ToolFreehand);
        // «Дуга» переехала в свою группу вместе с вылетом.
        PaletteGroup myGroup = palette[^1];
        Assert.Equal("Мои инструменты", myGroup.NameKey);
        PaletteButton arcButton = Assert.Single(myGroup.Buttons);
        Assert.Equal(ShortcutCatalog.ToolArc, arcButton.Definition.CommandId);
        Assert.NotEmpty(arcButton.FlyoutItems);
    }

    [Fact]
    public void ApplyLayout_HiddenGroupAndButtons_AreNotShown()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        dialog.PanelGroups[1].IsVisible = false; // «Правка» скрыта целиком
        dialog.SetToolVisible(ShortcutCatalog.ToolLine, false);

        IReadOnlyList<PaletteGroup> palette = ToolLayoutPaletteResolver.Resolve(dialog.BuildLayout());

        Assert.DoesNotContain(palette, group => group.Id == ToolGroupRegistry.EditingGroupId);
        ToolLayoutGroup drawing = dialog.BuildLayout().Groups[0];
        PaletteGroup resolved = palette[0];
        Assert.Equal(drawing.Items.Count(item => item.Visible), resolved.Buttons.Count);
    }

    [Fact]
    public void RoundTrip_ThroughStore_KeepsCustomGroupsAndOrder()
    {
        JsonToolLayoutStore store = CreateStore();
        ToolLayoutDialogViewModel first = CreateDialog();
        first.SetToolVisible(ShortcutCatalog.ToolPolygon, false);
        ToolLayoutPanelGroupNode custom = first.CreateGroup("Мои инструменты");
        first.MoveToolTo(
            first.PanelGroups[1].Items.Single(item => item.CommandId == ShortcutCatalog.ToolMove),
            custom,
            0);
        first.MoveToolTo(
            first.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolLine),
            custom,
            1);
        store.Save(first.BuildLayout());

        // «Перезапуск»: новое хранилище и новый диалог над сохранённой раскладкой.
        JsonToolLayoutStore restarted = new(store.FilePath);
        ToolLayoutDialogViewModel second = CreateDialog(restarted.Load());

        ToolLayoutPanelGroupNode restored = second.PanelGroups[^1];
        Assert.Equal("Мои инструменты", restored.DisplayText);
        Assert.Equal("custom.1", restored.Id);
        Assert.Equal(
            new[] { ShortcutCatalog.ToolMove, ShortcutCatalog.ToolLine },
            restored.Items.Select(item => item.CommandId));
        Assert.False(second.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolPolygon).IsVisible);
    }

    [Fact]
    public void Search_FiltersTreeByNameAndGesture()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();

        // Жест под-инструмента «Дуга (3 точки)» — фильтр поднимает и кнопку с вылетом.
        dialog.SearchText = "Shift+A";
        foreach (ToolLayoutAllGroupNode group in dialog.AllGroups)
        {
            foreach (ToolLayoutToolNode tool in group.Items)
            {
                Assert.Equal(tool.CommandId == ShortcutCatalog.ToolArc, tool.IsMatch);
            }
        }

        dialog.SearchText = "tool.line";
        Assert.All(
            dialog.AllGroups.SelectMany(group => group.Items),
            tool => Assert.Equal(tool.CommandId == ShortcutCatalog.ToolLine, tool.IsMatch));

        dialog.SearchText = string.Empty;
        Assert.All(dialog.AllGroups.SelectMany(group => group.Items), tool => Assert.True(tool.IsMatch));
    }

    [Fact]
    public void Selection_PopulatesButtonProperties()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        ToolLayoutToolNode line = dialog.PanelGroups[0].Items.Single(item => item.CommandId == ShortcutCatalog.ToolLine);

        dialog.SelectTool(line);

        Assert.True(dialog.HasSelectedTool);
        Assert.True(line.IsSelected);
        Assert.Equal(_localization.Get(line.NameKey), line.Name);
        Assert.NotEqual(line.NameKey, line.Name);
        Assert.Equal(dialog.PanelGroups[0].DisplayText, line.GroupName);
        Assert.Equal(_shortcuts.Find(ShortcutCatalog.ToolLine)?.ToString(), line.Gesture);
        Assert.NotEqual(dialog.PropertyVcbText, line.VcbText);
    }

    [Fact]
    public void ApplyAndCancel_RequestDialogClose()
    {
        ToolLayoutDialogViewModel dialog = CreateDialog();
        List<bool> closes = [];
        dialog.CloseRequested += closes.Add;

        dialog.ApplyCommand.Execute(null);
        dialog.CancelCommand.Execute(null);

        Assert.Equal(new[] { true, false }, closes);
    }
}
