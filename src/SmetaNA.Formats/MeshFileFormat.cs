namespace SmetaNA.Formats;

/// <summary>Поддерживаемые форматы обмена мешами (модуль M8).</summary>
public enum MeshFileFormat
{
    /// <summary>STL бинарный: 80-байтовый заголовок + count + по 50 байт на треугольник.</summary>
    BinaryStl,

    /// <summary>STL текстовый: facet/outer loop/vertex.</summary>
    AsciiStl,

    /// <summary>Wavefront OBJ: v/f, полигоны, ссылки v/vt/vn.</summary>
    Obj,

    /// <summary>3MF: OPC-архив (ZIP) с моделью 3D/3dmodel.model (XML) и атрибутом unit.</summary>
    ThreeMf,
}
