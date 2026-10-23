using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Компоненты (задача 3.4): наследование правок мастер-компоненты всеми инстансами,
/// включая вложенные (DoD §5.5, сценарий S5), Make Unique, инстанс против копии.
/// </summary>
public sealed class ComponentInheritanceTests
{
    [Fact]
    public void MasterEdit_PropagatesToAllInstances_IncludingNested()
    {
        // Мастер B: меш «Head».
        var bolt = ModelTestSupport.Mesh("Head");
        var defB = ModelTestSupport.Definition("Bolt", "BoltBody", bolt);

        // Мастер A: вложенный инстанс B + плита.
        var nestedB = ModelTestSupport.Instance(defB, "Bolt#1", 5, 0, 0);
        var defA = ModelTestSupport.Definition("Assembly", "AssemblyBody", nestedB, ModelTestSupport.Mesh("Plate"));

        // Сцена: два инстанса A и прямой инстанс B.
        var scene = new Scene();
        scene.Root.AddChild(ModelTestSupport.Instance(defA, "A1"));
        scene.Root.AddChild(ModelTestSupport.Instance(defA, "A2", 0, 10, 0));
        scene.Root.AddChild(ModelTestSupport.Instance(defB, "B0", 0, 0, 20));
        var stack = new UndoStack();

        // Правка мастера B: сдвиг меша.
        stack.Push(new SetNodeTransformCommand(bolt, Transform3d.CreateTranslation(new Vector3d(0, 0, 3))));

        IReadOnlyList<Vector3d> points = ModelTestSupport.WorldPointsOf(scene, bolt);
        Assert.Equal(3, points.Count);
        Assert.Contains(new Vector3d(5, 0, 3), points);   // через A1 → вложенный B
        Assert.Contains(new Vector3d(5, 10, 3), points);  // через A2 → вложенный B
        Assert.Contains(new Vector3d(0, 0, 23), points);  // прямой инстанс B0

        // Правка имени мастера тоже видна всем.
        stack.Push(new RenameNodeCommand(bolt, "HeadRenamed"));
        Assert.Equal(3, OutlinerQuery.FindByName(scene, "HeadRenamed").Count);

        // Undo раскручивает обе правки для всех инстансов.
        stack.Undo();
        Assert.Equal(3, OutlinerQuery.FindByName(scene, "Head").Count);
        stack.Undo();
        Assert.Contains(new Vector3d(5, 0, 0), ModelTestSupport.WorldPointsOf(scene, bolt));
        Assert.Contains(new Vector3d(0, 0, 20), ModelTestSupport.WorldPointsOf(scene, bolt));

        // Redo повторяет для всех.
        stack.Redo();
        stack.Redo();
        Assert.Contains(new Vector3d(5, 10, 3), ModelTestSupport.WorldPointsOf(scene, bolt));
        Assert.Equal(3, OutlinerQuery.FindByName(scene, "HeadRenamed").Count);
    }

    [Fact]
    public void Instances_ShareDefinition_MasterEditAffectsBoth()
    {
        var mesh = ModelTestSupport.Mesh("M");
        var definition = ModelTestSupport.Definition("D", "DBody", mesh);
        var i1 = ModelTestSupport.Instance(definition, "I1");
        var i2 = ModelTestSupport.Instance(definition, "I2", 0, 10, 0);
        Assert.Same(i1.Definition, i2.Definition);

        var scene = new Scene();
        scene.Root.AddChild(i1);
        scene.Root.AddChild(i2);
        var stack = new UndoStack();

        stack.Push(new SetNodeTransformCommand(mesh, Transform3d.CreateTranslation(new Vector3d(0, 0, 4))));

        Assert.Contains(new Vector3d(0, 0, 4), ModelTestSupport.WorldPointsOf(scene, mesh));
        Assert.Contains(new Vector3d(0, 10, 4), ModelTestSupport.WorldPointsOf(scene, mesh));
    }

    [Fact]
    public void IndependentCopy_DoesNotInheritMasterEdits()
    {
        var mesh = ModelTestSupport.Mesh("M");
        var definition = ModelTestSupport.Definition("D", "DBody", mesh);
        var instance = ModelTestSupport.Instance(definition, "I1", 1, 0, 0);
        var scene = new Scene();
        scene.Root.AddChild(instance);
        var copy = ComponentFactory.CreateIndependentCopy(instance);
        scene.Root.AddChild(copy);
        Assert.NotSame(definition, copy.Definition);
        var stack = new UndoStack();

        stack.Push(new SetNodeTransformCommand(mesh, Transform3d.CreateTranslation(new Vector3d(0, 0, 4))));

        // Инстанс унаследовал правку, копия — нет.
        Assert.Contains(new Vector3d(1, 0, 4), ModelTestSupport.WorldPointsOf(scene, mesh));
        var copyMesh = Assert.IsType<MeshNode>(copy.Definition.Content.Children[0]);
        Assert.Equal("M", copyMesh.Name);
        Assert.Contains(new Vector3d(1, 0, 0), ModelTestSupport.WorldPointsOf(scene, copyMesh));
    }

