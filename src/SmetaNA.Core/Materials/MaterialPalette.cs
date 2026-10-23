namespace SmetaNA.Core.Materials;

/// <summary>
/// Палитра материалов по умолчанию (задача 5.4): базовые цвета и простые
/// материалы. Порядок <see cref="Standard"/> стабилен — индексы закреплены
/// контрактом (индекс 0 — <see cref="Default"/>) и одинаковы у всех новых мешей.
/// </summary>
public static class MaterialPalette
{
    /// <summary>Материал по умолчанию (белый) — база для граней без материала.</summary>
    public static Material Default { get; } = new("Default", RgbaColor.White);

    public static Material Red { get; } = new("Red", RgbaColor.FromHex("#C0392B"));
    public static Material Orange { get; } = new("Orange", RgbaColor.FromHex("#D35400"));
    public static Material Yellow { get; } = new("Yellow", RgbaColor.FromHex("#F1C40F"));
    public static Material Green { get; } = new("Green", RgbaColor.FromHex("#27AE60"));
    public static Material Cyan { get; } = new("Cyan", RgbaColor.FromHex("#16A085"));
    public static Material Blue { get; } = new("Blue", RgbaColor.FromHex("#2980B9"));
    public static Material Purple { get; } = new("Purple", RgbaColor.FromHex("#8E44AD"));
    public static Material Brown { get; } = new("Brown", RgbaColor.FromHex("#795548"));
    public static Material Tan { get; } = new("Tan", RgbaColor.FromHex("#D7B98E"));
    public static Material LightGray { get; } = new("LightGray", RgbaColor.FromHex("#BDC3C7"));
    public static Material Gray { get; } = new("Gray", RgbaColor.FromHex("#7F8C8D"));
    public static Material DarkGray { get; } = new("DarkGray", RgbaColor.FromHex("#4D5656"));
    public static Material Black { get; } = new("Black", RgbaColor.Black);

    /// <summary>Дерево: тёплый полуматовый цвет с лёгким блеском.</summary>
    public static Material Wood { get; } = new("Wood", RgbaColor.FromHex("#B07A3C"), shininess: 0.35);

    /// <summary>Кирпич: красно-коричневый, матовый.</summary>
    public static Material Brick { get; } = new("Brick", RgbaColor.FromHex("#A0522D"), shininess: 0.05);

    /// <summary>Стекло: полупрозрачный голубоватый, глянцевый.</summary>
    public static Material Glass { get; } = new("Glass", RgbaColor.FromHex("#AED6F1"), opacity: 0.35, shininess: 0.9);

    /// <summary>Стандартный набор в порядке стабильных индексов.</summary>
    public static IReadOnlyList<Material> Standard { get; } =
    [
        Default,
        Red,
        Orange,
        Yellow,
        Green,
        Cyan,
        Blue,
        Purple,
        Brown,
        Tan,
        LightGray,
        Gray,
        DarkGray,
        Black,
        Wood,
        Brick,
        Glass,
    ];

    /// <summary>Новая палитра со стандартным набором (порядок индексов стабилен).</summary>
    public static MaterialTable CreateDefault()
    {
        var table = new MaterialTable();
        foreach (Material material in Standard)
        {
            table.Add(material);
        }

        return table;
    }
}
