using System.Security.Cryptography;
using System.Text;
using Lib3MF;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ThreeMf;

/// <summary>
/// Экспорт 3MF из <see cref="MeshData"/>/<see cref="HalfEdgeMesh"/> через lib3mf
/// (официальный C#-биндинг, BSD-2-Clause) с явным контролем единиц: координаты (мм)
/// масштабируются в единицы файла, единицы записываются атрибутом unit модели,
/// метаданные — блоком metadata.
/// Вывод детерминирован байт-в-байт: вершины канонизируются (первое появление
/// в потоке треугольников, ключ — сериализованные float-координаты), UUID объекта,
/// сборки и элемента выводятся из SHA-256 содержимого, метаданные пишутся
/// в фиксированном порядке. Точность координат ограничена форматом (3MF хранит
/// Single) — до 1e-6 относительно величины, что заведомо лучше 0,01 мм (DoD §5.4).
/// </summary>
public static class ThreeMfExporter
{
    /// <summary>Число знаков после запятой при записи координат (детерминизм и точность).</summary>
    private const uint WriterDecimalPrecision = 6;

    private const string ApplicationName = "SmetaNA";

    /// <summary>
    /// Пространство имён для пользовательских метаданных: в корневом пространстве 3MF
    /// допускаются только стандартные имена (Title, Designer, …), остальные lib3mf отвергает.
    /// </summary>
    private const string CustomMetaDataNamespace = "urn:smetana:metadata";

    private static readonly string[] StandardMetaDataNames =
    [
        "Title",
        "Designer",
        "Description",
        "Application",
        "CreationDate",
        "ModificationDate",
    ];

    /// <summary>Экспорт 3MF в массив байт.</summary>
    public static byte[] Export(MeshData mesh, MeshExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        Export(mesh, stream, options);
        return stream.ToArray();
    }

    /// <summary>Экспорт 3MF в поток.</summary>
    public static void Export(MeshData mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new MeshExportOptions();

        if (mesh.Indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(mesh));
        }

