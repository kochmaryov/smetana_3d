using Avalonia.Controls;

namespace SmetaNA.EditorUI.Views;

/// <summary>
/// Панель горячих клавиш: список маппингов и inline-редактор
/// (без модальных диалогов, спека §3.2 п. 6).
/// </summary>
public partial class ShortcutPanel : UserControl
{
    public ShortcutPanel()
    {
        InitializeComponent();
    }
}
