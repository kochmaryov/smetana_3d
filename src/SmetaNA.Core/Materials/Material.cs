namespace SmetaNA.Core.Materials;

/// <summary>
/// Простой материал (задача 5.4): имя, цвет RGBA и базовые свойства —
/// непрозрачность (<see cref="Opacity"/>) и блеск (<see cref="Shininess"/>) в долях 0..1.
/// Сравнение — по значению (имя + свойства); адрес материала в сцене — его индекс
/// в палитре меша (<see cref="MaterialTable"/>).
/// </summary>
public sealed record Material
{
    /// <summary>Материал по умолчанию: белый, непрозрачный, без блеска.</summary>
    public static readonly Material Default = new("Default", RgbaColor.White);

    public Material(string name, RgbaColor color, double opacity = 1.0, double shininess = 0.2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Непрозрачность должна быть в диапазоне 0..1.");
        }

        if (!double.IsFinite(shininess) || shininess < 0 || shininess > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(shininess), "Блеск должен быть в диапазоне 0..1.");
        }

        Name = name;
        Color = color;
        Opacity = opacity;
        Shininess = shininess;
    }

    /// <summary>Имя материала (в палитре и на панели свойств).</summary>
    public string Name { get; init; }

    /// <summary>Цвет RGBA.</summary>
    public RgbaColor Color { get; init; }

    /// <summary>Непрозрачность 0..1; 1 — полностью непрозрачный.</summary>
    public double Opacity { get; init; }

    /// <summary>Блеск (specular) 0..1: 0 — матовый, 1 — зеркальный.</summary>
    public double Shininess { get; init; }

    /// <summary>Итоговый цвет с учётом непрозрачности (альфа = цвет × <see cref="Opacity"/>).</summary>
    public RgbaColor EffectiveColor =>
        Color.WithAlpha((byte)Math.Round(Color.A * Opacity, MidpointRounding.AwayFromZero));

    /// <summary>Копия с другим цветом.</summary>
    public Material WithColor(RgbaColor color) => this with { Color = color };

    /// <summary>Копия с другой непрозрачностью.</summary>
    public Material WithOpacity(double opacity) => this with { Opacity = opacity };
}
