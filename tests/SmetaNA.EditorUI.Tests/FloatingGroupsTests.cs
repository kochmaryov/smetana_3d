using SmetaNA.EditorUI.FloatingGroups;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Перемещаемые группы (спека ui-redesign T3): персистентность размещений,
/// миграция схемы (R7), отрыв/возврат, лотки краёв и плавание из лотка.
/// </summary>
public sealed class FloatingGroupsTests
{
    private static List<ToolPaletteGroupViewModel> CreateGroups()
    {
        var localization = new LocalizationService();
        return
        [
            .. ToolGroupRegistry.DefaultGroups.Select(group =>
                new ToolPaletteGroupViewModel(localization, group, [])),
        ];
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"floating-groups-{Guid.NewGuid():N}.json");

    [Fact]
    public void Model_CreateDefault_AllGroupsInPalette()
    {
        FloatingGroupsModel model = FloatingGroupsModel.CreateDefault();

        Assert.Equal(FloatingGroupsModel.CurrentVersion, model.Version);
        Assert.Empty(model.Groups);
    }

    [Fact]
    public void Migrations_DropsUnknownAndAppendsNewGroups()
    {
        var model = new FloatingGroupsModel(
            FloatingGroupsModel.CurrentVersion,
            [
                new FloatingGroupPlacement("ghost", FloatingGroupState.Floating, 10, 20),
                new FloatingGroupPlacement("drawing", FloatingGroupState.Left, 0, 0),
            ]);

        FloatingGroupsModel upgraded = FloatingGroupsMigrations.UpgradeToCurrent(
            model,
            ["drawing", "editing"]);

        Assert.Equal(2, upgraded.Groups.Count);
        FloatingGroupPlacement drawing = Assert.Single(upgraded.Groups, p => p.GroupId == "drawing");
        Assert.Equal(FloatingGroupState.Left, drawing.State);
        FloatingGroupPlacement editing = Assert.Single(upgraded.Groups, p => p.GroupId == "editing");
        Assert.Equal(FloatingGroupState.Palette, editing.State);
        Assert.DoesNotContain(upgraded.Groups, p => p.GroupId == "ghost");
    }

    [Fact]
    public void Store_RoundTripsPlacements()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            var model = new FloatingGroupsModel(
                FloatingGroupsModel.CurrentVersion,
                [
                    new FloatingGroupPlacement("drawing", FloatingGroupState.Floating, 120, 340),
                    new FloatingGroupPlacement("editing", FloatingGroupState.Right, 0, 0),
                ]);

            store.Save(model);
            FloatingGroupsModel loaded = store.Load();

