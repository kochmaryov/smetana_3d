namespace SmetaNA.Manifold;

/// <summary>Булева операция CSG (соответствует ManifoldOpType из C API).</summary>
public enum BooleanOp
{
    Union = 0,
    Difference = 1,
    Intersection = 2,
}
