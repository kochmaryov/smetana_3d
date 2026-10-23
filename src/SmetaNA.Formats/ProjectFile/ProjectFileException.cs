namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Ошибка формата проекта: файл повреждён, имеет несовместимую версию схемы
/// или содержит невалидные данные сцены.
/// </summary>
public sealed class ProjectFileException : Exception
{
    public ProjectFileException(string message)
        : base(message)
    {
    }

    public ProjectFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
