using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// W5: инструмент «Точка сборки» через полный путь UI — палитра → вьюпорт →
/// snap в сцене → отмена одной записью истории.
/// </summary>
public sealed class SnapToolFlowTests
{
    private static (MainWindowViewModel ViewModel, EditorSession Session) Setup()
    {
        var session = new EditorSession();
        var viewModel = new MainWindowViewModel(new LocalizationService(), new ThemeService(), session);
        viewModel.NewProjectCommand.Execute(null);
        return (viewModel, session);
    }

    [Fact]
    public void SnapTool_IsRegisteredInPalette_AndActivates()
    {
        (MainWindowViewModel viewModel, EditorSession session) = Setup();

        Assert.NotNull(ToolPalette.ToolGroupRegistry.FindButton(ShortcutCatalog.ToolSnap));

        viewModel.ActivateToolById(ShortcutCatalog.ToolSnap);

        Assert.True(session.Tools.IsActive);
        Assert.Equal(Core.Tools.Drawing.SnapTool.ToolId, session.Tools.ActiveTool?.Id);
    }

    [Fact]
    public void SnapTool_TwoClicks_AddsSnap_AndUndoRemovesIt()
    {
        (MainWindowViewModel viewModel, EditorSession session) = Setup();
        viewModel.ActivateToolById(ShortcutCatalog.ToolSnap);

        var camera = new Camera();
        camera.AspectRatio = 1280.0 / 800.0;
        session.Tools.Context.Viewport = InferenceViewport.CreatePerspective(
            camera.EyePosition, camera.Forward, camera.Up, camera.FieldOfViewY, 1240, 620);

        int before = session.Scene.EnumerateNodes().Count();
        bool p1 = session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.45, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));
        bool p2 = session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.55, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));

        SnapNode snap = Assert.Single(session.Scene.EnumerateNodes().OfType<SnapNode>());
        Assert.True(p1 && p2);
        Assert.Equal(before + 1, session.Scene.EnumerateNodes().Count());
        Assert.Equal(1, snap.Frame.Direction.Length(), 12);

        session.History.Undo();
        Assert.Empty(session.Scene.EnumerateNodes().OfType<SnapNode>());
    }
}