        foreach (int index in mesh.Indices)
        {
            if (index < 0 || index >= mesh.Positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(mesh), $"Индекс вершины вне диапазона 0..{mesh.Positions.Count - 1}.");
            }
        }

        (List<sPosition> vertices, List<sTriangle> triangles) = PackGeometry(mesh, options.Unit);
        var metaData = BuildMetaData(options);
        byte[] contentHash = ComputeContentHash(options.Unit, options.SafeModelName, metaData, vertices, triangles);

        using CModel model = Wrapper.CreateModel();
        model.SetUnit(ThreeMfUnits.ToModelUnit(options.Unit));

        using (CMetaDataGroup group = model.GetMetaDataGroup())
        {
            foreach ((string name, string value) in metaData)
            {
                bool standard = IsStandardMetaDataName(name);
                group.AddMetaData(
                    standard ? string.Empty : CustomMetaDataNamespace,
                    name,
                    value,
                    "xs:string",
                    !standard);
            }
        }

        using CMeshObject meshObject = model.AddMeshObject();
        meshObject.SetName(options.SafeModelName);
        meshObject.SetUUID(FormatUuid(contentHash, "object"));
        foreach (sPosition vertex in vertices)
        {
            meshObject.AddVertex(vertex);
        }

        foreach (sTriangle triangle in triangles)
        {
            meshObject.AddTriangle(triangle);
        }

        model.SetBuildUUID(FormatUuid(contentHash, "build"));
        using CBuildItem buildItem = model.AddBuildItem(meshObject, IdentityTransform());
        buildItem.SetUUID(FormatUuid(contentHash, "item"));

        using CWriter writer = model.QueryWriter("3mf");
        writer.SetDecimalPrecision(WriterDecimalPrecision);
        writer.WriteToBuffer(out byte[] buffer);
        output.Write(buffer, 0, buffer.Length);
    }

    /// <summary>Экспорт 3MF из топологии меша (полигоны граней триангулируются веером).</summary>
    public static byte[] Export(HalfEdgeMesh mesh, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Export(mesh.ToTriangles(), options);
    }

    /// <summary>Экспорт 3MF из топологии меша в поток.</summary>
    public static void Export(HalfEdgeMesh mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        Export(mesh.ToTriangles(), output, options);
    }

    /// <summary>
    /// Каноническая упаковка геометрии: вершины в порядке первого появления
    /// в потоке треугольников (сварка по сериализованным float-координатам —
    /// ровно те различия, которые переживут запись в 3MF).
    /// </summary>
    private static (List<sPosition> Vertices, List<sTriangle> Triangles) PackGeometry(MeshData mesh, LengthUnit unit)
    {
        var vertices = new List<sPosition>();
        var triangles = new List<sTriangle>();
        var indexOf = new Dictionary<(float X, float Y, float Z), uint>();
        double scale = 1.0 / unit.ToMillimeters();

        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            uint i0 = PackVertex(mesh.Positions[mesh.Indices[i]], scale, indexOf, vertices);
            uint i1 = PackVertex(mesh.Positions[mesh.Indices[i + 1]], scale, indexOf, vertices);
            uint i2 = PackVertex(mesh.Positions[mesh.Indices[i + 2]], scale, indexOf, vertices);
            triangles.Add(new sTriangle { Indices = [i0, i1, i2] });
        }

        return (vertices, triangles);
    }

    private static uint PackVertex(
        Vector3d position,
        double scale,
        Dictionary<(float X, float Y, float Z), uint> indexOf,
        List<sPosition> vertices)
    {
        var key = (
            X: (float)(position.X * scale),
            Y: (float)(position.Y * scale),
            Z: (float)(position.Z * scale));
        if (indexOf.TryGetValue(key, out uint index))
        {
            return index;
        }

        vertices.Add(new sPosition { Coordinates = [key.X, key.Y, key.Z] });
        index = (uint)(vertices.Count - 1);
        indexOf[key] = index;
        return index;
    }

    private static List<(string Name, string Value)> BuildMetaData(MeshExportOptions options)
    {
        var custom = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (options.Metadata is not null)
        {
            foreach ((string key, string value) in options.Metadata)
            {
                string name = SanitizeMetaDataText(key);
                if (name.Length > 0)
                {
                    custom[name] = SanitizeMetaDataText(value ?? string.Empty);
                }
            }
        }

        var entries = new List<(string Name, string Value)>();
        foreach (string standardName in StandardMetaDataNames)
        {
            if (custom.Remove(standardName, out string? value))
            {
                entries.Add((standardName, value));
            }
            else if (standardName == "Title")
            {
                entries.Add((standardName, options.SafeModelName));
            }
            else if (standardName == "Application")
            {
                entries.Add((standardName, ApplicationName));
            }
        }

        foreach ((string name, string value) in custom)
        {
            entries.Add((name, value));
        }

        return entries;
    }

    /// <summary>
    /// Детерминированный UUID (RFC 4122, версия 4) из SHA-256 содержимого —
    /// байтовая стабильность экспорта без случайных идентификаторов lib3mf.
    /// </summary>
    private static byte[] ComputeContentHash(
        LengthUnit unit,
        string modelName,
        List<(string Name, string Value)> metaData,
        List<sPosition> vertices,
        List<sTriangle> triangles)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(UnitConversion.GetName(unit));
            writer.Write('\n');
            writer.Write(modelName);
            writer.Write('\n');
            foreach ((string name, string value) in metaData)
            {
                writer.Write(name);
                writer.Write('=');
                writer.Write(value);
                writer.Write('\n');
            }

            foreach (sPosition vertex in vertices)
            {
                writer.Write(vertex.Coordinates[0]);
                writer.Write(vertex.Coordinates[1]);
                writer.Write(vertex.Coordinates[2]);
            }

            foreach (sTriangle triangle in triangles)
            {
                writer.Write(triangle.Indices[0]);
                writer.Write(triangle.Indices[1]);
                writer.Write(triangle.Indices[2]);
            }
        }

        return SHA256.HashData(buffer.ToArray());
    }

    private static string FormatUuid(byte[] contentHash, string role)
    {
        byte[] roleBytes = Encoding.UTF8.GetBytes(role);
        byte[] data = new byte[contentHash.Length + roleBytes.Length];
        contentHash.CopyTo(data, 0);
        roleBytes.CopyTo(data, contentHash.Length);

        byte[] digest = SHA256.HashData(data)[..16];
        digest[6] = (byte)((digest[6] & 0x0F) | 0x40);
        digest[8] = (byte)((digest[8] & 0x3F) | 0x80);
        return new Guid(digest).ToString();
    }

    private static sTransform IdentityTransform() => new()
    {
        Fields =
        [
            [1, 0, 0],
            [0, 1, 0],
            [0, 0, 1],
            [0, 0, 0],
        ],
    };

    private static bool IsStandardMetaDataName(string name) =>
        Array.Exists(StandardMetaDataNames, standard => standard.Equals(name, StringComparison.Ordinal));

    /// <summary>Имя и значения metadata без управляющих символов (иначе получится невалидный XML).</summary>
    private static string SanitizeMetaDataText(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]))
            {
                chars[i] = ' ';
            }
        }

        return new string(chars).Trim();
    }
}
