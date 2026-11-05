using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// W5: пользовательские точки сборки (Snap) переживают сохранение проекта —
/// позиция, нормаль диска и up хранятся как единичные векторы.
/// </summary>
public sealed class ProjectFileUserSnapTests
{
    private static ProjectDocument DocumentWithSnap(out SnapNode snap)
    {
        var scene = new Scene();
        var group = new GroupNode
        {
            Name = "Стол",
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(10, 0, 0)),
        };
        Assert.True(SnapFrame.TryCreate(
            new Vector3d(1.5, -2, 3),
            new Vector3d(0, 1, 1),
            new Vector3d(1, 0, 0),
            out SnapFrame frame));
        snap = new SnapNode { Name = "Snap столешницы", Frame = frame };
        group.AddChild(snap);
        scene.Root.AddChild(group);
        return new ProjectDocument { Scene = scene };
    }

    [Fact]
    public void SaveLoad_UserSnap_PreservesFrameAndPlacement()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = DocumentWithSnap(out SnapNode expectedSnap);
        string path = temp.FilePath("snap" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        var group = Assert.IsType<GroupNode>(actual.Scene.Root.Children[0]);
        var loaded = Assert.IsType<SnapNode>(Assert.Single(group.Children));

        Assert.Equal(expectedSnap.Name, loaded.Name);
        Assert.Equal(expectedSnap.Frame.Position.X, loaded.Frame.Position.X, 12);
        Assert.Equal(expectedSnap.Frame.Position.Y, loaded.Frame.Position.Y, 12);
        Assert.Equal(expectedSnap.Frame.Position.Z, loaded.Frame.Position.Z, 12);
        Assert.Equal(expectedSnap.Frame.Direction.X, loaded.Frame.Direction.X, 12);
        Assert.Equal(expectedSnap.Frame.Direction.Y, loaded.Frame.Direction.Y, 12);
        Assert.Equal(expectedSnap.Frame.Direction.Z, loaded.Frame.Direction.Z, 12);
        Assert.Equal(expectedSnap.Frame.Up.X, loaded.Frame.Up.X, 12);
        Assert.Equal(expectedSnap.Frame.Up.Y, loaded.Frame.Up.Y, 12);
        Assert.Equal(expectedSnap.Frame.Up.Z, loaded.Frame.Up.Z, 12);
        Assert.Equal(1, loaded.Frame.Direction.Length(), 12);
        Assert.Equal(0, Vector3d.Dot(loaded.Frame.Direction, loaded.Frame.Up), 12);
    }

    [Fact]
    public void SaveLoad_UserSnap_KeepsNodeId()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = DocumentWithSnap(out SnapNode expectedSnap);
        string path = temp.FilePath("snap" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        var group = Assert.IsType<GroupNode>(actual.Scene.Root.Children[0]);
        Assert.Equal(expectedSnap.Id, Assert.Single(group.Children).Id);
    }
}
