namespace SmetaNA.EditorUI.Commands;

/// <summary>Ошибки назначения комбинации клавиш.</summary>
public enum ShortcutMapError
{
    None,
    UnknownCommand,
    Conflict,
}

/// <summary>Результат назначения комбинации; при конфликте — id команды-владельца.</summary>
public readonly record struct ShortcutMapResult(bool IsSuccess, ShortcutMapError Error, string? ConflictCommandId)
{
    public static ShortcutMapResult Ok() => new(true, ShortcutMapError.None, null);

    public static ShortcutMapResult Unknown() => new(false, ShortcutMapError.UnknownCommand, null);

    public static ShortcutMapResult Conflict(string conflictCommandId) =>
        new(false, ShortcutMapError.Conflict, conflictCommandId);
}

/// <summary>
/// Настраиваемые маппинги горячих клавиш: значения по умолчанию из
/// <see cref="ShortcutCatalog"/> и пользовательские переопределения
/// с контролем конфликтов (спека §3.2 п. 6).
/// </summary>
public sealed class ShortcutMap
{
    private readonly Dictionary<string, Shortcut> _defaults = [];
    private readonly Dictionary<string, Shortcut> _current = [];
    private readonly Dictionary<string, string> _nameKeys = [];
    private readonly List<string> _order = [];

    public ShortcutMap()
        : this(ShortcutCatalog.Defaults)
    {
    }

    public ShortcutMap(IEnumerable<ShortcutDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (ShortcutDefinition definition in definitions)
        {
            ArgumentException.ThrowIfNullOrEmpty(definition.CommandId);
            ArgumentException.ThrowIfNullOrEmpty(definition.NameKey);
            if (!_current.TryAdd(definition.CommandId, definition.Gesture))
            {
                throw new ArgumentException($"Команда встречается дважды: «{definition.CommandId}».", nameof(definitions));
            }

            _defaults.Add(definition.CommandId, definition.Gesture);
            _nameKeys.Add(definition.CommandId, definition.NameKey);
            _order.Add(definition.CommandId);
        }
    }

    /// <summary>Идентификаторы команд в порядке отображения.</summary>
    public IReadOnlyList<string> CommandIds => _order;

    public bool Contains(string commandId) => _current.ContainsKey(commandId);

    /// <summary>Текущая комбинация команды; <c>null</c> — команда неизвестна.</summary>
    public Shortcut? Find(string commandId) =>
        _current.TryGetValue(commandId, out Shortcut gesture) ? gesture : null;

    /// <summary>Комбинация по умолчанию.</summary>
    public Shortcut? FindDefault(string commandId) =>
        _defaults.TryGetValue(commandId, out Shortcut gesture) ? gesture : null;

    /// <summary>Ключ локализации имени команды.</summary>
    public string? GetNameKey(string commandId) =>
        _nameKeys.TryGetValue(commandId, out string? key) ? key : null;

    /// <summary>
    /// Назначить комбинацию: отказ при неизвестной команде или конфликте
    /// с другой командой.
    /// </summary>
    public ShortcutMapResult TrySet(string commandId, Shortcut gesture)
    {
        if (!_current.ContainsKey(commandId))
        {
            return ShortcutMapResult.Unknown();
        }

        foreach (KeyValuePair<string, Shortcut> pair in _current)
        {
            if (pair.Key != commandId && pair.Value == gesture)
            {
                return ShortcutMapResult.Conflict(pair.Key);
            }
        }

        _current[commandId] = gesture;
        return ShortcutMapResult.Ok();
    }

    /// <summary>Вернуть команде комбинацию по умолчанию.</summary>
    public bool Reset(string commandId)
    {
        if (!_defaults.TryGetValue(commandId, out Shortcut gesture))
        {
            return false;
        }

        _current[commandId] = gesture;
        return true;
    }

    /// <summary>Вернуть все комбинации по умолчанию.</summary>
    public void ResetAll()
    {
        foreach (KeyValuePair<string, Shortcut> pair in _defaults)
        {
            _current[pair.Key] = pair.Value;
        }
    }
}
