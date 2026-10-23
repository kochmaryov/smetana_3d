using Avalonia.Controls;

namespace SmetaNA.EditorUI.PrintVolume;

/// <summary>
/// Панель зоны печати (задача 4.5): выбор профиля принтера, свойства профиля
/// (габариты, сопло, высота слоя) и включение зоны в вьюпорте. Своя view-модель
/// создаётся здесь — DataContext окна не затрагивается.
/// </summary>
public partial class PrintVolumePanel : UserControl
{
    public PrintVolumePanel()
    {
        InitializeComponent();
        DataContext = new PrintVolumeViewModel();
    }
}
