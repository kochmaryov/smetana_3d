namespace SmetaNA.EditorUI.Commands;

/// <summary>Модификаторы комбинации клавиш.</summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
}

/// <summary>Клавиши, поддерживаемые маппингами горячих клавиш.</summary>
public enum ShortcutKey
{
    None = 0,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Escape, Enter, Tab, Space, Backspace, Delete, Insert, Home, End, PageUp, PageDown,
    Up, Down, Left, Right,
}

/// <summary>
/// Комбинация клавиш в текстовом виде: разбор и формат в каноническом виде
/// («Ctrl+Shift+Z») — для маппингов горячих клавиш (спека §3.2 п. 6).
/// </summary>
public readonly record struct Shortcut(ShortcutModifiers Modifiers, ShortcutKey Key)
{
    private static readonly Dictionary<string, ShortcutKey> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = ShortcutKey.Escape,
        ["escape"] = ShortcutKey.Escape,
        ["enter"] = ShortcutKey.Enter,
        ["return"] = ShortcutKey.Enter,
        ["tab"] = ShortcutKey.Tab,
        ["space"] = ShortcutKey.Space,
        ["backspace"] = ShortcutKey.Backspace,
        ["bs"] = ShortcutKey.Backspace,
        ["delete"] = ShortcutKey.Delete,
        ["del"] = ShortcutKey.Delete,
        ["insert"] = ShortcutKey.Insert,
        ["ins"] = ShortcutKey.Insert,
        ["home"] = ShortcutKey.Home,
        ["end"] = ShortcutKey.End,
        ["pgup"] = ShortcutKey.PageUp,
        ["pageup"] = ShortcutKey.PageUp,
        ["pgdn"] = ShortcutKey.PageDown,
        ["pagedown"] = ShortcutKey.PageDown,
        ["up"] = ShortcutKey.Up,
        ["down"] = ShortcutKey.Down,
        ["left"] = ShortcutKey.Left,
        ["right"] = ShortcutKey.Right,
    };

    private static readonly Dictionary<ShortcutKey, string> KeyNames = new()
    {
        [ShortcutKey.Escape] = "Esc",
        [ShortcutKey.Enter] = "Enter",
        [ShortcutKey.Tab] = "Tab",
        [ShortcutKey.Space] = "Space",
        [ShortcutKey.Backspace] = "Backspace",
        [ShortcutKey.Delete] = "Delete",
        [ShortcutKey.Insert] = "Insert",
        [ShortcutKey.Home] = "Home",
        [ShortcutKey.End] = "End",
        [ShortcutKey.PageUp] = "PgUp",
        [ShortcutKey.PageDown] = "PgDn",
        [ShortcutKey.Up] = "Up",
        [ShortcutKey.Down] = "Down",
        [ShortcutKey.Left] = "Left",
        [ShortcutKey.Right] = "Right",
    };

    /// <summary>Разбор комбинации; исключение при неверном формате.</summary>
    public static Shortcut Parse(string text)
    {
        if (!TryParse(text, out Shortcut shortcut))
        {
            throw new FormatException($"Нераспознанная комбинация клавиш: «{text}».");
        }

        return shortcut;
    }

    /// <summary>Разбор комбинации вида «Ctrl+Shift+Z».</summary>
    public static bool TryParse(string? text, out Shortcut shortcut)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries);
        ShortcutModifiers modifiers = ShortcutModifiers.None;
        ShortcutKey key = ShortcutKey.None;

        for (int i = 0; i < parts.Length; i++)
        {
            bool isLast = i == parts.Length - 1;
            if (isLast)
            {
                if (!TryParseKey(parts[i], out key))
                {
                    return false;
                }
            }
            else if (!TryParseModifier(parts[i], out ShortcutModifiers modifier))
            {
                return false;
            }
            else
            {
                modifiers |= modifier;
            }
        }

        if (key == ShortcutKey.None)
        {
            return false;
        }

        shortcut = new Shortcut(modifiers, key);
        return true;
    }

    /// <summary>Канонический текст комбинации: модификаторы в порядке Ctrl, Alt, Shift.</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(ShortcutModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(FormatKey(Key));
        return string.Join("+", parts);
    }

    private static bool TryParseModifier(string text, out ShortcutModifiers modifier)
    {
        switch (text.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                modifier = ShortcutModifiers.Ctrl;
                return true;
            case "alt":
                modifier = ShortcutModifiers.Alt;
                return true;
            case "shift":
                modifier = ShortcutModifiers.Shift;
                return true;
            default:
                modifier = ShortcutModifiers.None;
                return false;
        }
    }

    private static bool TryParseKey(string text, out ShortcutKey key)
    {
        key = ShortcutKey.None;
        if (text.Length == 1)
        {
            char c = text[0];
            if (c is >= 'a' and <= 'z')
            {
                key = ShortcutKey.A + (c - 'a');
                return true;
            }

            if (c is >= 'A' and <= 'Z')
            {
                key = ShortcutKey.A + (c - 'A');
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                key = ShortcutKey.D0 + (c - '0');
                return true;
            }

            return false;
        }

        if (KeyAliases.TryGetValue(text, out ShortcutKey alias))
        {
            key = alias;
            return true;
        }

        if (text.Length >= 2 && (text[0] == 'f' || text[0] == 'F')
            && int.TryParse(text[1..], out int functionIndex)
            && functionIndex is >= 1 and <= 12)
        {
            key = ShortcutKey.F1 + (functionIndex - 1);
            return true;
        }

        return false;
    }

    private static string FormatKey(ShortcutKey key)
    {
        if (key == ShortcutKey.None)
        {
            return string.Empty;
        }

        if (key is >= ShortcutKey.A and <= ShortcutKey.Z)
        {
            return ((char)('A' + (key - ShortcutKey.A))).ToString();
        }

        if (key is >= ShortcutKey.D0 and <= ShortcutKey.D9)
        {
            return ((char)('0' + (key - ShortcutKey.D0))).ToString();
        }

        if (key is >= ShortcutKey.F1 and <= ShortcutKey.F12)
        {
            return "F" + (key - ShortcutKey.F1 + 1).ToString();
        }

        return KeyNames.TryGetValue(key, out string? name) ? name : key.ToString();
    }
}
