using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools;

namespace SmetaNA.EditorUI.Tools;

/// <summary>
/// Перевод ввода указателя/клавиш UI в модель инструментов
/// (<see cref="PointerInput"/>, <see cref="KeyInput"/>) — без типов Avalonia,
/// чтобы маршрутизация тестировалась юнит-тестами.
/// </summary>
public static class PointerInputFactory
{
    public static ToolModifiers Modifiers(bool shift, bool ctrl, bool alt)
    {
        ToolModifiers modifiers = ToolModifiers.None;
        if (shift)
        {
            modifiers |= ToolModifiers.Shift;
        }

        if (ctrl)
        {
            modifiers |= ToolModifiers.Ctrl;
        }

        if (alt)
        {
            modifiers |= ToolModifiers.Alt;
        }

        return modifiers;
    }

    public static PointerInput Create(
        double x,
        double y,
        PointerEventKind kind,
        PointerButton button = PointerButton.None,
        bool shift = false,
        bool ctrl = false,
        bool alt = false)
        => new(new Vector2d(x, y), kind, button, Modifiers(shift, ctrl, alt));

    public static KeyInput Key(ToolKey key, bool shift = false, bool ctrl = false, bool alt = false)
        => new(key, Modifiers(shift, ctrl, alt));
}
