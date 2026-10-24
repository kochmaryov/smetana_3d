using Avalonia.Controls;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Модальный запрос имени своей группы («Новая группа…», «Переименовать…»):
/// пустое имя заменяется на «Мои инструменты» в <see cref="ToolLayoutDialogViewModel"/>.
/// </summary>
public partial class ToolLayoutGroupNameWindow : Window
{
    public ToolLayoutGroupNameWindow()
        : this(ToolLayoutGroupNameViewModel.ForNewGroup(EditorServices.Localization, string.Empty))
    {
    }

    public ToolLayoutGroupNameWindow(ToolLayoutGroupNameViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += ok => Close(ok);
        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    internal ToolLayoutGroupNameViewModel ViewModel { get; }
}
