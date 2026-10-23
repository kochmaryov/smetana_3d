using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Editing;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Математика массивов (задача 5.1): шаги линейного (смещение) и кругового
/// (поворот вокруг оси) массивов и построение копий узла с готовыми
/// трансформациями.
/// <para>
/// Копии — «копии узлов» (<see cref="M6NodeCloner.DeepCopy"/>): новый меш для
/// обычных объектов, инстансы компонентов копируются как инстансы того же
/// определения (правка мастера распространяется на весь массив). Копии сразу
/// получают итоговую локальную трансформацию — в историю попадает одна
/// составная команда добавления (одна отмена на весь массив).
/// </para>
/// </summary>
public static class ArrayMath
{
    /// <summary>Копий по умолчанию (без учёта исходного объекта).</summary>
    public const int DefaultCount = 3;

    /// <summary>Максимум копий в массиве (защита от случайно огромного ввода).</summary>
    public const int MaxCount = 1000;

    /// <summary>Минимальный осмысленный шаг (смещение), мм.</summary>
    public const double MinStepMm = 1e-6;

    /// <summary>Минимальный осмысленный шаг (угол), градусы.</summary>
    public const double MinStepDegrees = 1e-6;

    /// <summary>Число копий из значения VCB: округление до целого в диапазоне 1..<see cref="MaxCount"/>.</summary>
    public static int NormalizeCount(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Число копий должно быть конечным.");
        }

        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 1, MaxCount);
    }

    /// <summary>Мировые преобразования копий линейного массива: смещение × 1..count.</summary>
    public static IReadOnlyList<Transform3d> LinearSteps(Vector3d step, int count)
    {
        ValidateCount(count);
        double length = step.Length();
        if (!double.IsFinite(length) || length < MinStepMm)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Шаг массива слишком мал.");
        }

        var steps = new List<Transform3d>(count);
        for (int k = 1; k <= count; k++)
        {
            steps.Add(Transform3d.CreateTranslation(step * k));
        }

        return steps;
    }

    /// <summary>Мировые преобразования копий кругового массива: поворот × 1..count.</summary>
    public static IReadOnlyList<Transform3d> PolarSteps(Vector3d axis, Vector3d pivot, double stepDegrees, int count)
    {
        ValidateCount(count);
        if (!double.IsFinite(stepDegrees) || Math.Abs(stepDegrees) < MinStepDegrees)
        {
            throw new ArgumentOutOfRangeException(nameof(stepDegrees), stepDegrees, "Шаг угла массива слишком мал.");
        }

        Vector3d direction = axis.Normalized();
        if (direction.LengthSquared() <= 0)
        {
            throw new ArgumentException("Ось массива вырождена.", nameof(axis));
        }

        var steps = new List<Transform3d>(count);
        for (int k = 1; k <= count; k++)
        {
            steps.Add(EditingTransforms.CreateRotation(direction, pivot, EditingMath.ToRadians(stepDegrees * k)));
        }

        return steps;
    }

    /// <summary>
    /// Копии узла с итоговыми локальными трансформациями: копия k занимает
    /// мир <c>W·D_k</c> (W — текущий мир исходного узла, D_k — шаг k).
    /// </summary>
    public static IReadOnlyList<SceneNode> BuildCopies(SceneNode subject, IReadOnlyList<Transform3d> worldSteps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(worldSteps);
        SceneNode parent = subject.Parent
            ?? throw new InvalidOperationException("Массив строится только для узла, входящего в иерархию сцены.");

        Transform3d nodeWorld = M6Transforms.WorldTransformOf(subject);
        Transform3d parentWorld = M6Transforms.WorldTransformOf(parent);
        var copies = new List<SceneNode>(worldSteps.Count);
        foreach (Transform3d step in worldSteps)
        {
            SceneNode copy = M6NodeCloner.DeepCopy(subject);
            if (!M6Transforms.TryComposeCopyTransform(nodeWorld, parentWorld, step, out Transform3d newLocal))
            {
                throw new InvalidOperationException("Трансформация родителя необратима — построить массив невозможно.");
            }

            copy.LocalTransform = newLocal;
            copies.Add(copy);
        }

        return copies;
    }

    private static void ValidateCount(int count)
    {
        if (count < 1 || count > MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Число копий должно быть от 1 до {MaxCount}.");
        }
    }
}
