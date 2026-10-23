using System.Buffers.Binary;
using System.Text;

namespace SmetaNA.Formats;

/// <summary>
/// Авто-детект формата по байтам («ключи» формата).
/// Ключ binary STL — точное совпадение длины файла с 84 + 50 × count;
/// ключи ASCII STL и OBJ — первая значимая строка (solid / v / f / mtllib / …).
/// Бинарная проверка идёт первой: файлы binary STL могут начинаться с «solid».
/// </summary>
internal static class MeshFormatDetector
{
    private const int BinaryHeaderLength = 80;
    private const int BinaryTriangleRecordLength = 50;
    private const int SniffByteCount = 4096;

    private static readonly HashSet<string> ObjKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "v", "vn", "vt", "vp", "f", "o", "g", "s", "l", "p", "mtllib", "usemtl", "bevel", "c_interp",
    };

    public static MeshFileFormat Detect(ReadOnlySpan<byte> data)
    {
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
            "Не удалось определить формат файла: ожидается STL (binary/ASCII) или OBJ.");
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
