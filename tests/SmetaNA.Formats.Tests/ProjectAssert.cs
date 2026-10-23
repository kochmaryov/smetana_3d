using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Сравнение документов проектного формата: бит-в-бит по double (позиции вершин,
/// трансформации, вид), полная топология мешей (полигональные кольца), структура
/// сцены и связность компонентов (общие definition/меш остаются общими).
/// </summary>
internal static class ProjectAssert
{
    public static void DoubleBits(double expected, double actual, string what)
    {
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{what}: {expected:R} != {actual:R} (биты {BitConverter.DoubleToInt64Bits(expected)} != {BitConverter.DoubleToInt64Bits(actual)})");
    }

    public static void DocumentsEqual(ProjectDocument expected, ProjectDocument actual)
    {
        Assert.Equal(expected.Unit, actual.Unit);
        Assert.Equal(expected.Metadata.Title, actual.Metadata.Title);
        Assert.Equal(expected.Metadata.Generator, actual.Metadata.Generator);
        Assert.Equal(expected.Metadata.CreatedUtc, actual.Metadata.CreatedUtc);
        Assert.Equal(expected.Metadata.ModifiedUtc, actual.Metadata.ModifiedUtc);

        ProjectViewSettings e = expected.View;
        ProjectViewSettings a = actual.View;
        Assert.Equal(e.Projection, a.Projection);
        DoubleBits(e.Target.X, a.Target.X, "view.target.x");
        DoubleBits(e.Target.Y, a.Target.Y, "view.target.y");
        DoubleBits(e.Target.Z, a.Target.Z, "view.target.z");
        DoubleBits(e.Distance, a.Distance, "view.distance");
        DoubleBits(e.Yaw, a.Yaw, "view.yaw");
        DoubleBits(e.Pitch, a.Pitch, "view.pitch");
        DoubleBits(e.FieldOfViewY, a.FieldOfViewY, "view.fieldOfViewY");
        DoubleBits(e.OrthoHeight, a.OrthoHeight, "view.orthoHeight");
        DoubleBits(e.AspectRatio, a.AspectRatio, "view.aspectRatio");
        DoubleBits(e.NearPlane, a.NearPlane, "view.nearPlane");
        DoubleBits(e.FarPlane, a.FarPlane, "view.farPlane");

        Assert.Equal(expected.Materials.Count, actual.Materials.Count);
        for (int i = 0; i < expected.Materials.Count; i++)
        {
            ProjectMaterial em = expected.Materials[i];
            ProjectMaterial am = actual.Materials[i];
            Assert.Equal(em.Id, am.Id);
            Assert.Equal(em.Name, am.Name);
            DoubleBits(em.Red, am.Red, $"material[{i}].red");
            DoubleBits(em.Green, am.Green, $"material[{i}].green");
            DoubleBits(em.Blue, am.Blue, $"material[{i}].blue");
            DoubleBits(em.Alpha, am.Alpha, $"material[{i}].alpha");
        }

        var definitions = new Dictionary<ComponentDefinition, ComponentDefinition>(ReferenceEqualityComparer.Instance);
        var meshes = new Dictionary<HalfEdgeMesh, HalfEdgeMesh>(ReferenceEqualityComparer.Instance);
        NodesEqual(expected.Scene.Root, actual.Scene.Root, definitions, meshes, isRoot: true);
    }

    private static void NodesEqual(
        SceneNode expected,
        SceneNode actual,
        Dictionary<ComponentDefinition, ComponentDefinition> definitions,
        Dictionary<HalfEdgeMesh, HalfEdgeMesh> meshes,
        bool isRoot)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        if (!isRoot)
        {
            Assert.Equal(expected.Id, actual.Id);
        }

        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.IsVisible, actual.IsVisible);
        TransformsEqual(expected.LocalTransform, actual.LocalTransform, $"узел «{expected.Name}»");

        switch (expected)
        {
            case MeshNode expectedMesh:
                var actualMesh = Assert.IsType<MeshNode>(actual);
                MeshesEqual(expectedMesh.Mesh, actualMesh.Mesh, meshes, expected.Name);
                break;
            case ComponentInstanceNode expectedInstance:
                var actualInstance = Assert.IsType<ComponentInstanceNode>(actual);
                DefinitionsEqual(expectedInstance.Definition, actualInstance.Definition, definitions, meshes);
                break;
        }

        Assert.Equal(expected.Children.Count, actual.Children.Count);
        for (int i = 0; i < expected.Children.Count; i++)
        {
            NodesEqual(expected.Children[i], actual.Children[i], definitions, meshes, isRoot: false);
        }
    }

    private static void DefinitionsEqual(
        ComponentDefinition expected,
        ComponentDefinition actual,
        Dictionary<ComponentDefinition, ComponentDefinition> definitions,
        Dictionary<HalfEdgeMesh, HalfEdgeMesh> meshes)
    {
        if (definitions.TryGetValue(expected, out ComponentDefinition? seen))
        {
            Assert.Same(seen, actual);
            return;
        }

        definitions[expected] = actual;
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        NodesEqual(expected.Content, actual.Content, definitions, meshes, isRoot: false);
    }

    private static void MeshesEqual(
        HalfEdgeMesh expected,
        HalfEdgeMesh actual,
        Dictionary<HalfEdgeMesh, HalfEdgeMesh> meshes,
        string nodeName)
    {
        if (meshes.TryGetValue(expected, out HalfEdgeMesh? seen))
        {
            Assert.Same(seen, actual);
            return;
        }

        meshes[expected] = actual;

        Assert.Equal(expected.Vertices.Count, actual.Vertices.Count);
        for (int i = 0; i < expected.Vertices.Count; i++)
        {
            Vector3d e = expected.Vertices[i].Position;
            Vector3d a = actual.Vertices[i].Position;
            DoubleBits(e.X, a.X, $"«{nodeName}» vertex[{i}].x");
            DoubleBits(e.Y, a.Y, $"«{nodeName}» vertex[{i}].y");
            DoubleBits(e.Z, a.Z, $"«{nodeName}» vertex[{i}].z");
        }

        Assert.Equal(expected.Faces.Count, actual.Faces.Count);
        for (int i = 0; i < expected.Faces.Count; i++)
        {
            Assert.Equal(expected.Faces[i].MaterialIndex, actual.Faces[i].MaterialIndex);
            Assert.Equal(FaceRing(expected, i), FaceRing(actual, i));
        }
    }

    /// <summary>Кольцо грани как список индексов вершин меша.</summary>
    private static List<int> FaceRing(HalfEdgeMesh mesh, int faceIndex)
    {
        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        var ring = new List<int>();
        MeshHalfEdge edge = mesh.Faces[faceIndex].HalfEdge;
        do
        {
            ring.Add(indexOf[edge.Origin]);
            edge = edge.Next!;
        }
        while (edge != mesh.Faces[faceIndex].HalfEdge);

        return ring;
    }

    private static void TransformsEqual(Transform3d expected, Transform3d actual, string what)
    {
        double[] e =
        [
            expected.M11, expected.M12, expected.M13, expected.M14,
            expected.M21, expected.M22, expected.M23, expected.M24,
            expected.M31, expected.M32, expected.M33, expected.M34,
            expected.M41, expected.M42, expected.M43, expected.M44,
        ];
        double[] a =
        [
            actual.M11, actual.M12, actual.M13, actual.M14,
            actual.M21, actual.M22, actual.M23, actual.M24,
            actual.M31, actual.M32, actual.M33, actual.M34,
            actual.M41, actual.M42, actual.M43, actual.M44,
        ];
        for (int i = 0; i < e.Length; i++)
        {
            DoubleBits(e[i], a[i], $"{what} transform[{i}]");
        }
    }
}

