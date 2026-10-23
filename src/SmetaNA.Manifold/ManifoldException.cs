namespace SmetaNA.Manifold;

/// <summary>
/// Ошибка CSG-бэкенда Manifold: невалидный входной меш или результат
/// (статус из C API не <see cref="ManifoldError.NoError"/>).
/// </summary>
public sealed class ManifoldException : Exception
{
    public ManifoldException(ManifoldError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>Статус C API, вызвавший исключение.</summary>
    public ManifoldError Error { get; }
}
