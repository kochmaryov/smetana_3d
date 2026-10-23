using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.Tests.ReferenceModels;

/// <summary>
/// Эталонные модели (задача 4.6, DoD §5.3): программируемые фикстуры через
/// API меша — «стакан», «подставка» и «корпус для электроники» (сценарий S3)
/// с параметрами (габариты, толщина стенки, вырезы). Все модели — замкнутые
/// ориентированные «solid»-меши с нормалями наружу, единицы — миллиметры.
/// </summary>
internal static class ReferenceModelFactory
{
    /// <summary>
    /// «Стакан»: полый цилиндр с открытым верхом (D60×H80 мм, стенка 5 мм,
    /// дно 5 мм по умолчанию).
    /// </summary>
    public static MeshData Cup(
        double outerDiameterMm = 60,
        double heightMm = 80,
        double wallMm = 5,
        double bottomMm = 5,
        int segments = 48)
    {
        if (outerDiameterMm <= 0 || heightMm <= 0 || wallMm <= 0 || bottomMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outerDiameterMm), "Размеры стакана должны быть положительными.");
        }

        if (wallMm * 2 >= outerDiameterMm || bottomMm >= heightMm)
        {
            throw new ArgumentException("Стенка/дно не помещаются в габариты стакана.");
        }

        if (segments < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), "Окружности нужно минимум 8 сегментов.");
        }

        double outerR = outerDiameterMm * 0.5;
        double innerR = outerR - wallMm;
        var builder = new MeshBuilder();

        int[] outerBottom = new int[segments];
        int[] outerTop = new int[segments];
        int[] innerTop = new int[segments];
        int[] innerBottom = new int[segments];
        for (int i = 0; i < segments; i++)
        {
            double angle = (2.0 * Math.PI * i) / segments;
            double x = Math.Cos(angle);
            double y = Math.Sin(angle);
            outerBottom[i] = builder.AddVertex(new Vector3d(outerR * x, outerR * y, 0));
            outerTop[i] = builder.AddVertex(new Vector3d(outerR * x, outerR * y, heightMm));
            innerTop[i] = builder.AddVertex(new Vector3d(innerR * x, innerR * y, heightMm));
            innerBottom[i] = builder.AddVertex(new Vector3d(innerR * x, innerR * y, bottomMm));
        }

        int bottomCenter = builder.AddVertex(new Vector3d(0, 0, 0));
        int cavityCenter = builder.AddVertex(new Vector3d(0, 0, bottomMm));

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            builder.Quad(outerBottom[i], outerBottom[next], outerTop[next], outerTop[i]);
            builder.Quad(outerTop[i], outerTop[next], innerTop[next], innerTop[i]);
            builder.Quad(innerTop[i], innerTop[next], innerBottom[next], innerBottom[i]);
            builder.Tri(innerBottom[i], innerBottom[next], cavityCenter);
            builder.Tri(outerBottom[next], outerBottom[i], bottomCenter);
        }

        return builder.Build();
    }

    /// <summary>
    /// «Подставка»: ступенька (база 100×60×20 мм + верх 60×60×30 мм по центру).
    /// </summary>
    public static MeshData Stand(
        double baseWidthMm = 100,
        double baseDepthMm = 60,
        double baseHeightMm = 20,
        double stepWidthMm = 60,
        double stepHeightMm = 30)
    {
        if (baseWidthMm <= 0 || baseDepthMm <= 0 || baseHeightMm <= 0 || stepWidthMm <= 0 || stepHeightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseWidthMm), "Размеры подставки должны быть положительными.");
        }

        if (stepWidthMm > baseWidthMm)
        {
            throw new ArgumentException("Верхняя ступень шире базы.");
        }

        double x0 = (baseWidthMm - stepWidthMm) * 0.5;
        double x1 = x0 + stepWidthMm;

        // Профиль в плоскости XZ (CCW), выдавлен вдоль Y на baseDepthMm.
        Vector3d[] profile =
        [
            new(0, 0, 0),
            new(baseWidthMm, 0, 0),
            new(baseWidthMm, 0, baseHeightMm),
            new(x1, 0, baseHeightMm),
            new(x1, 0, baseHeightMm + stepHeightMm),
            new(x0, 0, baseHeightMm + stepHeightMm),
            new(x0, 0, baseHeightMm),
            new(0, 0, baseHeightMm),
        ];

        var builder = new MeshBuilder();
        int[] front = new int[profile.Length];
        int[] back = new int[profile.Length];
        for (int i = 0; i < profile.Length; i++)
        {
            front[i] = builder.AddVertex(profile[i]);
            back[i] = builder.AddVertex(profile[i] + new Vector3d(0, baseDepthMm, 0));
        }

        for (int i = 0; i < profile.Length; i++)
        {
            int next = (i + 1) % profile.Length;
            builder.Quad(front[i], back[i], back[next], front[next]);
        }

        // Торцы: перед (−Y) и зад (+Y), шесть треугольников на каждый.
        builder.Tri(front[0], front[1], front[2]);
        builder.Tri(front[0], front[2], front[3]);
        builder.Tri(front[0], front[3], front[6]);
        builder.Tri(front[0], front[6], front[7]);
        builder.Tri(front[3], front[4], front[5]);
        builder.Tri(front[3], front[5], front[6]);

        builder.Tri(back[0], back[2], back[1]);
        builder.Tri(back[0], back[3], back[2]);
        builder.Tri(back[0], back[6], back[3]);
        builder.Tri(back[0], back[7], back[6]);
        builder.Tri(back[3], back[5], back[4]);
        builder.Tri(back[3], back[6], back[5]);

        return builder.Build();
    }

    /// <summary>
    /// «Корпус для электроники» (сценарий S3): открытая сверху оболочка
    /// (100×80×40 мм, стенка 4 мм, дно 4 мм) с прямоугольным вырезом
    /// 24×12 мм в передней стенке (разъём/кабельный канал).
    /// </summary>
    public static MeshData ElectronicsEnclosure(
        double widthMm = 100,
        double depthMm = 80,
        double heightMm = 40,
        double wallMm = 4,
        double bottomMm = 4,
        double windowWidthMm = 24,
        double windowHeightMm = 12)
    {
        if (widthMm <= 0 || depthMm <= 0 || heightMm <= 0 || wallMm <= 0 || bottomMm <= 0
            || windowWidthMm <= 0 || windowHeightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMm), "Размеры корпуса должны быть положительными.");
        }

        if (wallMm * 2 >= widthMm || wallMm * 2 >= depthMm || bottomMm >= heightMm)
        {
            throw new ArgumentException("Стенка/дно не помещаются в габариты корпуса.");
        }

        double windowX0 = (widthMm - windowWidthMm) * 0.5;
        double windowX1 = windowX0 + windowWidthMm;
        double windowZ0 = bottomMm + ((heightMm - bottomMm - windowHeightMm) * 0.5);
        double windowZ1 = windowZ0 + windowHeightMm;
        if (windowZ0 <= bottomMm || windowZ1 >= heightMm || windowX0 <= wallMm || windowX1 >= widthMm - wallMm)
        {
            throw new ArgumentException("Вырез не помещается в переднюю стенку корпуса.");
        }

        var builder = new MeshBuilder();

        // Восемь внешних и восемь внутренних углов — общие вершины граней.
        int o000 = builder.AddVertex(new Vector3d(0, 0, 0));
        int o100 = builder.AddVertex(new Vector3d(widthMm, 0, 0));
        int o110 = builder.AddVertex(new Vector3d(widthMm, depthMm, 0));
        int o010 = builder.AddVertex(new Vector3d(0, depthMm, 0));
        int o001 = builder.AddVertex(new Vector3d(0, 0, heightMm));
        int o101 = builder.AddVertex(new Vector3d(widthMm, 0, heightMm));
        int o111 = builder.AddVertex(new Vector3d(widthMm, depthMm, heightMm));
        int o011 = builder.AddVertex(new Vector3d(0, depthMm, heightMm));

        int i000 = builder.AddVertex(new Vector3d(wallMm, wallMm, bottomMm));
        int i100 = builder.AddVertex(new Vector3d(widthMm - wallMm, wallMm, bottomMm));
        int i110 = builder.AddVertex(new Vector3d(widthMm - wallMm, depthMm - wallMm, bottomMm));
        int i010 = builder.AddVertex(new Vector3d(wallMm, depthMm - wallMm, bottomMm));
        int i001 = builder.AddVertex(new Vector3d(wallMm, wallMm, heightMm));
        int i101 = builder.AddVertex(new Vector3d(widthMm - wallMm, wallMm, heightMm));
        int i111 = builder.AddVertex(new Vector3d(widthMm - wallMm, depthMm - wallMm, heightMm));
        int i011 = builder.AddVertex(new Vector3d(wallMm, depthMm - wallMm, heightMm));

        // Вырез в передней стенке (y=0 и y=wallMm): кольца по 4 вершины (CCW в XZ).
        int fo0 = builder.AddVertex(new Vector3d(windowX0, 0, windowZ0));
        int fo1 = builder.AddVertex(new Vector3d(windowX1, 0, windowZ0));
        int fo2 = builder.AddVertex(new Vector3d(windowX1, 0, windowZ1));
        int fo3 = builder.AddVertex(new Vector3d(windowX0, 0, windowZ1));
        int io0 = builder.AddVertex(new Vector3d(windowX0, wallMm, windowZ0));
        int io1 = builder.AddVertex(new Vector3d(windowX1, wallMm, windowZ0));
        int io2 = builder.AddVertex(new Vector3d(windowX1, wallMm, windowZ1));
        int io3 = builder.AddVertex(new Vector3d(windowX0, wallMm, windowZ1));

        // Внешние грани (кроме передней с вырезом).
        builder.Quad(o000, o010, o110, o100); // низ (−Z)
        builder.Quad(o000, o001, o011, o010); // лево (−X)
        builder.Quad(o100, o110, o111, o101); // право (+X)
        builder.Quad(o010, o011, o111, o110); // зад (+Y)

        // Передняя стенка (y=0, −Y): внешнее кольцо ↔ кольцо выреза.
        ZipStrip(builder, [o000, o100, o101, o001], [fo0, fo1, fo2, fo3]);

        // Внутренняя поверхность передней стенки (y=wallMm, +Y): кольца CW в XZ.
        ZipStrip(builder, [i001, i101, i100, i000], [io3, io2, io1, io0]);

        // Грани выреза (нормали — внутрь окна).
        builder.Quad(fo3, fo0, io0, io3); // лево (+X)
        builder.Quad(fo0, fo1, io1, io0); // низ (+Z)
        builder.Quad(fo1, fo2, io2, io1); // право (−X)
        builder.Quad(fo2, fo3, io3, io2); // верх (−Z)

        // Внутренние стенки полости и дно.
        builder.Quad(i000, i100, i110, i010); // дно полости (+Z)
        builder.Quad(i000, i010, i011, i001); // лево полости (+X)
        builder.Quad(i100, i101, i111, i110); // право полости (−X)
        builder.Quad(i010, i110, i111, i011); // зад полости (−Y)

        // Верхний обод (z=heightMm, +Z): внешний контур ↔ контур полости.
        ZipStrip(builder, [o001, o101, o111, o011], [i001, i101, i111, i011]);

        return builder.Build();
    }

    /// <summary>
    /// Лента треугольников между двумя кольцами по 4 вершины: оба кольца одного
    /// направления (CCW/CW в своей плоскости), i-я вершина внешнего кольца
    /// соответствует i-й вершина внутреннего; нормаль — по направлению обхода.
    /// </summary>
    private static void ZipStrip(MeshBuilder builder, int[] outer, int[] inner)
    {
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            builder.Tri(outer[i], outer[next], inner[i]);
            builder.Tri(outer[next], inner[next], inner[i]);
        }
    }

    /// <summary>Сборка треугольного меша с общими вершинами по индексу.</summary>
    private sealed class MeshBuilder
    {
        private readonly List<Vector3d> _positions = [];
        private readonly List<int> _indices = [];

        public int AddVertex(Vector3d position)
        {
            _positions.Add(position);
            return _positions.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        public MeshData Build() => new([.. _positions], [.. _indices]);
    }
}
