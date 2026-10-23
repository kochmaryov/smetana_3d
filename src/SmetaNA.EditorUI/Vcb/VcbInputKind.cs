namespace SmetaNA.EditorUI.Vcb;

/// <summary>Типы числового ввода в строке значений (VCB).</summary>
public enum VcbInputKind
{
    /// <summary>Длина: неотрицательное число с единицей.</summary>
    Length,

    /// <summary>Радиус: строго положительное число.</summary>
    Radius,

    /// <summary>Угол в градусах.</summary>
    Angle,

    /// <summary>Точка x; y; z (единицы длины).</summary>
    Coordinate,
}

/// <summary>Ошибки валидации VCB; сообщения локализуются через ключи ресурсов.</summary>
public enum VcbError
{
    None,
    Empty,
    InvalidNumber,
    NotFinite,
    Negative,
    NotPositive,
    UnknownUnit,
    ComponentCount,
}
