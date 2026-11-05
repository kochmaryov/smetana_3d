namespace SmetaNA.Core.Inference;

/// <summary>
/// Настройки привязок (спека §6): допуск курсора, шаг сетки, категории, подписи
/// и показ скрытой геометрии. Значения приходят из настроек приложения и
/// действуют на все инструменты через <see cref="Tools.ToolContext"/>.
/// </summary>
public sealed record InferenceSettings
{
    /// <summary>Шаг сетки по умолчанию — 10 единиц сцены.</summary>
    public const double DefaultGridStep = 10.0;

    /// <summary>Допуск сетки по умолчанию — 12 px (меньше — курсор «проскакивает» узлы).</summary>
    public const double DefaultGridTolerancePx = 12.0;

    /// <summary>Настройки по умолчанию: геометрия и оси, сетка и скрытое выключены.</summary>
    public static readonly InferenceSettings Default = new();

    /// <summary>Радиус толеранса привязки в пикселях экрана.</summary>
    public double TolerancePx { get; init; } = SnapRequest.DefaultTolerancePx;

    /// <summary>Показывать подписи типов привязок у курсора.</summary>
    public bool ShowLabels { get; init; } = true;

    /// <summary>Постоянный режим «Показать скрытую геометрию» (R-10).</summary>
    public bool ShowHiddenGeometry { get; init; }

    /// <summary>Сетка привязки включена (наше расширение, по умолчанию выключена).</summary>
    public bool GridEnabled { get; init; }

    /// <summary>Шаг сетки в единицах сцены.</summary>
    public double GridStep { get; init; } = DefaultGridStep;

    /// <summary>
    /// Привязка длины к точности (аналог `Enable Length Snapping`, R-14.1):
    /// длина от опорной точки округляется до <see cref="LengthPrecision"/>.
    /// </summary>
    public bool LengthSnappingEnabled { get; init; }

    /// <summary>Точность длины в единицах сцены (приближение «точности отображения»).</summary>
    public double LengthPrecision { get; init; } = 0.1;

    /// <summary>Включённые категории привязок.</summary>
    public SnapFilter Categories { get; init; } = SnapFilter.All;

    /// <summary>Привести значения к допустимым: толеранс 2–40 px, шаг &gt; 0.</summary>
    public InferenceSettings Normalize()
    {
        double tolerance = double.IsFinite(TolerancePx) && TolerancePx > 0
            ? Math.Clamp(TolerancePx, 2.0, 40.0)
            : SnapRequest.DefaultTolerancePx;
        double step = double.IsFinite(GridStep) && GridStep > 0 ? GridStep : DefaultGridStep;
        double precision = double.IsFinite(LengthPrecision) && LengthPrecision > 0 ? LengthPrecision : 0.1;

        return this with { TolerancePx = tolerance, GridStep = step, LengthPrecision = precision };
    }
}
