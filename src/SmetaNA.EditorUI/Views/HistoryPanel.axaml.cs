using Avalonia.Controls;

namespace SmetaNA.EditorUI.Views;

/// <summary>Панель истории undo/redo, привязанная к UndoStack из SmetaNA.Core.</summary>
public partial class HistoryPanel : UserControl
{
    public HistoryPanel()
    {
        InitializeComponent();
    }
}
