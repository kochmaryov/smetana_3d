using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Глиф палитры инструментов: рисует <see cref="ToolIconId"/> из
/// <see cref="ToolIconResources"/> stroke-стилем (без заливки) с перекраской через
/// currentColor — унаследованный Foreground, поэтому глиф следует за темой и
/// состоянием кнопки-носителя (T6, DoD §5.5). Как на макете variant_a: поле
/// глифа 24×24, на кнопке — 20 px; на кнопке с классом active (App.axaml) глиф
/// подсвечивается акцентом.
/// </summary>
public class ToolIcon : Control
{
    /// <summary>Поле глифа (mini-language ресурсов, макет variant_a).</summary>
    public const double GlyphDesignSize = 24;

    /// <summary>Размер глифа по умолчанию (кнопки палитры 38/44 px).</summary>
    public const double DefaultDisplaySize = 20;

    /// <summary>Толщина обводки в единицах поля (макет: stroke-width 1.7).</summary>
    public const double StrokeDesignThickness = 1.7;

    private const string ActiveClass = "active";
    private const string AccentResourceKey = "SystemAccentColor";

    /// <summary>Глиф для отрисовки (<see cref="ToolIconId.None"/> — пусто).</summary>
    public static readonly StyledProperty<ToolIconId> IconIdProperty =
        AvaloniaProperty.Register<ToolIcon, ToolIconId>(nameof(IconId));

    private Button? _hostButton;

    /// <summary>Глиф для отрисовки (<see cref="ToolIconId.None"/> — пусто).</summary>
    public ToolIconId IconId
    {
        get => GetValue(IconIdProperty);
        set => SetValue(IconIdProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(DefaultDisplaySize, DefaultDisplaySize);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _hostButton = this.GetVisualAncestors().OfType<Button>().FirstOrDefault();
        if (_hostButton is not null)
        {
            _hostButton.Classes.CollectionChanged += OnHostClassesChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_hostButton is not null)
        {
            _hostButton.Classes.CollectionChanged -= OnHostClassesChanged;
            _hostButton = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

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
        if (ToolIconResources.GetGeometry(IconId) is not { } geometry)
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
        IBrush? brush = ResolveBrush();
        if (brush is null)
        {
            return;
        }

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

    /// <summary>
    /// Кисть глифа: акцент — на активной кнопке (макет variant_a,
    /// .icon-btn.active), иначе currentColor (унаследованный Foreground).
    /// </summary>
    private IBrush? ResolveBrush()
    {
        if (_hostButton?.Classes.Contains(ActiveClass) == true && ResolveAccent() is { } accent)
        {
            return accent;
        }

        return TextElement.GetForeground(this)
            ?? FindAncestorForeground()
            ?? FallbackForeground();
    }

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

    private IBrush? ResolveAccent()
    {
        return this.TryFindResource(AccentResourceKey, out var resource)
            ? resource switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => null,
            }
            : null;
    }

    private IBrush FallbackForeground() =>
        GetValue(ThemeVariantScope.ActualThemeVariantProperty) == ThemeVariant.Dark
            ? Brushes.White
            : Brushes.Black;

    private void OnHostClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        InvalidateVisual();
}
