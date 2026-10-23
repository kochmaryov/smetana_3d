using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools;

/// <summary>Клавиши-модификаторы ввода инструмента (без привязки к UI-фреймворку).</summary>
[Flags]
public enum ToolModifiers
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
}

/// <summary>Кнопка указателя.</summary>
public enum PointerButton
{
    None,
    Left,
    Middle,
    Right,
}

/// <summary>Вид события указателя.</summary>
public enum PointerEventKind
{
    Move,
    Press,
    Release,
    Leave,
}

/// <summary>
/// Событие указателя в пикселях вьюпорта (начало — левый верхний угол,
/// X вправо, Y вниз). Модель ввода инструментов не зависит от Avalonia.
/// </summary>
/// <param name="PositionPx">Положение в пикселях.</param>
/// <param name="Kind">Вид события.</param>
/// <param name="Button">Кнопка (для press/release; <see cref="PointerButton.None"/> для move).</param>
/// <param name="Modifiers">Нажатые клавиши-модификаторы.</param>
public readonly record struct PointerInput(
    Vector2d PositionPx,
    PointerEventKind Kind,
    PointerButton Button,
    ToolModifiers Modifiers)
{
    public static PointerInput Move(Vector2d positionPx, ToolModifiers modifiers = ToolModifiers.None)
        => new(positionPx, PointerEventKind.Move, PointerButton.None, modifiers);

    public static PointerInput Press(Vector2d positionPx, PointerButton button, ToolModifiers modifiers = ToolModifiers.None)
        => new(positionPx, PointerEventKind.Press, button, modifiers);

    public static PointerInput Release(Vector2d positionPx, PointerButton button, ToolModifiers modifiers = ToolModifiers.None)
        => new(positionPx, PointerEventKind.Release, button, modifiers);
}

/// <summary>Клавиши, которые понимают инструменты.</summary>
public enum ToolKey
{
    Escape,
    Enter,
    Delete,
}

/// <summary>Событие клавиши с модификаторами.</summary>
public readonly record struct KeyInput(ToolKey Key, ToolModifiers Modifiers = ToolModifiers.None);
