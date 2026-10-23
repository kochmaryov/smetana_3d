using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Команда истории: равномерное масштабирование модели (задача 3.2) — мировая
/// геометрия пересчитывается с сохранением пропорций, точка
/// <see cref="Anchor"/> остаётся неподвижной.
/// <para>
/// Масштаб применяется к локальным трансформациям прямых детей корня
/// (<c>L' = L·S</c>): мировая трансформация каждого узла поддерева становится
/// <c>W' = W·S</c> — включая содержимое определений компонентов, общее для
/// инстансов (геометрия вершин при этом не дублируется и не расходится).
/// Undo возвращает исходные трансформации.
/// </para>
/// </summary>
public sealed class ScaleModelCommand : IEditCommand
{
    private readonly Scene _scene;
    private readonly List<(SceneNode Node, Transform3d OldTransform)> _oldTransforms = [];

    public ScaleModelCommand(Scene scene, Vector3d anchor, double factor)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Множитель масштаба должен быть конечным и положительным.");
        }

        _scene = scene;
        Anchor = anchor;
        Factor = factor;
        Description = string.Format(CultureInfo.InvariantCulture, "Масштабирование модели ×{0:0.####}", factor);
    }

    public string Description { get; }

    /// <summary>Неподвижная точка масштабирования (мировые координаты, мм).</summary>
    public Vector3d Anchor { get; }

    /// <summary>Множитель масштаба (единый для всех осей).</summary>
    public double Factor { get; }

    public void Execute()
    {
        if (_oldTransforms.Count == 0)
        {
            foreach (SceneNode child in _scene.Root.Children)
            {
                _oldTransforms.Add((child, child.LocalTransform));
            }
        }

        Transform3d scale = CreateScale(Anchor, Factor);
        foreach ((SceneNode node, Transform3d oldTransform) in _oldTransforms)
        {
            node.LocalTransform = Transform3d.Multiply(oldTransform, scale);
        }
    }

    public void Undo()
    {
        if (_oldTransforms.Count == 0)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        foreach ((SceneNode node, Transform3d oldTransform) in _oldTransforms)
        {
            node.LocalTransform = oldTransform;
        }
    }

    /// <summary>
    /// Равномерное масштабирование мира вокруг точки: (p − anchor)·factor + anchor.
    /// </summary>
    public static Transform3d CreateScale(Vector3d anchor, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Множитель масштаба должен быть конечным и положительным.");
        }

        return new Transform3d(
            factor, 0, 0, 0,
            0, factor, 0, 0,
            0, 0, factor, 0,
            anchor.X * (1 - factor), anchor.Y * (1 - factor), anchor.Z * (1 - factor), 1);
    }
}
