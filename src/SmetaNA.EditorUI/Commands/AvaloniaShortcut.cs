using Avalonia.Input;

namespace SmetaNA.EditorUI.Commands;

/// <summary>Мост между клавишами Avalonia и маппингами горячих клавиш.</summary>
public static class AvaloniaShortcut
{
    public static bool TryConvert(Key key, KeyModifiers keyModifiers, out Shortcut shortcut)
    {
        shortcut = default;
        ShortcutKey mapped = MapKey(key);
        if (mapped == ShortcutKey.None)
        {
            return false;
        }

        ShortcutModifiers modifiers = ShortcutModifiers.None;
        if (keyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= ShortcutModifiers.Ctrl;
        }

        if (keyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= ShortcutModifiers.Alt;
        }

        if (keyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= ShortcutModifiers.Shift;
        }

        shortcut = new Shortcut(modifiers, mapped);
        return true;
    }

    private static ShortcutKey MapKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return ShortcutKey.A + (key - Key.A);
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ShortcutKey.D0 + (key - Key.D0);
        }

        if (key is >= Key.F1 and <= Key.F12)
        {
            return ShortcutKey.F1 + (key - Key.F1);
        }

        return key switch
        {
            Key.Escape => ShortcutKey.Escape,
            Key.Enter => ShortcutKey.Enter,
            Key.Tab => ShortcutKey.Tab,
            Key.Space => ShortcutKey.Space,
            Key.Back => ShortcutKey.Backspace,
            Key.Delete => ShortcutKey.Delete,
            Key.Insert => ShortcutKey.Insert,
            Key.Home => ShortcutKey.Home,
            Key.End => ShortcutKey.End,
            Key.PageUp => ShortcutKey.PageUp,
            Key.PageDown => ShortcutKey.PageDown,
            Key.Up => ShortcutKey.Up,
            Key.Down => ShortcutKey.Down,
            Key.Left => ShortcutKey.Left,
            Key.Right => ShortcutKey.Right,
            _ => ShortcutKey.None,
        };
    }
}