/// <summary>Манипуляции с ZIP-контейнером проекта в тестах миграций и повреждений.</summary>
internal static class ProjectZip
{
    /// <summary>Прочитать манифест из контейнера в JSON-дерево.</summary>
    public static JsonObject ReadManifest(byte[] zip)
    {
        using var stream = new MemoryStream(zip, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using Stream manifestStream = archive.GetEntry(ProjectFileFormat.ManifestEntryName)!.Open();
        return (JsonObject)JsonNode.Parse(manifestStream)!;
    }

    /// <summary>Переписать контейнер с новым содержимым манифеста (остальные записи — как есть).</summary>
    public static byte[] RewriteManifest(byte[] zip, Func<JsonObject, JsonObject> mutate)
    {
        var entries = ReadEntries(zip);
        entries[ProjectFileFormat.ManifestEntryName] = Encoding.UTF8.GetBytes(mutate(ReadManifest(zip)).ToJsonString());
        return WriteEntries(entries);
    }

    /// <summary>Переписать контейнер с заменой одного байта в записи (имитация повреждения).</summary>
    public static byte[] CorruptEntry(byte[] zip, string entryName)
    {
        var entries = ReadEntries(zip);
        byte[] content = (byte[])entries[entryName].Clone();
        content[^1] ^= 0xFF;
        entries[entryName] = content;
        return WriteEntries(entries);
    }

    /// <summary>Обрезать запись контейнера (имитация неполной записи снимка).</summary>
    public static byte[] TruncateEntry(byte[] zip, string entryName)
    {
        var entries = ReadEntries(zip);
        entries[entryName] = entries[entryName][..Math.Max(1, entries[entryName].Length / 2)];
        return WriteEntries(entries);
    }

    private static Dictionary<string, byte[]> ReadEntries(byte[] zip)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var stream = new MemoryStream(zip, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using Stream source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            entries[entry.FullName] = buffer.ToArray();
        }

        return entries;
    }

    private static byte[] WriteEntries(Dictionary<string, byte[]> entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using Stream target = entry.Open();
                target.Write(content, 0, content.Length);
            }
        }

        return buffer.ToArray();
    }
}
