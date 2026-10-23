namespace SmetaNA.Core.Annotations;

/// <summary>
/// Оформление размеров в пикселях экрана: размеры подписей и стрелок постоянны
/// на экране, в мировые единицы их переводит рендер через
/// <c>InferenceViewport.WorldRadiusAt</c>.
/// </summary>
public sealed record DimensionStyle(
    double TextHeightPx = 14.0,
    double ArrowSizePx = 7.0,
    double ExtensionOvershootPx = 3.0,
    double TextGapPx = 6.0)
{
    public static DimensionStyle Default { get; } = new();
}
