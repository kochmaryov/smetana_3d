namespace SmetaNA.Core.Model;

/// <summary>Корень сцены редактора: иерархия объектов и единицы проекта.</summary>
public sealed class Scene
{
    public GroupNode Root { get; } = new() { Name = "Root" };
    public LengthUnit Unit { get; set; } = LengthUnit.Millimeter;

    /// <summary>Обход всех узлов сцены в порядке иерархии.</summary>
    public IEnumerable<SceneNode> EnumerateNodes()
    {
        var stack = new Stack<SceneNode>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            SceneNode node = stack.Pop();
            yield return node;
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }
}
