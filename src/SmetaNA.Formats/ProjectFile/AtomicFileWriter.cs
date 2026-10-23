namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Атомарная запись файла через временный файл и замену (модуль M9: автосохранение
/// и сохранение проекта). Читатель всегда видит либо старое, либо новое содержимое:
/// сбой записи (исключение, «зависание» процесса, сбой питания) не оставляет
/// полусохранённый целевой файл.
/// </summary>
public static class AtomicFileWriter
{
    /// <summary>
    /// Записать <paramref name="path"/> атомарно: содержимое пишется во временный файл
    /// рядом с целью (та же тома — замена через rename), сбрасывается на диск и заменяет
    /// целевой файл. При ошибке временный файл удаляется, целевой остаётся нетронутым.
    /// </summary>
    public static void Write(string path, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);

        string targetPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = Path.Combine(
            directory ?? ".",
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Файловая блокировка: остаток .tmp не влияет на целевой файл и удаляется при следующей записи.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
