using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmetaNA.EditorUI.Commands;

namespace SmetaNA.EditorUI.CommandPalette;

/// <summary>
/// Окно палитры команд (Ctrl+K): поиск по имени команды, Enter/двойной клик
/// запускает выбранную, Escape закрывает.
/// </summary>
public partial class CommandPaletteWindow : Window
{
    private readonly CommandPaletteViewModel _viewModel;

    /// <summary>Конструктор дизайнера XAML (AVLN3001); в рантайме используется фабричный.</summary>
    public CommandPaletteWindow()
        : this(new CommandPaletteViewModel(
            new Localization.LocalizationService(),
            new ShortcutMap(ShortcutCatalog.Defaults),
            _ => { }))
    {
    }

    public CommandPaletteWindow(CommandPaletteViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SearchBox.KeyDown += OnSearchKeyDown;
        Opened += (_, _) => SearchBox.Focus();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_viewModel.RunSelected())
            {
                Close();
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.RunSelected())
        {
            Close();
        }
    }
}
