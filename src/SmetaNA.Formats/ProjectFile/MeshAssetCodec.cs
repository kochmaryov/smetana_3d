using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Бинарный кодек ассета меша (модуль M9: «меши — бинарно, double-позиции»).
/// <para>
/// Раскладка (little-endian): магия «SNAM» (4 байта) + int32 версия формата ассета;
/// int32 число вершин, затем по 3 double (X, Y, Z) на вершину; int32 число граней,
/// затем для каждой грани int32 MaterialIndex, int32 длина кольца и индексы вершин
/// (int32) по кольцу. Полигональные грани (не только треугольники) сохраняются
/// как есть — точная топология round-trip.
/// </para>
/// </summary>
internal static class MeshAssetCodec
{
    private const int Version = 1;
    private const int HeaderLength = 4 + 4 + 4;
    private const int BytesPerVertex = 3 * 8;

    private static readonly byte[] Magic = "SNAM"u8.ToArray();

    /// <summary>Кодировать меш в бинарный ассет; неконечные координаты — ошибка данных.</summary>
    public static byte[] Encode(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(mesh.Vertices.Count);
            foreach (MeshVertex vertex in mesh.Vertices)
            {
                Vector3d position = vertex.Position;
                if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z))
                {
                    throw new ProjectFileException(
                        "Меш содержит неконечные координаты вершин — запись проекта остановлена без потери файла.");
                }

                writer.Write(position.X);
                writer.Write(position.Y);
                writer.Write(position.Z);
            }

            writer.Write(mesh.Faces.Count);
            foreach (MeshFace face in mesh.Faces)
            {
                List<int> ring = [];
                MeshHalfEdge edge = face.HalfEdge;
                do
                {
                    if (!indexOf.TryGetValue(edge.Origin, out int vertexIndex))
                    {
                        throw new ProjectFileException("Грань меша ссылается на вершину вне списка вершин.");
                    }

                    ring.Add(vertexIndex);
                    edge = edge.Next ?? throw new ProjectFileException("Разомкнутое кольцо полуребер в грани меша.");
                }
                while (edge != face.HalfEdge);

                writer.Write(face.MaterialIndex);
                writer.Write(ring.Count);
                foreach (int vertexIndex in ring)
                {
                    writer.Write(vertexIndex);
                }
            }
        }

        return buffer.ToArray();
    }

    /// <summary>SHA-256 ассета в нижнем hex-регистре (контроль повреждений при чтении).</summary>
    public static string ComputeHash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Декодировать меш из бинарного ассета; повреждения — <see cref="ProjectFileException"/>.</summary>
    public static HalfEdgeMesh Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return DecodeCore(data);
        }
        catch (EndOfStreamException ex)
        {
            throw new ProjectFileException("Бинарный ассет меша обрезан или повреждён.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new ProjectFileException("Бинарный ассет меша повреждён.", ex);
        }
    }

    private static HalfEdgeMesh DecodeCore(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream);

        byte[] magic = reader.ReadBytes(4);
        if (magic.Length != 4 || !magic.AsSpan().SequenceEqual(Magic))
        {
            throw new ProjectFileException("Ассет меша имеет неизвестную сигнатуру (ожидалась «SNAM»).");
        }

        int version = reader.ReadInt32();
        if (version != Version)
        {
            throw new ProjectFileException($"Ассет меша имеет неизвестную версию {version} (ожидалась {Version}).");
        }

        int vertexCount = ReadCount(reader, "вершин");
        long remaining = stream.Length - stream.Position;
        if (vertexCount * (long)BytesPerVertex > remaining)
        {
            throw new ProjectFileException("Число вершин ассета не укладывается в размер файла — ассет повреждён.");
        }

        var mesh = new HalfEdgeMesh();
        var vertices = new MeshVertex[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            var position = new Vector3d(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            vertices[i] = mesh.AddVertex(position);
        }

        int faceCount = ReadCount(reader, "граней");
        for (int i = 0; i < faceCount; i++)
        {
            int materialIndex = reader.ReadInt32();
            int ringLength = ReadCount(reader, "вершин грани");
            if (ringLength < 3)
            {
                throw new ProjectFileException("Грань ассета имеет меньше трёх вершин — ассет повреждён.");
            }

            if (ringLength > remaining)
            {
                throw new ProjectFileException("Кольцо грани не укладывается в размер файла — ассет повреждён.");
            }

            var ring = new MeshVertex[ringLength];
            for (int j = 0; j < ringLength; j++)
            {
                int vertexIndex = reader.ReadInt32();
                if ((uint)vertexIndex >= (uint)vertexCount)
                {
                    throw new ProjectFileException("Грань ассета ссылается на несуществующую вершину.");
                }

                ring[j] = vertices[vertexIndex];
            }

            try
            {
                mesh.AddFace(ring).MaterialIndex = materialIndex;
            }
            catch (InvalidOperationException ex)
            {
                throw new ProjectFileException("Топология ассета меша невалидна (повтор ребра или неманифолдность).", ex);
            }
        }

        return mesh;
    }

    private static int ReadCount(BinaryReader reader, string what)
    {
        int count = reader.ReadInt32();
        if (count < 0)
        {
            throw new ProjectFileException($"Отрицательное число {what} в ассете меша — ассет повреждён.");
        }

        return count;
    }
}
