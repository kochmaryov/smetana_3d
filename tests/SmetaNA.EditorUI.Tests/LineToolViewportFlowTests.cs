using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>Сценарий рисования через полный путь UI: палитра → пустая сцена → камера по умолчанию.</summary>
public class LineToolViewportFlowTests
{
    [Fact]
    public void LineTool_ViaViewModel_EmptyScene_PerspectiveCamera_Commits()
    {
        var localization = new LocalizationService();
        var theme = new ThemeService();
        var session = new EditorSession();
        var vm = new MainWindowViewModel(localization, theme, session);

        // активация как из палитры
        vm.ActivateToolById(ShortcutCatalog.ToolLine);
        Assert.True(session.Tools.IsActive, "Инструмент не активировался.");

        // вьюпорт как UpdateToolViewport: камера GL по умолчанию, 1280x800
        var camera = new Camera();
        camera.AspectRatio = 1280.0 / 800.0;
        session.Tools.Context.Viewport = InferenceViewport.CreatePerspective(
            camera.EyePosition, camera.Forward, camera.Up, camera.FieldOfViewY, 1240, 620);

        int before = session.Scene.EnumerateNodes().Count();

        // клики как в ViewportView: (0.4w, 0.55h) и (0.6w, 0.55h)
        bool p1 = session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.4, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));
        bool p2 = session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.6, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));
        bool enter = session.Tools.RouteKey(new KeyInput(ToolKey.Enter));

        int after = session.Scene.EnumerateNodes().Count();
        Assert.True(p1 && p2 && enter, $"press1={p1} press2={p2} enter={enter}");
        Assert.True(after > before, $"Узлы: {before} -> {after}.");
    }
}
