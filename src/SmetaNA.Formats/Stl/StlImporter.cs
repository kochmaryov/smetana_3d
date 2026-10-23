using System.Buffers.Binary;
using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.Stl;

/// <summary>
/// Импорт STL с авто-детектом binary/ASCII и нормализацией в миллиметры.
/// Нормали из файла игнорируются (пересчитываются при экспорте), записи вершин
/// свариваются по точной позиции; о потерях — в <see cref="MeshImportReport"/>.
/// </summary>
public static class StlImporter
{
    private const int BinaryHeaderLength = 80;
    private const int BinaryTriangleRecordLength = 50;

    private static readonly string[] StlLostFeatures =
    [
        "имена объектов, группы и структура сцены (STL не хранит)",
        "материалы и цвета (STL не хранит)",
        "нормали из файла (пересчитываются по геометрии)",
    ];

    /// <summary>Импорт STL из массива байт с авто-детектом binary/ASCII.</summary>
    public static MeshImportResult Import(byte[] data, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        options ??= new MeshImportOptions();

        return MeshFormatDetector.Detect(data) switch
        {
            MeshFileFormat.BinaryStl => ParseBinary(data, options),
            MeshFileFormat.AsciiStl => ParseAscii(data, options),
            _ => throw new InvalidDataException("Файл распознан как OBJ — используйте ObjImporter."),
        };
    }

    /// <summary>Импорт STL из потока с авто-детектом binary/ASCII.</summary>
    public static MeshImportResult Import(Stream stream, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Import(buffer.ToArray(), options);
    }

    private static MeshImportResult ParseBinary(byte[] data, MeshImportOptions options)
    {
        if (data.Length < BinaryHeaderLength + 4)
        {
            throw new InvalidDataException("Файл слишком короткий для binary STL.");
        }

        uint triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(BinaryHeaderLength, 4));
        long expectedLength = BinaryHeaderLength + 4L + BinaryTriangleRecordLength * triangleCount;
        if (data.Length < expectedLength)
        {
            throw new InvalidDataException(
                $"Binary STL повреждён: ожидается {expectedLength} байт, получено {data.Length}.");
        }

        double scale = options.Unit.ToMillimeters();
        var accumulator = new TriangleSoupAccumulator(options.WeldVertices);
        for (uint i = 0; i < triangleCount; i++)
        {
            int offset = BinaryHeaderLength + 4 + (int)(BinaryTriangleRecordLength * i);
            Vector3d a = ReadVertex(data, offset + 12, scale);
            Vector3d b = ReadVertex(data, offset + 24, scale);
            Vector3d c = ReadVertex(data, offset + 36, scale);
            accumulator.AddTriangle(a, b, c);
        }

        return BuildResult(MeshFileFormat.BinaryStl, accumulator, options);
    }

    private static MeshImportResult ParseAscii(byte[] data, MeshImportOptions options)
    {
        double scale = options.Unit.ToMillimeters();
        var accumulator = new TriangleSoupAccumulator(options.WeldVertices);
        string text = System.Text.Encoding.UTF8.GetString(data);

        var corners = new List<Vector3d>(3);
        int lineNumber = 0;
        foreach (string rawLine in text.Split('\n'))
        {
            lineNumber++;
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!tokens[0].Equals("vertex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tokens.Length < 4)
            {
                throw new InvalidDataException($"Строка {lineNumber}: запись vertex требует трёх координат.");
            }

            corners.Add(new Vector3d(
                ParseCoordinate(tokens[1], lineNumber),
                ParseCoordinate(tokens[2], lineNumber),
                ParseCoordinate(tokens[3], lineNumber)) * scale);

            if (corners.Count == 3)
            {
                accumulator.AddTriangle(corners[0], corners[1], corners[2]);
                corners.Clear();
            }
        }

        if (corners.Count != 0)
        {
            throw new InvalidDataException("ASCII STL повреждён: число записей vertex не кратно 3.");
        }

        return BuildResult(MeshFileFormat.AsciiStl, accumulator, options);
    }

    private static MeshImportResult BuildResult(MeshFileFormat format, TriangleSoupAccumulator accumulator, MeshImportOptions options)
    {
        MeshData mesh = accumulator.ToMeshData();
        var warnings = new List<string>();
        if (mesh.Indices.Count == 0)
        {
            warnings.Add("Файл не содержит треугольников.");
        }

        if (accumulator.DegenerateTriangleIndices.Count > 0)
        {
            warnings.Add($"Вырожденных треугольников: {accumulator.DegenerateTriangleIndices.Count} (индексы в отчёте).");
        }

        var report = new MeshImportReport(
            format,
            options.Unit,
            options.Unit.ToMillimeters(),
            accumulator.SourceVertexCount,
            mesh.Positions.Count,
            mesh.Indices.Count / 3,
            [.. accumulator.DegenerateTriangleIndices],
            StlLostFeatures,
            warnings);

        return new MeshImportResult(mesh, report);
    }

    private static Vector3d ReadVertex(byte[] data, int offset, double scale)
    {
        ReadOnlySpan<byte> slice = data.AsSpan(offset, 12);
        return new Vector3d(
            BinaryPrimitives.ReadSingleLittleEndian(slice) * scale,
            BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(4)) * scale,
            BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(8)) * scale);
    }

    private static double ParseCoordinate(string token, int lineNumber)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new InvalidDataException($"Строка {lineNumber}: число «{token}» не разбирается (ожидается инвариантный формат).");
        }

        return value;
    }
}
