using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Annotations;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Tools;
using SmetaNA.EditorUI.Tools.Drawing;
using SmetaNA.EditorUI.Tools.Editing;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.EditorUI.Vcb;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки полноты ленты (задача 6.1): недостающие кнопки подключены,
/// имена и подсказки — из общего каталога локализации, маршрутизация работает.
/// </summary>
public sealed class RibbonToolsTests
{
    private static IEnumerable<ToolDefinition> AllDefinitions() =>
        MainWindowViewModel.PrimaryRibbonDefinitions.Concat(MainWindowViewModel.AdvancedRibbonDefinitions);

    [Fact]
    public void Ribbon_IncludesArc3Point_FromDrawingModuleRegistration()
    {
        ToolDefinition? definition = AllDefinitions()
            .FirstOrDefault(candidate => candidate.CommandId == RegisterDrawingTools.ToolArc3PointId);

        Assert.NotNull(definition);
        Assert.Equal(LocalizationKeys.ToolArc3Point, definition!.NameKey);

        ToolDefinition? module = RegisterDrawingTools.RibbonDefinitions
            .FirstOrDefault(candidate => candidate.CommandId == RegisterDrawingTools.ToolArc3PointId);
        Assert.NotNull(module);
        Assert.Equal(module!.VcbKind, definition.VcbKind);
    }

    [Fact]
    public void Ribbon_IncludesDimensionTools_FromDimensionModuleRegistration()
    {
        Assert.Contains(
            AllDefinitions(),
            definition => definition.CommandId == RegisterDimensionTools.DimensionCommandId
                && definition.NameKey == LocalizationKeys.ToolDimension);
        Assert.Contains(
            AllDefinitions(),
            definition => definition.CommandId == RegisterDimensionTools.ScaleToDimensionCommandId
                && definition.NameKey == LocalizationKeys.ToolScaleToDimension);
    }

    [Fact]
    public void Ribbon_EveryTool_HasLocalizedNamesAndHints_InBothCultures()
    {
        var service = new LocalizationService();
        foreach (ToolDefinition definition in AllDefinitions())
        {
            foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
            {
                service.SetCulture(culture);
                string name = service.Get(definition.NameKey);
                Assert.False(string.IsNullOrWhiteSpace(name), $"{definition.CommandId}: имя пусто для {culture}");
                Assert.NotEqual(definition.NameKey, name);

                Assert.NotNull(definition.HintKey);
                string hint = service.Get(definition.HintKey!);
                Assert.False(string.IsNullOrWhiteSpace(hint), $"{definition.CommandId}: подсказка пуста для {culture}");
                Assert.NotEqual(definition.HintKey, hint);
            }
        }
    }

    [Fact]
    public void Ribbon_EveryToolCommand_HasUniqueShortcut()
    {
        var map = new ShortcutMap(ShortcutCatalog.Defaults);
        var gestures = new HashSet<string>(StringComparer.Ordinal);
        foreach (ToolDefinition definition in AllDefinitions())
        {
            Shortcut? gesture = map.Find(definition.CommandId);
            Assert.True(gesture is not null, $"{definition.CommandId}: нет горячей клавиши");
            Assert.True(gestures.Add(gesture!.Value.ToString()), $"{definition.CommandId}: клавиша занята");
        }
    }

    [Fact]
    public void Ribbon_Tools_AreActivableThroughModuleRouters()
    {
        var tools = new ToolController(new Scene(), new UndoStack());
        foreach (ToolDefinition definition in AllDefinitions())
        {
            if (definition.CommandId == ShortcutCatalog.ToolSelect)
            {
                // выбор — это отсутствие активного инструмента
                continue;
            }

            bool routed = false;
            if (definition.CommandId == ShortcutCatalog.ToolPushPull)
            {
                // Push/Pull активируется напрямую (как в MainWindowViewModel)
                tools.Activate(new PushPullTool());
                routed = true;
            }
            else if (RegisterDrawingTools.TryActivate(tools, definition.CommandId))
            {
                routed = true;
            }
            else if (RegisterEditingTools.Create(definition.CommandId) is { } editing)
            {
                tools.Activate(editing);
                routed = true;
            }
            else if (RegisterDimensionTools.CreateDescriptors().FirstOrDefault(d => d.Id == definition.CommandId)
                is { } dimension)
            {
                tools.Activate(dimension.Create());
                routed = true;
            }

            Assert.True(routed, $"{definition.CommandId}: инструмент не маршрутизируется");
            Assert.NotNull(tools.ActiveTool);
            Assert.Equal(definition.CommandId, tools.ActiveTool!.Id);
        }
    }

    [Fact]
    public void Ribbon_ToolIds_MatchCoreToolIds()
    {
        Assert.Equal(Core.Tools.Drawing.Arc3PointTool.ToolId, RegisterDrawingTools.ToolArc3PointId);
        Assert.Equal(Core.Tools.DimensionTool.ToolId, RegisterDimensionTools.DimensionCommandId);
        Assert.Equal(Core.Tools.ScaleToDimensionTool.ToolId, RegisterDimensionTools.ScaleToDimensionCommandId);
        Assert.Equal(Core.Tools.Editing.PaintBucketTool.ToolId, ShortcutCatalog.ToolPaintBucket);
    }

    [Fact]
    public void Ribbon_EveryTool_VcbKindIsDefinedOrNull()
    {
        // VCB-вид инструментов рисования согласован с регистрациями модуля
        foreach (ToolDefinition definition in RegisterDrawingTools.RibbonDefinitions)
        {
            ToolDefinition? ribbon = AllDefinitions()
                .FirstOrDefault(candidate => candidate.CommandId == definition.CommandId);
            Assert.NotNull(ribbon);
            Assert.Equal(definition.VcbKind, ribbon!.VcbKind);
        }

        Assert.All(
            AllDefinitions(),
            definition => Assert.True(definition.VcbKind is null || Enum.IsDefined(definition.VcbKind.Value)));
    }
}
