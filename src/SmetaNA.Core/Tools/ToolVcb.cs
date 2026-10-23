namespace SmetaNA.Core.Tools;

/// <summary>Тип числового значения инструмента для VCB (value control box).</summary>
public enum ToolValueKind
{
    None,
    Length,
    Angle,
    Coordinate,

    /// <summary>Целое количество (например, стороны многоугольника).</summary>
    Count,
}

/// <summary>Состояние числового ввода (VCB) активного инструмента.</summary>
/// <param name="IsActive">Принимает ли инструмент числовой ввод сейчас.</param>
/// <param name="Kind">Тип ожидаемого значения.</param>
/// <param name="Value">Текущее значение (мм для длин) или <c>null</c>.</param>
public sealed record ToolVcbState(bool IsActive, ToolValueKind Kind, double? Value)
{
    public static readonly ToolVcbState Inactive = new(false, ToolValueKind.None, null);
}
