using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.Obj;

/// <summary>
/// Экспорт OBJ из <see cref="MeshData"/>/<see cref="HalfEdgeMesh"/> с явным контролем
/// единиц: координаты в мм масштабируются в единицы файла, конвенция единиц
/// записывается комментарием в заголовок (OBJ безразмерен).
/// </summary>
public static class ObjExporter
{
    /// <summary>Экспорт OBJ в массив байт (UTF-8 без BOM, переводы строк LF).</summary>
    public static byte[] Export(MeshData mesh, MeshExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        Export(mesh, stream, options);
        return stream.ToArray();
    }

    /// <summary>Экспорт OBJ в поток.</summary>
    public static void Export(MeshData mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new MeshExportOptions();

        if (mesh.Indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(mesh));
        }

        double scale = 1.0 / options.Unit.ToMillimeters();
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);

        writer.Write("# SmetaNA OBJ export\n");
        writer.Write("# units: ");
        writer.Write(UnitConversion.GetName(options.Unit));
        writer.Write(" (1 unit = ");
        writer.Write(TextFormat.Obj(UnitConversion.MillimetersPerUnit(options.Unit)));
        writer.Write(" mm)\n");
        writer.Write("o ");
        writer.Write(options.SafeModelName);
        writer.Write('\n');

        foreach (Vector3d position in mesh.Positions)
        {
            Vector3d scaled = position * scale;
            writer.Write("v ");
            writer.Write(TextFormat.Obj(scaled.X));
            writer.Write(' ');
            writer.Write(TextFormat.Obj(scaled.Y));
            writer.Write(' ');
            writer.Write(TextFormat.Obj(scaled.Z));
            writer.Write('\n');
        }

        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            int i0 = mesh.Indices[i];
            int i1 = mesh.Indices[i + 1];
            int i2 = mesh.Indices[i + 2];
            if (i0 < 0 || i0 >= mesh.Positions.Count ||
                i1 < 0 || i1 >= mesh.Positions.Count ||
                i2 < 0 || i2 >= mesh.Positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(mesh), $"Индекс вершины вне диапазона 0..{mesh.Positions.Count - 1}.");
            }

            writer.Write("f ");
            writer.Write(i0 + 1);
            writer.Write(' ');
            writer.Write(i1 + 1);
            writer.Write(' ');
            writer.Write(i2 + 1);
            writer.Write('\n');
        }

        writer.Flush();
    }

    /// <summary>Экспорт OBJ из топологии меша (полигоны граней триангулируются веером).</summary>
    public static byte[] Export(HalfEdgeMesh mesh, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Export(mesh.ToTriangles(), options);
    }

    /// <summary>Экспорт OBJ из топологии меша в поток.</summary>
    public static void Export(HalfEdgeMesh mesh, Stream output, MeshExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Export(mesh.ToTriangles(), output, options);
    }
}
