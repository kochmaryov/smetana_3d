using Avalonia.Controls;
using Avalonia.Interactivity;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Views;

/// <summary>
/// Уведомление о необработанной ошибке: текст сообщения и подробности исключения.
/// Окно одно на приложение — повторные ошибки обновляют его текст.
/// </summary>
public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
        DataContext = new LocalizedStrings(EditorServices.Localization);
    }

    /// <summary>Показать сообщение об ошибке и подробности (стек вызовов).</summary>
    public void ShowError(string message, string details)
    {
        MessageText.Text = message;
        DetailsText.Text = details;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
