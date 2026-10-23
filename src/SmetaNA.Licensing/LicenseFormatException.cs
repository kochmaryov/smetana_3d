namespace SmetaNA.Licensing;

/// <summary>Некорректный формат ключа, токена или подписанного файла.</summary>
public sealed class LicenseFormatException : Exception
{
    /// <summary>Создаёт исключение с пояснением.</summary>
    public LicenseFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с пояснением и внутренней причиной.</summary>
    public LicenseFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
