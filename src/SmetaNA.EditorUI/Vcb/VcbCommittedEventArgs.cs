namespace SmetaNA.EditorUI.Vcb;

/// <summary>Аргументы события принятия значения из VCB.</summary>
public sealed class VcbCommittedEventArgs : EventArgs
{
    private VcbCommittedEventArgs(VcbInputKind kind, VcbParseResult? scalar, VcbCoordinateResult? coordinate)
    {
        Kind = kind;
        Scalar = scalar;
        Coordinate = coordinate;
    }

    public VcbInputKind Kind { get; }

    /// <summary>Разобранное скалярное значение (длина/радиус/угол); <c>null</c> для координат.</summary>
    public VcbParseResult? Scalar { get; }

    /// <summary>Разобранные координаты; <c>null</c> для скалярных значений.</summary>
    public VcbCoordinateResult? Coordinate { get; }

    public static VcbCommittedEventArgs ForScalar(VcbInputKind kind, VcbParseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new VcbCommittedEventArgs(kind, result, null);
    }

    public static VcbCommittedEventArgs ForCoordinate(VcbCoordinateResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new VcbCommittedEventArgs(VcbInputKind.Coordinate, null, result);
    }
}