    [Fact]
    public void MakeUnique_BreaksInheritance_ForSingleInstanceOnly()
    {
        var mesh = ModelTestSupport.Mesh("M");
        var definition = ModelTestSupport.Definition("D", "DBody", mesh);
        var i1 = ModelTestSupport.Instance(definition, "I1", 1, 0, 0);
        var i2 = ModelTestSupport.Instance(definition, "I2", 2, 0, 0);
        var scene = new Scene();
        scene.Root.AddChild(i1);
        scene.Root.AddChild(i2);
        var extra = ModelTestSupport.Group("Extra");
        i1.AddChild(extra);
        var stack = new UndoStack();
        var command = new MakeUniqueInstanceCommand(i1);

        stack.Push(command);

        ComponentInstanceNode unique = Assert.IsType<ComponentInstanceNode>(scene.Root.Children[0]);
        Assert.Same(unique, command.UniqueInstance);
        Assert.NotSame(definition, unique.Definition);
        Assert.Equal("I1", unique.Name);
        Assert.Equal([extra], unique.Children);

        // Правка прежнего мастера видна только оставшемуся инстансу.
        stack.Push(new RenameNodeCommand(mesh, "Changed"));
        Assert.Single(OutlinerQuery.FindByName(scene, "Changed"));
        Assert.Single(OutlinerQuery.FindByName(scene, "M"));

        // Undo make unique возвращает наследование.
        stack.Undo();
        stack.Undo();
        Assert.Same(i1, scene.Root.Children[0]);
        Assert.Equal(2, ModelTestSupport.WorldPointsOf(scene, mesh).Count);

        // Redo снова разрывает связь, уникальный инстанс тот же самый.
        stack.Redo();
        Assert.Same(unique, scene.Root.Children[0]);
    }

    [Fact]
    public void MakeUnique_KeepsNestedInstancesShared()
    {
        var bolt = ModelTestSupport.Mesh("Bolt");
        var defB = ModelTestSupport.Definition("Bolt", "BoltBody", bolt);
        var nestedB = ModelTestSupport.Instance(defB, "Bolt#1", 5, 0, 0);
        var defA = ModelTestSupport.Definition("Assembly", "AssemblyBody", nestedB);
        var a1 = ModelTestSupport.Instance(defA, "A1");
        var a2 = ModelTestSupport.Instance(defA, "A2", 0, 10, 0);
        var scene = new Scene();
        scene.Root.AddChild(a1);
        scene.Root.AddChild(a2);
        var stack = new UndoStack();

        stack.Push(new MakeUniqueInstanceCommand(a1));

        ComponentInstanceNode unique = Assert.IsType<ComponentInstanceNode>(scene.Root.Children[0]);
        var clonedNested = Assert.IsType<ComponentInstanceNode>(unique.Definition.Content.Children[0]);
        Assert.Same(defB, clonedNested.Definition);

        // Вложенный мастер B по-прежнему общий: правка видна и в уникальной копии, и в A2.
        stack.Push(new RenameNodeCommand(bolt, "Renamed"));
        Assert.Equal(2, OutlinerQuery.FindByName(scene, "Renamed").Count);

        // А мастер A разошёлся: правка вложенного инстанса не влияет на уникальную копию.
        stack.Push(new RenameNodeCommand(nestedB, "NestedEdit"));
        Assert.Single(OutlinerQuery.FindByName(scene, "NestedEdit"));
    }

    [Fact]
    public void MakeUnique_PreservesInstanceState()
    {
        var definition = ModelTestSupport.Definition("D", "DBody", ModelTestSupport.Mesh("M"));
        var tag = new Tag { Name = "Тег" };
        var instance = new ComponentInstanceNode
        {
            Definition = definition,
            Name = "I1",
            IsVisible = false,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(1, 2, 3)),
            Tag = tag,
        };
        var scene = new Scene();
        scene.Root.AddChild(instance);
        var child = ModelTestSupport.Group("Child");
        instance.AddChild(child);
        var stack = new UndoStack();

        stack.Push(new MakeUniqueInstanceCommand(instance));

        ComponentInstanceNode unique = Assert.IsType<ComponentInstanceNode>(scene.Root.Children[0]);
        Assert.Equal("I1", unique.Name);
        Assert.False(unique.IsVisible);
        Assert.Same(tag, unique.Tag);
        Assert.Equal(new Vector3d(1, 2, 3), unique.LocalTransform.TransformPoint(new Vector3d(0, 0, 0)));
        Assert.Same(unique, child.Parent);
    }

    [Fact]
    public void ComponentFactory_CreateInstance_SharesDefinition()
    {
        var definition = ModelTestSupport.Definition("D", "DBody", ModelTestSupport.Mesh("M"));

        var i1 = ComponentFactory.CreateInstance(definition);
        var i2 = ComponentFactory.CreateInstance(definition, "Копия");

        Assert.Same(definition, i1.Definition);
        Assert.Same(definition, i2.Definition);
        Assert.Equal("D", i1.Name);
        Assert.Equal("Копия", i2.Name);
    }
}
