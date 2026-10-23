namespace SmetaNA.Rendering;

/// <summary>Режим отображения сцены во вьюпорте (задача 1.4).</summary>
public enum RenderMode
{
    /// <summary>Каркас: только рёбра объектов.</summary>
    Wireframe,

    /// <summary>Штриховка: грани с плоским светом и рёбрами поверх (стиль SketchUp).</summary>
    Shaded,

    /// <summary>Рентген: полупрозрачные грани, рёбра видны сквозь них.</summary>
    XRay,
}
