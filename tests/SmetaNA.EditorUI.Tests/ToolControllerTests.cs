using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Tools;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>Маршрутизация ввода вьюпорта в активный инструмент.</summary>
public class ToolControllerTests
{
    [Fact]
    public void Activate_SetsActiveTool_AndActivatesIt()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();

        controller.Activate(tool);

        Assert.Same(tool, controller.ActiveTool);
        Assert.True(controller.IsActive);
        Assert.True(tool.IsOn);
    }

    [Fact]
    public void Activate_ReplacesPreviousTool_WithCancel()
    {
        ToolController controller = CreateController();
        var first = new FakeTool();
        var second = new FakeTool();
        controller.Activate(first);

        controller.Activate(second);

        Assert.Same(second, controller.ActiveTool);
        Assert.True(first.IsOff);
        Assert.Equal(1, first.CancelCount); // Deactivate отменяет незавершённое действие
    }

    [Fact]
    public void Deactivate_CancelsTool_AndResetsPreview()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();
        controller.Activate(tool);
        tool.PublishPreview(new ToolPreview([new ToolPreviewLine(Vector3d.Zero, Vector3d.UnitX)]));

        controller.Deactivate();

        Assert.False(controller.IsActive);
        Assert.True(tool.IsOff);
        Assert.Equal(1, tool.CancelCount);
        Assert.True(controller.Preview.IsEmpty);
    }

    [Fact]
    public void RoutePointer_ForwardsToActiveTool()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();
        controller.Activate(tool);
        PointerInput input = PointerInput.Press(new Vector2d(10, 20), PointerButton.Left);

        bool handled = controller.RoutePointer(input);

        Assert.True(handled);
        Assert.Equal(new[] { input }, tool.Pointers);
    }

    [Fact]
    public void RouteKey_ForwardsToActiveTool()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();
        controller.Activate(tool);
        KeyInput input = new(ToolKey.Escape, ToolModifiers.Shift);

        bool handled = controller.RouteKey(input);

        Assert.True(handled);
        Assert.Equal(new[] { input }, tool.Keys);
    }

    [Fact]
    public void CommitValue_ForwardsToActiveTool()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();
        controller.Activate(tool);

        bool applied = controller.CommitValue(25.5);

        Assert.True(applied);
        Assert.Equal(new[] { 25.5 }, tool.Values);
    }

    [Fact]
    public void Routing_WithoutActiveTool_ReturnsFalse()
    {
        ToolController controller = CreateController();

        Assert.False(controller.RoutePointer(PointerInput.Move(new Vector2d(1, 1))));
        Assert.False(controller.RouteKey(new KeyInput(ToolKey.Escape)));
        Assert.False(controller.CommitValue(10));
        Assert.True(controller.Preview.IsEmpty);
        Assert.Equal(ToolVcbState.Inactive, controller.Vcb);
    }

    [Fact]
    public void Changed_RaisedOnToolStateChanged()
    {
        ToolController controller = CreateController();
        var tool = new FakeTool();
        controller.Activate(tool);
        int changed = 0;
        controller.Changed += () => changed++;

        tool.PublishPreview(new ToolPreview(labels: [new ToolPreviewLabel(Vector3d.Zero, "10 mm")]));
        tool.PublishVcb(ToolValueKind.Length, 10, true);

        Assert.Equal(2, changed);
        Assert.False(controller.Preview.IsEmpty);
        Assert.True(controller.Vcb.IsActive);
    }

    [Fact]
    public void ProjectTabViewModel_CarriesToolsController()
    {
        ToolController controller = CreateController();
        var viewModel = new ProjectTabViewModel(
            new Localization.LocalizationService(),
            new Scene(),
            1,
            controller);

        Assert.Same(controller, viewModel.Tools);
    }

    private static ToolController CreateController()
        => new(new Scene(), new UndoStack());

    private sealed class FakeTool : ToolBase
    {
        public override string Id => "test.tool";

        public bool IsOn { get; private set; }

        public bool IsOff { get; private set; }

        public int CancelCount { get; private set; }

        public List<PointerInput> Pointers { get; } = [];

        public List<KeyInput> Keys { get; } = [];

        public List<double> Values { get; } = [];

        public override void Cancel() => CancelCount++;

        public override bool OnPointer(PointerInput input)
        {
            Pointers.Add(input);
            return true;
        }

        public override bool OnKey(KeyInput input)
        {
            Keys.Add(input);
            return true;
        }

        public override bool TryApplyValue(double value)
        {
            Values.Add(value);
            return true;
        }

        protected override void OnActivated() => IsOn = true;

        protected override void OnDeactivated() => IsOff = true;

        public void PublishPreview(ToolPreview preview) => SetPreview(preview);

        public void PublishVcb(ToolValueKind kind, double? value, bool isActive) => SetVcb(kind, value, isActive);
    }
}
