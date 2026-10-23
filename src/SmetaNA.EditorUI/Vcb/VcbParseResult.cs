namespace SmetaNA.EditorUI.Vcb;

/// <summary>Результат разбора скалярного значения VCB (длина/радиус/угол).</summary>
public sealed record VcbParseResult
{
    public VcbError Error { get; init; }

    /// <summary>Значение в единицах ввода; для угла — градусы.</summary>
    public double Value { get; init; }

    /// <summary>Значение в миллиметрах; для угла не определяется.</summary>
    public double? ValueInMillimeters { get; init; }

    public bool IsSuccess => Error == VcbError.None;

    public static VcbParseResult Fail(VcbError error) => new() { Error = error };

    public static VcbParseResult ForLength(double value, double valueInMillimeters) => new()
    {
        Error = VcbError.None,
        Value = value,
        ValueInMillimeters = valueInMillimeters,
    };

    public static VcbParseResult ForAngle(double degrees) => new()
    {
        Error = VcbError.None,
        Value = degrees,
        ValueInMillimeters = null,
    };
}

/// <summary>Результат разбора координат VCB; компоненты — в миллиметрах.</summary>
public sealed record VcbCoordinateResult
{
    public VcbError Error { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }

    public bool IsSuccess => Error == VcbError.None;

    public static VcbCoordinateResult Fail(VcbError error) => new() { Error = error };

    public static VcbCoordinateResult Ok(double x, double y, double z) => new()
    {
        Error = VcbError.None,
        X = x,
        Y = y,
        Z = z,
    };
}
