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
/// Сценарий «нарисовал прямоугольник → выдавил его» через полный путь UI:
/// палитра → вьюпорт → перетаскивание грани. Регрессия: выдавливание плоской
/// грани должно строить тело со стенками, а не переносить грань в пустоте.
/// </summary>
public class RectanglePushPullFlowTests
{
    [Fact]
    public void Rectangle_ThenPushPull_BuildsSolid()
    {
        var localization = new LocalizationService();
        var theme = new ThemeService();
        var session = new EditorSession();
        var vm = new MainWindowViewModel(localization, theme, session);

        var camera = new Camera();
        camera.AspectRatio = 1240.0 / 620.0;
        session.Tools.Context.Viewport = InferenceViewport.CreatePerspective(
            camera.EyePosition, camera.Forward, camera.Up, camera.FieldOfViewY, 1240, 620);

        vm.ActivateToolById(ShortcutCatalog.ToolRectangle);
        Assert.True(session.Tools.IsActive, "Инструмент «Прямоугольник» не активировался.");

        session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.4, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));
        session.Tools.RoutePointer(new PointerInput(
            new Vector2d(1240 * 0.6, 620 * 0.55), PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));

        MeshNode node = session.Scene.EnumerateNodes().OfType<MeshNode>().Single();
        Assert.Single(node.Mesh.Faces);
        MeshFace face = node.Mesh.Faces[0];

        vm.ActivateToolById(ShortcutCatalog.ToolPushPull);
        Assert.True(session.Tools.IsActive, "Инструмент выдавливания не активировался.");

        Vector3d center = MeshEditOps.FaceCentroid(face);
        Vector2d press = session.Tools.Context.Viewport.TryProject(center)
            ?? throw new InvalidOperationException("Грань не проецируется во вьюпорт.");
        Vector2d drag = press + new Vector2d(0, -80);

        bool pressed = session.Tools.RoutePointer(new PointerInput(
            press, PointerEventKind.Press, PointerButton.Left, ToolModifiers.None));
        bool moved = session.Tools.RoutePointer(new PointerInput(
            drag, PointerEventKind.Move, PointerButton.None, ToolModifiers.None));
        bool released = session.Tools.RoutePointer(new PointerInput(
            drag, PointerEventKind.Release, PointerButton.Left, ToolModifiers.None));

        Assert.True(pressed && moved && released, $"press={pressed} move={moved} release={released}");
        Assert.Equal(6, node.Mesh.Faces.Count);
        Assert.True(node.Mesh.IsWatertight, "Выдавленное тело не замкнуто.");
        Assert.True(session.History.Entries.Count > 0, "Выдавливание не попало в историю.");
    }
}
