using Lib3MF;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ThreeMf;

/// <summary>
/// Импорт 3MF через lib3mf (официальный C#-биндинг, BSD-2-Clause): чтение модели
/// с единицами из файла (атрибут unit), нормализация к миллиметрам, раскрытие сборок
/// (components) и трансформаций build items в треугольный суп, сварка вершин,
/// валидация и отчёт о потерях. Единицы задаются самим файлом —
/// <see cref="MeshImportOptions.Unit"/> для 3MF не используется (файл самоописываем).
/// </summary>
public static class ThreeMfImporter
{
    /// <summary>Предохранитель от циклических сборок (lib3mf их отвергает, но файл может быть повреждён).</summary>
    private const int MaxComponentDepth = 32;

    /// <summary>Импорт 3MF из массива байт.</summary>
    public static MeshImportResult Import(byte[] data, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        options ??= new MeshImportOptions();
        return Parse(data, options);
    }

    /// <summary>Импорт 3MF из потока.</summary>
    public static MeshImportResult Import(Stream stream, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new MeshImportOptions();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(buffer.ToArray(), options);
    }

    private static MeshImportResult Parse(byte[] data, MeshImportOptions options)
    {
        using CModel model = Wrapper.CreateModel();
        using CReader reader = model.QueryReader("3mf");
        try
        {
            reader.ReadFromBuffer(data);
        }
        catch (ELib3MFException ex)
        {
            throw new InvalidDataException($"3MF повреждён или не поддерживается: {ex.Message}", ex);
        }

        var readerWarnings = new List<string>();
        uint warningCount = reader.GetWarningCount();
        for (uint i = 0; i < warningCount; i++)
        {
            string message = reader.GetWarning(i, out uint code);
            readerWarnings.Add($"Читатель 3MF: {message} (код {code}).");
        }

        eModelUnit modelUnit = model.GetUnit();
        double scale = ThreeMfUnits.MillimetersPerUnit(modelUnit);
        var collector = new Collector(model, scale, options.WeldVertices);
        collector.Collect();
        return collector.BuildResult(modelUnit, readerWarnings);
    }

    /// <summary>Сбор треугольного супа из объектов модели: сварка и регистрация вырожденных треугольников.</summary>
    private sealed class Sink
    {
        private readonly bool _weldVertices;
        private readonly List<Vector3d> _positions = [];
        private readonly List<int> _indices = [];
        private readonly List<int> _degenerateTriangles = [];
        private readonly Dictionary<(double X, double Y, double Z), int> _weldMap = [];

        public Sink(bool weldVertices)
        {
            _weldVertices = weldVertices;
        }

        /// <summary>Записей вершин во всех файлах модели (до сварки).</summary>
        public int SourceVertexCount { get; private set; }

        public IReadOnlyList<int> DegenerateTriangleIndices => _degenerateTriangles;

        public int AddVertex(Vector3d position)
        {
            if (!_weldVertices)
            {
                _positions.Add(position);
                return _positions.Count - 1;
            }

            var key = (position.X, position.Y, position.Z);
            if (_weldMap.TryGetValue(key, out int index))
            {
                return index;
            }

            _positions.Add(position);
            index = _positions.Count - 1;
            _weldMap[key] = index;
            return index;
        }

        public void AddSourceVertexCount(int count)
        {
            SourceVertexCount += count;
        }

        public void AddTriangle(int i0, int i1, int i2)
        {
            int triangleIndex = _indices.Count / 3;
            if (i0 == i1 || i1 == i2 || i0 == i2 ||
                TriangleGeometry.IsDegenerate(_positions[i0], _positions[i1], _positions[i2]))
            {
                _degenerateTriangles.Add(triangleIndex);
            }

            _indices.Add(i0);
            _indices.Add(i1);
            _indices.Add(i2);
        }

        public MeshData ToMeshData() => new([.. _positions], [.. _indices]);
    }

    /// <summary>
    /// Обход графа объектов модели: build items → объекты (mesh или components),
    /// трансформации накапливаются до координат сборки; признаки потерь и предупреждения
    /// собираются для <see cref="MeshImportReport"/>.
    /// </summary>
    private sealed class Collector
    {
        private readonly CModel _model;
        private readonly double _scale;
        private readonly Sink _sink;
        private readonly List<string> _warnings = [];

        private bool _lostMetadata;
        private bool _lostNames;
        private bool _lostUuids;
        private bool _lostComponents;
        private bool _lostMaterials;
        private bool _lostTextures;
        private bool _lostAuxiliary;
        private bool _reportedDepthLimit;
        private int _meshObjectCount;

