using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Tools;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>Перевод ввода UI в модель инструментов.</summary>
public class PointerInputFactoryTests
{
    [Fact]
    public void Modifiers_MapShiftCtrlAlt()
    {
        Assert.Equal(ToolModifiers.None, PointerInputFactory.Modifiers(false, false, false));
        Assert.Equal(ToolModifiers.Shift, PointerInputFactory.Modifiers(true, false, false));
        Assert.Equal(ToolModifiers.Ctrl, PointerInputFactory.Modifiers(false, true, false));
        Assert.Equal(ToolModifiers.Alt, PointerInputFactory.Modifiers(false, false, true));
        Assert.Equal(
            ToolModifiers.Shift | ToolModifiers.Ctrl | ToolModifiers.Alt,
            PointerInputFactory.Modifiers(true, true, true));
    }

    [Fact]
    public void Create_BuildsPointerInput()
    {
        PointerInput input = PointerInputFactory.Create(
            12.5, 40, PointerEventKind.Press, PointerButton.Left, shift: true);

        Assert.Equal(new Vector2d(12.5, 40), input.PositionPx);
        Assert.Equal(PointerEventKind.Press, input.Kind);
        Assert.Equal(PointerButton.Left, input.Button);
        Assert.Equal(ToolModifiers.Shift, input.Modifiers);
    }

    [Fact]
    public void Create_MoveDefaultsToNoButton()
    {
        PointerInput input = PointerInputFactory.Create(1, 2, PointerEventKind.Move);

        Assert.Equal(PointerButton.None, input.Button);
        Assert.Equal(ToolModifiers.None, input.Modifiers);
    }

    [Fact]
    public void Key_BuildsKeyInput()
    {
        KeyInput input = PointerInputFactory.Key(ToolKey.Escape, ctrl: true);

        Assert.Equal(ToolKey.Escape, input.Key);
        Assert.Equal(ToolModifiers.Ctrl, input.Modifiers);
    }
}
