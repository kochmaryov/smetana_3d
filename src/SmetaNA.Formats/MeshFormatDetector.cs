using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SmetaNA.Formats;

/// <summary>
/// Авто-детект формата по байтам («ключи» формата).
/// Ключ binary STL — точное совпадение длины файла с 84 + 50 × count;
/// ключи ASCII STL и OBJ — первая значимая строка (solid / v / f / mtllib / …);
/// ключ 3MF — ZIP-архив с моделью 3D/3dmodel.model (OPC-контейнер).
/// Бинарная проверка STL идёт перед текстовой: файлы binary STL могут начинаться с «solid».
/// </summary>
internal static class MeshFormatDetector
{
    private const int BinaryHeaderLength = 80;
    private const int BinaryTriangleRecordLength = 50;
    private const int SniffByteCount = 4096;
    private const string ThreeMfModelPart = "3D/3dmodel.model";

    private static readonly HashSet<string> ObjKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "v", "vn", "vt", "vp", "f", "o", "g", "s", "l", "p", "mtllib", "usemtl", "bevel", "c_interp",
    };

    public static MeshFileFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2 && data[0] == (byte)'P' && data[1] == (byte)'K')
        {
            return DetectZip(data);
        }

        if (data.Length >= BinaryHeaderLength + 4)
        {
            uint triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(BinaryHeaderLength, 4));
            long expectedLength = BinaryHeaderLength + 4L + BinaryTriangleRecordLength * triangleCount;
            if (expectedLength == data.Length)
            {
                return MeshFileFormat.BinaryStl;
            }
        }

        string? firstKeyword = GetFirstSignificantKeyword(data);
        if (firstKeyword is not null)
        {
            if (firstKeyword.Equals("solid", StringComparison.OrdinalIgnoreCase))
            {
                return MeshFileFormat.AsciiStl;
            }

            if (ObjKeywords.Contains(firstKeyword))
            {
                return MeshFileFormat.Obj;
            }
        }

        throw new InvalidDataException(
            "Не удалось определить формат файла: ожидается STL (binary/ASCII), OBJ или 3MF.");
    }

    /// <summary>ZIP-контейнер: 3MF — если внутри есть обязательная модель 3D/3dmodel.model.</summary>
    private static MeshFileFormat DetectZip(ReadOnlySpan<byte> data)
    {
        try
        {
            using var stream = new MemoryStream(data.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.Equals(ThreeMfModelPart, StringComparison.OrdinalIgnoreCase))
                {
                    return MeshFileFormat.ThreeMf;
                }
            }
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Файл похож на ZIP-архив, но повреждён и не читается как 3MF.", ex);
        }

        throw new InvalidDataException(
            $"ZIP-архив не является 3MF: отсутствует модель {ThreeMfModelPart}.");
    }

    private static string? GetFirstSignificantKeyword(ReadOnlySpan<byte> data)
    {
        int start = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            start = 3;
        }

        int length = Math.Min(data.Length - start, SniffByteCount);
        string text = Encoding.ASCII.GetString(data.Slice(start, length));
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int spaceIndex = line.IndexOfAny([' ', '\t']);
            return spaceIndex < 0 ? line : line[..spaceIndex];
        }

        return null;
    }
}