        public Collector(CModel model, double scale, bool weldVertices)
        {
            _model = model;
            _scale = scale;
            _sink = new Sink(weldVertices);
        }

        public void Collect()
        {
            DetectLostResources();

            int buildItemCount = 0;
            using (CBuildItemIterator buildItems = _model.GetBuildItems())
            {
                while (buildItems.MoveNext())
                {
                    using CBuildItem buildItem = buildItems.GetCurrent();
                    buildItemCount++;
                    buildItem.GetUUID(out bool itemHasUuid);
                    _lostUuids |= itemHasUuid;
                    CObject resource = buildItem.GetObjectResource();
                    CollectObject(resource, ToTransform3d(buildItem.GetObjectTransform()), 0);
                }
            }

            if (buildItemCount == 0)
            {
                _warnings.Add("Файл не содержит элементов сборки (build items) — импортированы все объекты модели.");
                using CObjectIterator objects = _model.GetObjects();
                while (objects.MoveNext())
                {
                    using CObject resource = objects.GetCurrentObject();
                    CollectObject(resource, Transform3d.Identity, 0);
                }
            }
        }

        public MeshImportResult BuildResult(eModelUnit modelUnit, List<string> readerWarnings)
        {
            MeshData mesh = _sink.ToMeshData();
            var warnings = new List<string>(readerWarnings);
            if (mesh.Indices.Count == 0)
            {
                warnings.Add("Файл не содержит треугольников.");
            }

            if (_sink.DegenerateTriangleIndices.Count > 0)
            {
                warnings.Add($"Вырожденных треугольников: {_sink.DegenerateTriangleIndices.Count} (индексы в отчёте).");
            }

            warnings.AddRange(_warnings);
            bool knownUnit = ThreeMfUnits.TryGetLengthUnit(modelUnit, out LengthUnit sourceUnit);
            var report = new MeshImportReport(
                MeshFileFormat.ThreeMf,
                sourceUnit,
                _scale,
                _sink.SourceVertexCount,
                mesh.Positions.Count,
                mesh.Indices.Count / 3,
                [.. _sink.DegenerateTriangleIndices],
                BuildLostFeatures(),
                warnings,
                knownUnit ? null : modelUnit.ToString());

            return new MeshImportResult(mesh, report);
        }

        private void CollectObject(CObject resource, Transform3d transform, int depth)
        {
            if (depth > MaxComponentDepth)
            {
                if (!_reportedDepthLimit)
                {
                    _warnings.Add($"Вложенность сборок глубже {MaxComponentDepth} не раскрывается.");
                    _reportedDepthLimit = true;
                }

                return;
            }

            resource.GetUUID(out bool hasUuid);
            _lostUuids |= hasUuid;

            if (resource is CMeshObject meshObject)
            {
                CollectMesh(meshObject, transform);
                return;
            }

            if (resource.IsMeshObject())
            {
                using CMeshObject byId = _model.GetMeshObjectByID(resource.GetResourceID());
                CollectMesh(byId, transform);
                return;
            }

            if (resource is CComponentsObject components)
            {
                _lostComponents = true;
                uint componentCount = components.GetComponentCount();
                for (uint i = 0; i < componentCount; i++)
                {
                    using CComponent component = components.GetComponent(i);
                    CObject child = component.GetObjectResource();
                    Transform3d childTransform = Transform3d.Multiply(ToTransform3d(component.GetTransform()), transform);
                    CollectObject(child, childTransform, depth + 1);
                }

                return;
            }

            string name = resource.GetName();
            _warnings.Add(string.IsNullOrEmpty(name)
                ? $"Объект типа {resource.GetType()} пропущен: переносятся только сетки (mesh) и сборки (components)."
                : $"Объект «{name}» типа {resource.GetType()} пропущен: переносятся только сетки (mesh) и сборки (components).");
        }

        private void CollectMesh(CMeshObject meshObject, Transform3d transform)
        {
            _meshObjectCount++;
            if (!string.IsNullOrEmpty(meshObject.GetName()))
            {
                _lostNames = true;
            }

            meshObject.GetVertices(out sPosition[] vertices);
            meshObject.GetTriangleIndices(out sTriangle[] triangles);

            bool hasTransform = transform != Transform3d.Identity;
            var mapped = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Single[] coordinates = vertices[i].Coordinates;
                var position = new Vector3d(coordinates[0], coordinates[1], coordinates[2]);
                if (hasTransform)
                {
                    position = transform.TransformPoint(position);
                }

                mapped[i] = _sink.AddVertex(position * _scale);
            }

