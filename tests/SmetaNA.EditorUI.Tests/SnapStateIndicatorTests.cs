using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Аудит §15.4 (P0): индикатор блокировки (R-24) и пространство координат
/// числового ввода (R-16, S14).
/// </summary>
public sealed class SnapStateIndicatorTests
{
    private static ProjectTabViewModel CreateViewModel(Scene? scene = null) =>
        new(new LocalizationService(), scene ?? new Scene(), 1);

    [Fact]
    public void SnapStateText_EmptyByDefault()
    {
        ProjectTabViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.HasSnapState);
        Assert.Equal(string.Empty, viewModel.SnapStateText);
    }

    [Fact]
    public void SnapStateText_ReportsAxisLock()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Tools.Context.ToggleSnapLock(SnapModifiers.LockAxisX);

        Assert.True(viewModel.HasSnapState);
        Assert.Contains("X", viewModel.SnapStateText);
        Assert.Contains(nameof(ProjectTabViewModel.SnapStateText), changed);
        Assert.Contains(nameof(ProjectTabViewModel.HasSnapState), changed);
    }

    [Fact]
    public void SnapStateText_ReportsModeAndClearsOnEscape()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        ILocalizationService localization = new LocalizationService();
        viewModel.Tools.Context.ToggleSnapLock(SnapModifiers.LockAxisZ);
        viewModel.Tools.Context.CycleInferenceMode();

        Assert.Equal(
            localization.Get(LocalizationKeys.SnapLockAxisZ)
                + " · " + localization.Get(LocalizationKeys.SnapModePointsOnly),
            viewModel.SnapStateText);

        viewModel.Tools.Context.ResetSnapLocks();

        Assert.Equal(
            localization.Get(LocalizationKeys.SnapModePointsOnly),
            viewModel.SnapStateText);
    }

    [Fact]
    public void CoordinateSpace_WithoutContext_StaysWorld()
    {
        ProjectTabViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.HasCoordinateContext);
        Assert.False(viewModel.IsLocalCoordinateSpace);

        viewModel.Tools.Context.ToggleCoordinateSpace();

        Assert.False(viewModel.IsLocalCoordinateSpace);
    }

    [Fact]
    public void UpdateCoordinateContext_ContainerEnablesLocalSpace()
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            Name = "Detail",
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        scene.Root.AddChild(group);
        ProjectTabViewModel viewModel = CreateViewModel(scene);

        viewModel.UpdateCoordinateContext(group);

        Assert.True(viewModel.HasCoordinateContext);
        viewModel.ToggleCoordinateSpace();
        Assert.True(viewModel.IsLocalCoordinateSpace);

        viewModel.UpdateCoordinateContext(null);

        Assert.False(viewModel.HasCoordinateContext);
        Assert.False(viewModel.IsLocalCoordinateSpace);
    }

    [Fact]
    public void LocalCoordinates_ConvertInputIntoWorldAxes()
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        scene.Root.AddChild(group);
        ProjectTabViewModel viewModel = CreateViewModel(scene);

        viewModel.UpdateCoordinateContext(group);
        viewModel.ToggleCoordinateSpace();

        Vector3d world = viewModel.Tools.Context.ToWorldPoint(new Vector3d(10, 0, 0));

        Assert.Equal(110, world.X, 9);
        Assert.Equal(0, world.Y, 9);
        Assert.Equal(0, world.Z, 9);
    }

    [Fact]
    public void CommitPoint_AppliesLocalSpaceThroughTools()
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        scene.Root.AddChild(group);
        ProjectTabViewModel viewModel = CreateViewModel(scene);
        viewModel.UpdateCoordinateContext(group);
        viewModel.ToggleCoordinateSpace();

        var tool = new RecordingPointTool();
        viewModel.Tools.Activate(tool);

        Assert.True(viewModel.Tools.CommitPoint(new Vector3d(10, 0, 0), relative: false));
        Assert.Equal(110, tool.Applied!.Value.X, 9);
        Assert.False(tool.AppliedRelative);
    }

    private sealed class RecordingPointTool : ITool
    {
        public Vector3d? Applied { get; private set; }

        public bool AppliedRelative { get; private set; }

        public string Id => "test.recording";

        public ToolVcbState Vcb => ToolVcbState.Inactive;

        public bool IsInteracting => false;

        public ToolPreview Preview => ToolPreview.Empty;

        public SnapResult CurrentSnap => SnapResult.Empty;

        public event Action? StateChanged
        {
            add { }
            remove { }
        }

        public void Activate(ToolContext context)
        {
        }

        public void Deactivate()
        {
        }

        public bool OnPointer(PointerInput input) => false;

        public bool OnKey(KeyInput input) => false;

        public bool TryApplyValue(double value) => false;

        public bool TryApplyPoint(Vector3d point, bool relative)
        {
            Applied = point;
            AppliedRelative = relative;
            return true;
        }

        public void Cancel()
        {
        }

        public void ResetSnap()
        {
        }
    }
}
