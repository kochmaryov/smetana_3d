using System.Buffers.Binary;
using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.Stl;

/// <summary>
/// Экспорт STL (binary и ASCII) из <see cref="MeshData"/>/<see cref="HalfEdgeMesh"/>
/// с явным контролем единиц: координаты в мм масштабируются в единицы файла.
/// Заголовок binary STL детерминирован (без дат/имён файлов) — байтовая стабильность.
/// </summary>
public static class StlExporter
{
    private const int HeaderLength = 80;
    private const int TriangleRecordLength = 50;
    private const string BannerPrefix = "SmetaNA binary STL; units: ";

    /// <summary>Экспорт binary STL в массив байт.</summary>
    public static byte[] ExportBinary(MeshData mesh, MeshExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        ExportBinary(mesh, stream, options);
        return stream.ToArray();
    }

    /// <summary>Экспорт binary STL в поток (структура: заголовок 80 байт + count + 50 байт на грань).</summary>
    public static void ExportBinary(MeshData mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new MeshExportOptions();

        double scale = 1.0 / options.Unit.ToMillimeters();
        (Vector3d A, Vector3d B, Vector3d C)[] triangles = GetTriangles(mesh, scale);

        WriteBinaryHeader(output, options.Unit);
        WriteUInt32(output, (uint)triangles.Length);

        Span<byte> record = stackalloc byte[TriangleRecordLength];
        foreach ((Vector3d a, Vector3d b, Vector3d c) in triangles)
        {
            Vector3d normal = Vector3d.Cross(b - a, c - a).Normalized();
            WriteSingle(record, 0, (float)normal.X);
            WriteSingle(record, 4, (float)normal.Y);
            WriteSingle(record, 8, (float)normal.Z);
            WriteVertex(record, 12, a);
            WriteVertex(record, 24, b);
            WriteVertex(record, 36, c);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(48, 2), 0);
            output.Write(record);
        }
    }

    /// <summary>Экспорт binary STL из топологии меша (через <see cref="HalfEdgeMesh.ToTriangles"/>).</summary>
    public static byte[] ExportBinary(HalfEdgeMesh mesh, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return ExportBinary(mesh.ToTriangles(), options);
    }

    /// <summary>Экспорт binary STL из топологии меша в поток.</summary>
    public static void ExportBinary(HalfEdgeMesh mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ExportBinary(mesh.ToTriangles(), output, options);
    }

    /// <summary>Экспорт ASCII STL в массив байт (UTF-8 без BOM, переводы строк LF).</summary>
    public static byte[] ExportAscii(MeshData mesh, MeshExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        ExportAscii(mesh, stream, options);
        return stream.ToArray();
    }

    /// <summary>Экспорт ASCII STL в поток.</summary>
    public static void ExportAscii(MeshData mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new MeshExportOptions();

        double scale = 1.0 / options.Unit.ToMillimeters();
        (Vector3d A, Vector3d B, Vector3d C)[] triangles = GetTriangles(mesh, scale);
        string name = options.SafeModelName;

        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
        writer.Write("solid ");
        writer.Write(name);
        writer.Write('\n');
        foreach ((Vector3d a, Vector3d b, Vector3d c) in triangles)
        {
            Vector3d normal = Vector3d.Cross(b - a, c - a).Normalized();
            writer.Write("facet normal ");
            writer.Write(TextFormat.Stl((float)normal.X));
            writer.Write(' ');
            writer.Write(TextFormat.Stl((float)normal.Y));
            writer.Write(' ');
            writer.Write(TextFormat.Stl((float)normal.Z));
            writer.Write('\n');
            writer.Write("  outer loop\n");
            WriteAsciiVertex(writer, a);
            WriteAsciiVertex(writer, b);
            WriteAsciiVertex(writer, c);
            writer.Write("  endloop\n");
            writer.Write("endfacet\n");
        }

        writer.Write("endsolid ");
        writer.Write(name);
        writer.Write('\n');
        writer.Flush();
    }

    /// <summary>Экспорт ASCII STL из топологии меша.</summary>
    public static byte[] ExportAscii(HalfEdgeMesh mesh, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return ExportAscii(mesh.ToTriangles(), options);
    }

    /// <summary>Экспорт ASCII STL из топологии меша в поток.</summary>
    public static void ExportAscii(HalfEdgeMesh mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ExportAscii(mesh.ToTriangles(), output, options);
    }

    private static void WriteAsciiVertex(StreamWriter writer, Vector3d vertex)
    {
        writer.Write("    vertex ");
        writer.Write(TextFormat.Stl((float)vertex.X));
        writer.Write(' ');
        writer.Write(TextFormat.Stl((float)vertex.Y));
        writer.Write(' ');
        writer.Write(TextFormat.Stl((float)vertex.Z));
        writer.Write('\n');
    }

    private static (Vector3d A, Vector3d B, Vector3d C)[] GetTriangles(MeshData mesh, double scale)
    {
        if (mesh.Indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(mesh));
        }

        var triangles = new (Vector3d, Vector3d, Vector3d)[mesh.Indices.Count / 3];
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            triangles[i / 3] = (
                GetScaledPosition(mesh, mesh.Indices[i], scale),
                GetScaledPosition(mesh, mesh.Indices[i + 1], scale),
                GetScaledPosition(mesh, mesh.Indices[i + 2], scale));
        }

        return triangles;
    }

    private static Vector3d GetScaledPosition(MeshData mesh, int index, double scale)
    {
        if (index < 0 || index >= mesh.Positions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(mesh), $"Индекс вершины {index} вне диапазона 0..{mesh.Positions.Count - 1}.");
        }

        return mesh.Positions[index] * scale;
    }

    private static void WriteBinaryHeader(Stream output, LengthUnit unit)
    {
        string banner = BannerPrefix + UnitConversion.GetName(unit);
        byte[] header = new byte[HeaderLength];
        Encoding.ASCII.GetBytes(banner, 0, Math.Min(banner.Length, HeaderLength), header, 0);
        output.Write(header);
    }

    private static void WriteVertex(Span<byte> record, int offset, Vector3d vertex)
    {
        WriteSingle(record, offset, (float)vertex.X);
        WriteSingle(record, offset + 4, (float)vertex.Y);
        WriteSingle(record, offset + 8, (float)vertex.Z);
    }

    private static void WriteSingle(Span<byte> record, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(record.Slice(offset, 4), value);

    private static void WriteUInt32(Stream output, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        output.Write(buffer);
    }
}
