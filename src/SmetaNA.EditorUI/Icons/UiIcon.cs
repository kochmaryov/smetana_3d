using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Глиф icon-pack интерфейса: рисует <see cref="UiIconId"/> из
/// <see cref="UiIconResources"/> stroke-стилем (без заливки) с перекраской через
/// currentColor — унаследованный Foreground, поэтому глиф следует за темой и
/// состоянием кнопки-носителя. Поле глифа 24×24, как у палитры
/// (<see cref="ToolIcon"/>); на кнопке по умолчанию 20 px.
/// </summary>
public class UiIcon : Control
{
    /// <summary>Поле глифа (mini-language ресурсов icon-pack).</summary>
    public const double GlyphDesignSize = 24;

    /// <summary>Размер глифа по умолчанию (кнопки-иконки 32–38 px).</summary>
    public const double DefaultDisplaySize = 20;

    /// <summary>Толщина обводки в единицах поля (как у палитры: 1.7).</summary>
    public const double StrokeDesignThickness = 1.7;

    /// <summary>Глиф для отрисовки (<see cref="UiIconId.None"/> — пусто).</summary>
    public static readonly StyledProperty<UiIconId> IconIdProperty =
        AvaloniaProperty.Register<UiIcon, UiIconId>(nameof(IconId));

    /// <summary>Глиф для отрисовки (<see cref="UiIconId.None"/> — пусто).</summary>
    public UiIconId IconId
    {
        get => GetValue(IconIdProperty);
        set => SetValue(IconIdProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(DefaultDisplaySize, DefaultDisplaySize);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconIdProperty
            || change.Property == TextElement.ForegroundProperty
            || change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (UiIconResources.GetGeometry(IconId) is not { } geometry)
        {
            return;
        }

        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        double scale = size / GlyphDesignSize;
        double offsetX = (Bounds.Width - size) / 2;
        double offsetY = (Bounds.Height - size) / 2;
        IBrush brush = ResolveBrush();

        var pen = new Pen(brush, StrokeDesignThickness)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        using (context.PushTransform(Matrix.CreateTranslation(offsetX, offsetY)))
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    /// <summary>Кисть глифа: currentColor (унаследованный Foreground).</summary>
    private IBrush ResolveBrush() =>
        TextElement.GetForeground(this)
        ?? FindAncestorForeground()
        ?? FallbackForeground();

    private IBrush? FindAncestorForeground()
    {
        for (Visual? ancestor = this.GetVisualParent(); ancestor is not null; ancestor = ancestor.GetVisualParent())
        {
            if (ancestor is Control control && TextElement.GetForeground(control) is { } brush)
            {
                return brush;
            }
        }

        return null;
    }

    private IBrush FallbackForeground() =>
        GetValue(ThemeVariantScope.ActualThemeVariantProperty) == ThemeVariant.Dark
            ? Brushes.White
            : Brushes.Black;
}