            _sink.AddSourceVertexCount(vertices.Length);

            foreach (sTriangle triangle in triangles)
            {
                int i0 = MapIndex(triangle.Indices[0], mapped);
                int i1 = MapIndex(triangle.Indices[1], mapped);
                int i2 = MapIndex(triangle.Indices[2], mapped);
                _sink.AddTriangle(i0, i1, i2);
            }

            if (triangles.Length > 0 && !meshObject.IsManifoldAndOriented())
            {
                string name = meshObject.GetName();
                _warnings.Add(string.IsNullOrEmpty(name)
                    ? "Меш не замкнут и/или не ориентирован (IsManifoldAndOriented = false)."
                    : $"Меш «{name}» не замкнут и/или не ориентирован (IsManifoldAndOriented = false).");
            }
        }

        private int MapIndex(uint index, int[] mapped)
        {
            if (index >= mapped.Length)
            {
                throw new InvalidDataException($"3MF повреждён: индекс вершины {index} вне диапазона (вершин: {mapped.Length}).");
            }

            return mapped[index];
        }

        private void DetectLostResources()
        {
            using (CMetaDataGroup metaData = _model.GetMetaDataGroup())
            {
                _lostMetadata = metaData.GetMetaDataCount() > 0;
            }

            using (CBaseMaterialGroupIterator groups = _model.GetBaseMaterialGroups())
            {
                _lostMaterials = groups.MoveNext();
            }

            using (CColorGroupIterator groups = _model.GetColorGroups())
            {
                _lostMaterials |= groups.MoveNext();
            }

            using (CMultiPropertyGroupIterator groups = _model.GetMultiPropertyGroups())
            {
                _lostMaterials |= groups.MoveNext();
            }

            using (CCompositeMaterialsIterator groups = _model.GetCompositeMaterials())
            {
                _lostMaterials |= groups.MoveNext();
            }

            using (CTexture2DIterator textures = _model.GetTexture2Ds())
            {
                _lostTextures = textures.MoveNext();
            }

            using (CTexture2DGroupIterator groups = _model.GetTexture2DGroups())
            {
                _lostTextures |= groups.MoveNext();
            }

            using (CSliceStackIterator slices = _model.GetSliceStacks())
            {
                _lostAuxiliary = slices.MoveNext();
            }

            using (CImage3DIterator images = _model.GetImage3Ds())
            {
                _lostAuxiliary |= images.MoveNext();
            }

            using (CLevelSetIterator levelSets = _model.GetLevelSets())
            {
                _lostAuxiliary |= levelSets.MoveNext();
            }
        }

        private List<string> BuildLostFeatures()
        {
            var lost = new List<string>();
            if (_lostMetadata)
            {
                lost.Add("метаданные (Title, Designer и др.)");
            }

            if (_lostNames)
            {
                lost.Add("имена объектов (name)");
            }

            if (_meshObjectCount > 1)
            {
                lost.Add($"разделение на объекты ({_meshObjectCount} объектов объединены в один меш)");
            }

            if (_lostComponents)
            {
                lost.Add("сборки (components) — иерархия раскрыта в треугольный суп");
            }

            if (_lostUuids)
            {
                lost.Add("идентификаторы объектов (UUID)");
            }

            if (_lostMaterials)
            {
                lost.Add("материалы, цвета и свойства граней (property groups)");
            }

            if (_lostTextures)
            {
                lost.Add("текстуры (texture2d)");
            }

            if (_lostAuxiliary)
            {
                lost.Add("слайсовые стеки, 3D-изображения и level-set (slicestack/image3d/levelset)");
            }

            return lost;
        }

        /// <summary>
        /// Преобразование 3MF (12 чисел, вектор-строка) в <see cref="Transform3d"/>:
        /// строки m00..m22 — линейная часть, m30..m32 — трансляция.
        /// </summary>
        private static Transform3d ToTransform3d(sTransform transform)
        {
            Single[][] fields = transform.Fields;
            return new Transform3d(
                fields[0][0], fields[0][1], fields[0][2], 0,
                fields[1][0], fields[1][1], fields[1][2], 0,
                fields[2][0], fields[2][1], fields[2][2], 0,
                fields[3][0], fields[3][1], fields[3][2], 1);
        }
    }
}
