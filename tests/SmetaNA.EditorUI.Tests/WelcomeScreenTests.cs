using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>Стартовый экран и открытие файла проекта через view-модель главного окна.</summary>
public sealed class WelcomeScreenTests : IDisposable
{
    private readonly string _recentPath =
        Path.Combine(Path.GetTempPath(), $"recent-{Guid.NewGuid():N}.json");

    public WelcomeScreenTests()
    {
        // изоляция от глобального recent.json машины
        RecentFilesStore.FilePath = _recentPath;
    }

    public void Dispose()
    {
        RecentFilesStore.FilePath = RecentFilesStore.DefaultFilePath;
        if (File.Exists(_recentPath))
        {
            File.Delete(_recentPath);
        }
    }

    [Fact]
    public void Startup_WithoutSmoke_HasNoProjects_WelcomeVisible()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.HasProjects);
        Assert.False(vm.HasRecentFiles);
        Assert.False(vm.IsRecentListOpen);
    }

    [Fact]
    public void NewProject_FromWelcome_CreatesFirstProject()
    {
        var vm = CreateViewModel(out _);

        vm.NewProjectCommand.Execute(null);

        Assert.True(vm.HasProjects);
        Assert.Single(vm.Projects);
    }

    [Fact]
    public void CreateProjectWithUnit_SetsSceneUnitBeforeAddingProject()
    {
        var vm = CreateViewModel(out _);

        vm.CreateProjectWithUnitCommand.Execute("Centimeter");

        Assert.True(vm.HasProjects);
        Assert.Equal(LengthUnit.Centimeter, vm.SceneUnit);
    }

    [Fact]
    public void OpenProjectFile_ReplacesSceneClearsHistory_AddsToRecent()
    {
        var vm = CreateViewModel(out EditorSession session);
        string path = WriteTempProject("открытый");

        try
        {
            vm.OpenProjectFile(path);

            Assert.True(vm.HasProjects);
            Assert.True(vm.HasRecentFiles);
            Assert.Contains(vm.RecentFiles, item => item.FullPath == path);
            // содержимое заменено: узел из файла вместо пустой сцены
            Assert.Equal(2, session.Scene.EnumerateNodes().Count());
            // новая загрузка — пустая история
            Assert.Empty(vm.HistoryEntries);
            Assert.False(vm.UndoCommand.CanExecute(null));
            Assert.Contains(Path.GetFileName(path), vm.StatusText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenProjectFile_MissingFile_ReportsError_KeepsState()
    {
        var vm = CreateViewModel(out _);
        string missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.sna");

        vm.OpenProjectFile(missing);

        Assert.False(vm.HasProjects);
        Assert.False(vm.HasRecentFiles);
        Assert.Contains("Не удалось", vm.StatusText, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(out EditorSession session)
    {
        var localization = new LocalizationService();
        var theme = new ThemeService();
        session = new EditorSession();
        return new MainWindowViewModel(localization, theme, session);
    }

    private static string WriteTempProject(string nodeName)
    {
        var mesh = new HalfEdgeMesh();
        var scene = new Scene();
        scene.Root.AddChild(new MeshNode { Mesh = mesh, Name = nodeName });
        string path = Path.Combine(Path.GetTempPath(), $"welcome-{Guid.NewGuid():N}.sna");
        ProjectFileStore.Save(path, new ProjectDocument { Scene = scene });
        return path;
    }
}
