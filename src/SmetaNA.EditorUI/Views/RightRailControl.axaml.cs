using Avalonia.Controls;
using Avalonia.Interactivity;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.Views;

/// <summary>Код-behind правого рейла: открытие вылета секции по клику на иконку.</summary>
public partial class RightRailControl : UserControl
{
    public RightRailControl()
    {
        InitializeComponent();
    }

    private void OnSectionButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Control { Tag: RightRailSectionViewModel section })
        {
            return;
        }

        vm.RightRail.ToggleSection(section);
    }
}
