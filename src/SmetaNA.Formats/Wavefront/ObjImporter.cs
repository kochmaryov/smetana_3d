using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.Obj;

/// <summary>
/// Импорт OBJ: позиции, грани (v, v/vt/vn, отрицательные индексы), полигоны
/// триангулируются веером. Неподдерживаемые данные (материалы, UV, нормали,
/// группы, сглаживание) перечисляются в отчёте о потерях.
/// </summary>
public static class ObjImporter
{
    /// <summary>Импорт OBJ из массива байт.</summary>
    public static MeshImportResult Import(byte[] data, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        options ??= new MeshImportOptions();
        double scale = options.Unit.ToMillimeters();

        var positions = new List<Vector3d>();
        var accumulator = new TriangleSoupAccumulator(options.WeldVertices);
        var lost = new List<string>();
        var unknownKeywords = new SortedSet<string>(StringComparer.Ordinal);
        bool lostMaterials = false;
        bool lostTextureCoordinates = false;
        bool lostNormals = false;
        bool lostNames = false;
        bool lostSmoothing = false;
        bool lostCurves = false;
        int triangulatedFaces = 0;
        int skippedFaces = 0;

        string text = System.Text.Encoding.UTF8.GetString(data);
        int lineNumber = 0;
        foreach (string rawLine in text.Split('\n'))
        {
            lineNumber++;
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            switch (tokens[0])
            {
                case "v":
                    if (tokens.Length < 4)
                    {
                        throw new InvalidDataException($"Строка {lineNumber}: команда v требует трёх координат.");
                    }

                    positions.Add(new Vector3d(
                        ParseNumber(tokens[1], lineNumber),
                        ParseNumber(tokens[2], lineNumber),
                        ParseNumber(tokens[3], lineNumber)) * scale);
                    break;

                case "f":
                    int added = AddFace(accumulator, positions, tokens, lineNumber, out bool wasTriangulated);
                    if (added == 0)
                    {
                        skippedFaces++;
                    }

                    if (wasTriangulated)
                    {
                        triangulatedFaces++;
                    }

                    break;

                case "vn":
                    lostNormals = true;
                    break;

                case "vt":
                    lostTextureCoordinates = true;
                    break;

                case "mtllib":
                case "usemtl":
                    lostMaterials = true;
                    break;

                case "o":
                case "g":
                    lostNames = true;
                    break;

                case "s":
                    lostSmoothing = true;
                    break;

                case "l":
                case "p":
                    lostCurves = true;
                    break;

                default:
                    unknownKeywords.Add(tokens[0]);
                    break;
            }
        }

        if (lostMaterials)
        {
            lost.Add("материалы (mtllib/usemtl)");
        }

        if (lostTextureCoordinates)
        {
            lost.Add("текстурные координаты (vt)");
        }

        if (lostNormals)
        {
            lost.Add("нормали (vn) — пересчитываются по геометрии");
        }

        if (lostNames)
        {
            lost.Add("имена объектов и группы (o/g)");
        }

        if (lostSmoothing)
        {
            lost.Add("сглаживание (s)");
        }

        if (lostCurves)
        {
            lost.Add("линии и точки (l/p)");
        }

        MeshData mesh = accumulator.ToMeshData();
        var warnings = new List<string>();
        if (mesh.Indices.Count == 0)
        {
            warnings.Add("Файл не содержит треугольников.");
        }

        if (triangulatedFaces > 0)
        {
            warnings.Add($"Полигоны с числом вершин > 3 триангулированы веером: {triangulatedFaces}.");
        }

        if (skippedFaces > 0)
        {
            warnings.Add($"Граней с числом вершин < 3 пропущено: {skippedFaces}.");
        }

        if (unknownKeywords.Count > 0)
        {
            warnings.Add($"Неподдерживаемые команды OBJ пропущены: {string.Join(", ", unknownKeywords)}.");
        }

        if (accumulator.DegenerateTriangleIndices.Count > 0)
        {
            warnings.Add($"Вырожденных треугольников: {accumulator.DegenerateTriangleIndices.Count} (индексы в отчёте).");
        }

        var report = new MeshImportReport(
            MeshFileFormat.Obj,
            options.Unit,
            options.Unit.ToMillimeters(),
            accumulator.SourceVertexCount,
            mesh.Positions.Count,
            mesh.Indices.Count / 3,
            [.. accumulator.DegenerateTriangleIndices],
            lost,
            warnings);

        return new MeshImportResult(mesh, report);
    }

    /// <summary>Импорт OBJ из потока.</summary>
    public static MeshImportResult Import(Stream stream, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Import(buffer.ToArray(), options);
    }

    private static int AddFace(
        TriangleSoupAccumulator accumulator,
        List<Vector3d> positions,
        string[] tokens,
        int lineNumber,
        out bool wasTriangulated)
    {
        wasTriangulated = false;
        int cornerCount = tokens.Length - 1;
        if (cornerCount < 3)
        {
            return 0;
        }

        var corners = new Vector3d[cornerCount];
        for (int i = 0; i < cornerCount; i++)
        {
            string reference = tokens[i + 1];
            int slashIndex = reference.IndexOf('/');
            string vertexToken = slashIndex < 0 ? reference : reference[..slashIndex];
            if (!int.TryParse(vertexToken, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index))
            {
                throw new InvalidDataException($"Строка {lineNumber}: индекс вершины «{vertexToken}» не разбирается.");
            }

            int resolved = index > 0 ? index - 1 : positions.Count + index;
            if (resolved < 0 || resolved >= positions.Count)
            {
                throw new InvalidDataException($"Строка {lineNumber}: индекс вершины {index} вне диапазона (вершин: {positions.Count}).");
            }

            corners[i] = positions[resolved];
        }

        for (int i = 1; i + 1 < cornerCount; i++)
        {
            accumulator.AddTriangle(corners[0], corners[i], corners[i + 1]);
        }

        wasTriangulated = cornerCount > 3;
        return cornerCount - 2;
    }

    private static double ParseNumber(string token, int lineNumber)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new InvalidDataException($"Строка {lineNumber}: число «{token}» не разбирается (ожидается инвариантный формат).");
        }

        return value;
    }
}
