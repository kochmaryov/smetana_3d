using Avalonia.Controls;
using Avalonia.Interactivity;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.Views;

/// <summary>
/// Код-behind правого дока: выбор секции по клику на иконку/вкладку и
/// адаптив по ширине окна (док сворачивается в рейл ≤ 960 px).
/// </summary>
public partial class RightRailControl : UserControl
{
    public RightRailControl()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>Адаптив по ширине окна: ≤ 960 px док сворачивается в рейл (T11).</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            vm.RightRail.ApplyWindowWidth(topLevel.Bounds.Width);
        }
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
