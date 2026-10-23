using SmetaNA.EditorUI.Commands;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

public sealed class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Z", ShortcutModifiers.Ctrl, ShortcutKey.Z)]
    [InlineData("ctrl+shift+f5", ShortcutModifiers.Ctrl | ShortcutModifiers.Shift, ShortcutKey.F5)]
    [InlineData("Alt+Space", ShortcutModifiers.Alt, ShortcutKey.Space)]
    [InlineData("A", ShortcutModifiers.None, ShortcutKey.A)]
    [InlineData("Ctrl+1", ShortcutModifiers.Ctrl, ShortcutKey.D1)]
    [InlineData("Esc", ShortcutModifiers.None, ShortcutKey.Escape)]
    [InlineData("Ctrl+PgDn", ShortcutModifiers.Ctrl, ShortcutKey.PageDown)]
    [InlineData("Ctrl+Alt+Shift+Delete", ShortcutModifiers.Ctrl | ShortcutModifiers.Alt | ShortcutModifiers.Shift, ShortcutKey.Delete)]
    public void TryParse_ValidText_ReturnsShortcut(string text, ShortcutModifiers modifiers, ShortcutKey key)
    {
        bool parsed = Shortcut.TryParse(text, out Shortcut shortcut);

        Assert.True(parsed);
        Assert.Equal(modifiers, shortcut.Modifiers);
        Assert.Equal(key, shortcut.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("+A")]
    [InlineData("Ctrl+Foo")]
    [InlineData("A+B")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Ctrl")]
    public void TryParse_InvalidText_ReturnsFalse(string text)
    {
        Assert.False(Shortcut.TryParse(text, out _));
    }

    [Theory]
    [InlineData("ctrl+shift+z", "Ctrl+Shift+Z")]
    [InlineData("esc", "Esc")]
    [InlineData("ctrl+pgdn", "Ctrl+PgDn")]
    [InlineData("f12", "F12")]
    [InlineData("shift+ctrl+a", "Ctrl+Shift+A")]
    public void ToString_ReturnsCanonicalForm(string text, string expected)
    {
        Shortcut shortcut = Shortcut.Parse(text);

        Assert.Equal(expected, shortcut.ToString());
    }

    [Fact]
    public void Parse_TrippedText_RoundTripsThroughToString()
    {
        foreach (string text in new[] { "Ctrl+N", "Space", "Alt+F1", "Ctrl+Shift+T", "Delete" })
        {
            Shortcut shortcut = Shortcut.Parse(text);
            Assert.Equal(shortcut, Shortcut.Parse(shortcut.ToString()));
        }
    }

    [Fact]
    public void Parse_InvalidText_Throws()
    {
        Assert.Throws<FormatException>(() => Shortcut.Parse("Ctrl+Nope"));
    }
}

public sealed class ShortcutMapTests
{
    [Fact]
    public void Defaults_HaveNoConflictingGestures()
    {
        var map = new ShortcutMap();

        List<Shortcut> gestures = map.CommandIds
            .Select(id => map.Find(id)!.Value)
            .ToList();

        Assert.Equal(gestures.Count, gestures.Distinct().Count());
    }

    [Fact]
    public void EveryCommand_HasNameKey()
    {
        var map = new ShortcutMap();

        Assert.NotEmpty(map.CommandIds);
        foreach (string commandId in map.CommandIds)
        {
            Assert.False(string.IsNullOrEmpty(map.GetNameKey(commandId)));
        }
    }

    [Fact]
    public void TrySet_RemapsGesture()
    {
        var map = new ShortcutMap();
        Shortcut custom = Shortcut.Parse("Ctrl+Alt+Z");

        ShortcutMapResult result = map.TrySet(ShortcutCatalog.EditUndo, custom);

        Assert.True(result.IsSuccess);
        Assert.Equal(custom, map.Find(ShortcutCatalog.EditUndo));
    }

    [Fact]
    public void TrySet_ConflictingGesture_IsRejected()
    {
        var map = new ShortcutMap();

        ShortcutMapResult result = map.TrySet(ShortcutCatalog.ToolLine, Shortcut.Parse("Ctrl+Z"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ShortcutMapError.Conflict, result.Error);
        Assert.Equal(ShortcutCatalog.EditUndo, result.ConflictCommandId);
        Assert.Equal(Shortcut.Parse("L"), map.Find(ShortcutCatalog.ToolLine));
    }

    [Fact]
    public void TrySet_UnknownCommand_ReturnsUnknown()
    {
        var map = new ShortcutMap();

        ShortcutMapResult result = map.TrySet("tool.nothing", Shortcut.Parse("Ctrl+K"));

        Assert.Equal(ShortcutMapError.UnknownCommand, result.Error);
    }

    [Fact]
    public void Reset_RestoresDefault()
    {
        var map = new ShortcutMap();
        map.TrySet(ShortcutCatalog.ToolLine, Shortcut.Parse("Ctrl+Alt+L"));

        Assert.True(map.Reset(ShortcutCatalog.ToolLine));
        Assert.Equal(map.FindDefault(ShortcutCatalog.ToolLine), map.Find(ShortcutCatalog.ToolLine));
        Assert.False(map.Reset("tool.nothing"));
    }

    [Fact]
    public void ResetAll_RestoresEveryDefault()
    {
        var map = new ShortcutMap();
        map.TrySet(ShortcutCatalog.ToolLine, Shortcut.Parse("Ctrl+Alt+L"));
        map.TrySet(ShortcutCatalog.EditUndo, Shortcut.Parse("Ctrl+Alt+Z"));

        map.ResetAll();

        foreach (string commandId in map.CommandIds)
        {
            Assert.Equal(map.FindDefault(commandId), map.Find(commandId));
        }
    }

    [Fact]
    public void Constructor_RejectsDuplicateCommands()
    {
        var definitions = new List<ShortcutDefinition>
        {
            new("a", "A", Shortcut.Parse("A")),
            new("a", "A", Shortcut.Parse("B")),
        };

        Assert.Throws<ArgumentException>(() => new ShortcutMap(definitions));
    }
}
