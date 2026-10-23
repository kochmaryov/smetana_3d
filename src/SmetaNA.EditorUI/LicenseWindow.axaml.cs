using Avalonia.Controls;
using Avalonia.Interactivity;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI;

/// <summary>
/// Экран лицензирования: статус и триал, проверка ключа, офлайн-активация
/// токеном и деактивация для переноса (задача 6.4).
/// </summary>
public partial class LicenseWindow : Window
{
    public LicenseWindow()
        : this(EditorServices.CreateLicenseViewModel())
    {
    }

    public LicenseWindow(LicenseWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    internal LicenseWindowViewModel ViewModel { get; }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
