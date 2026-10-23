namespace SmetaNA.Licensing;

/// <summary>Локальное состояние активации или триала повреждено либо подделано.</summary>
public sealed class LicenseStateException : Exception
{
    /// <summary>Создаёт исключение с пояснением.</summary>
    public LicenseStateException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с пояснением и внутренней причиной.</summary>
    public LicenseStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
