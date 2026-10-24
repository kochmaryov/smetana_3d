using Avalonia.Controls;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>
/// Палитра инструментов (макет variant_a, спека tool-palette §4). Контрол сам
/// держит ширину палитры и переводит view-модель в режимы 2 колонки → 1 колонка
/// (окно ≤ 960 px) → рейл с вылетами (окно ≤ 768 px); при переполнении по высоте
/// группы сворачиваются (риск R2). DataContext — <see cref="ToolPaletteViewModel"/>.
/// </summary>
public partial class ToolPaletteControl : UserControl
{
    public ToolPaletteControl()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>
    /// Точка подключения адаптива: ширина окна задаёт режим раскладки, высота
    /// области палитры — свёртку групп. View-модель кэширует значения, поэтому
    /// частые вызовы <see cref="LayoutUpdated"/> безопасны.
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (DataContext is not ToolPaletteViewModel viewModel)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.Bounds.Width > 0)
        {
            viewModel.ApplyWindowWidth(topLevel.Bounds.Width);
        }

        if (PaletteScroll.Bounds.Height > 0)
        {
            viewModel.ApplyViewportHeight(PaletteScroll.Bounds.Height);
        }
    }
}
