using SmetaNA.Core.Geometry;
using SmetaNA.Formats.Obj;
using SmetaNA.Formats.Stl;
using SmetaNA.Formats.ThreeMf;

namespace SmetaNA.Formats;

/// <summary>
/// Фасад обмена мешами (M8): авто-детект формата при чтении (binary/ASCII STL, OBJ, 3MF),
/// диспетчеризация записи с явным контролем единиц.
/// </summary>
public static class MeshFile
{
    /// <summary>Определение формата по байтам; неизвестный формат — <see cref="InvalidDataException"/>.</summary>
    public static MeshFileFormat Detect(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return MeshFormatDetector.Detect(data);
    }

    /// <summary>Определение формата по потоку (позиция потока сохраняется, если поток поддерживает Seek).</summary>
    public static MeshFileFormat Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long position = stream.CanSeek ? stream.Position : 0;
        byte[] data = ReadAllBytes(stream);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        return MeshFormatDetector.Detect(data);
    }

    /// <summary>Импорт меша с авто-детектом формата; координаты нормализуются к миллиметрам.</summary>
    public static MeshImportResult Import(byte[] data, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return MeshFormatDetector.Detect(data) switch
        {
            MeshFileFormat.BinaryStl => StlImporter.Import(data, options),
            MeshFileFormat.AsciiStl => StlImporter.Import(data, options),
            MeshFileFormat.ThreeMf => ThreeMfImporter.Import(data, options),
            _ => ObjImporter.Import(data, options),
        };
    }

    /// <summary>Импорт меша из потока с авто-детектом формата.</summary>
    public static MeshImportResult Import(Stream stream, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Import(ReadAllBytes(stream), options);
    }

    /// <summary>Экспорт меша в заданный формат; координаты (мм) масштабируются в единицы файла.</summary>
    public static byte[] Export(MeshData mesh, MeshFileFormat format, MeshExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        Export(mesh, stream, format, options);
        return stream.ToArray();
    }

    /// <summary>Экспорт меша в заданный формат в поток.</summary>
    public static void Export(MeshData mesh, Stream stream, MeshFileFormat format, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        switch (format)
        {
            case MeshFileFormat.BinaryStl:
                StlExporter.ExportBinary(mesh, stream, options);
                break;
            case MeshFileFormat.AsciiStl:
                StlExporter.ExportAscii(mesh, stream, options);
                break;
            case MeshFileFormat.Obj:
                ObjExporter.Export(mesh, stream, options);
                break;
            case MeshFileFormat.ThreeMf:
                ThreeMfExporter.Export(mesh, stream, options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Неизвестный формат экспорта.");
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