            // Миграция дополняет модель всеми известными группами реестра.
            Assert.Equal(ToolGroupRegistry.DefaultGroups.Count, loaded.Groups.Count);
            FloatingGroupPlacement drawing = Assert.Single(loaded.Groups, p => p.GroupId == "drawing");
            Assert.Equal(FloatingGroupState.Floating, drawing.State);
            Assert.Equal(120, drawing.X);
            Assert.Equal(340, drawing.Y);
            Assert.Equal(
                FloatingGroupState.Right,
                Assert.Single(loaded.Groups, p => p.GroupId == "editing").State);
            Assert.Single(
                loaded.Groups,
                p => p.GroupId == "operations" && p.State == FloatingGroupState.Palette);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Store_CorruptedFile_ReturnsDefault()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, "{ not valid json !!!");
            var store = new JsonFloatingGroupsStore(path);

            FloatingGroupsModel loaded = store.Load();

            Assert.Empty(loaded.Groups);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_DetachMakesFloatingAndSaves()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            FloatingGroupViewModel? requested = null;
            manager.FloatingRequested += floating => requested = floating;

            groups[0].RequestDetach();

            Assert.True(groups[0].IsDetached);
            FloatingGroupViewModel floating = Assert.Single(manager.Floating);
            Assert.Same(floating, requested);
            Assert.Equal(groups[0], floating.Group);

            // Персистентность: перезагрузка восстанавливает размещение.
            var reloaded = new FloatingGroupsViewModel(new JsonFloatingGroupsStore(path), CreateGroups());
            FloatingGroupViewModel restored = Assert.Single(reloaded.Floating);
            Assert.Equal(groups[0].Id, restored.GroupId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_EndDragNearEdge_DocksToTray()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            groups[1].RequestDetach();
            FloatingGroupViewModel floating = manager.Floating[0];
            bool windowClosed = false;
            manager.PlacementRemoved += _ => windowClosed = true;

            manager.EndDrag(floating, FloatingGroupState.Right);

            Assert.True(windowClosed);
            FloatingGroupViewModel docked = Assert.Single(manager.DockedRight);
            Assert.Same(floating, docked);
            Assert.Empty(manager.Floating);
            Assert.Equal(FloatingGroupState.Right, docked.State);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_MakeFloating_FromTrayOpensWindow()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            groups[2].RequestDetach();
            manager.EndDrag(manager.Floating[0], FloatingGroupState.Bottom);
            FloatingGroupViewModel? requested = null;
            manager.FloatingRequested += floating => requested = floating;

            manager.MakeFloating(manager.DockedBottom[0], 300, 200);

            Assert.Empty(manager.DockedBottom);
            FloatingGroupViewModel floating = Assert.Single(manager.Floating);
            Assert.Same(floating, requested);
            Assert.Equal(300, floating.X);
            Assert.Equal(200, floating.Y);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_ReturnToPalette_ReattachesGroup()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            groups[0].RequestDetach();
            FloatingGroupViewModel floating = manager.Floating[0];
            bool windowClosed = false;
            manager.PlacementRemoved += _ => windowClosed = true;

            groups[0].RequestReturnToPalette();

            Assert.False(groups[0].IsDetached);
            Assert.True(windowClosed);
            Assert.Empty(manager.AllPlacements());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_Move_UpdatesDockHint()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            groups[0].RequestDetach();
            FloatingGroupViewModel floating = manager.Floating[0];

            manager.Move(floating, 15, 25, FloatingGroupState.Left);

            Assert.Equal(15, floating.X);
            Assert.Equal(25, floating.Y);
            Assert.Equal(FloatingGroupState.Left, manager.DockHint);

            manager.EndDrag(floating, null);

            Assert.Null(manager.DockHint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_StateOf_ReflectsPlacement()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);

            Assert.Equal(FloatingGroupState.Palette, manager.StateOf(groups[0].Id));

            groups[0].RequestDetach();
            Assert.Equal(FloatingGroupState.Floating, manager.StateOf(groups[0].Id));

            manager.EndDrag(manager.Floating[0], FloatingGroupState.Bottom);
            Assert.Equal(FloatingGroupState.Bottom, manager.StateOf(groups[0].Id));

            // Неизвестная группа — как группа в палитре.
            Assert.Equal(FloatingGroupState.Palette, manager.StateOf("нет-такой"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Manager_SetDetached_TogglesLikePaletteButtons()
    {
        string path = TempFile();
        try
        {
            var store = new JsonFloatingGroupsStore(path);
            List<ToolPaletteGroupViewModel> groups = CreateGroups();
            var manager = new FloatingGroupsViewModel(store, groups);
            FloatingGroupViewModel? opened = null;
            manager.FloatingRequested += floating => opened = floating;

            manager.SetDetached(groups[2].Id, detached: true);

            Assert.True(groups[2].IsDetached);
            Assert.Same(manager.Floating.Single(), opened);
            Assert.NotEqual(FloatingGroupState.Palette, manager.StateOf(groups[2].Id));

            manager.SetDetached(groups[2].Id, detached: false);

            Assert.False(groups[2].IsDetached);
            Assert.Empty(manager.AllPlacements());
            Assert.Equal(FloatingGroupState.Palette, manager.StateOf(groups[2].Id));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
