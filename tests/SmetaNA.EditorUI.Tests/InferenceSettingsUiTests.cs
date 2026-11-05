using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Settings;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// W4: настройки привязок вкладки (скрытая геометрия, сетка, подписи) и их
/// сохранение в каталог настроек приложения.
/// </summary>
public sealed class InferenceSettingsUiTests
{
    private static ProjectTabViewModel CreateViewModel(Scene? scene = null) =>
        new(new LocalizationService(), scene ?? new Scene(), 1);

    [Fact]
    public void Defaults_MatchSpec()
    {
        ProjectTabViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.ShowHiddenGeometry);
        Assert.False(viewModel.GridSnapping);
        Assert.True(viewModel.ShowSnapLabels);
        Assert.False(viewModel.IsHiddenGeometryPreview);
    }

    [Fact]
    public void Toggles_PushSettingsIntoContext_AndNotify()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        int changes = 0;
        viewModel.InferenceSettingsChanged += () => changes++;

        viewModel.ShowHiddenGeometry = true;
        viewModel.GridSnapping = true;
        viewModel.ShowSnapLabels = false;

        Assert.Equal(3, changes);
        Assert.True(viewModel.Tools.Context.Inference.ShowHiddenGeometry);
        Assert.True(viewModel.Tools.Context.Inference.GridEnabled);
        Assert.False(viewModel.Tools.Context.Inference.ShowLabels);
        Assert.True(viewModel.Tools.Context.IncludeHiddenGeometry);
    }

    [Fact]
    public void ApplyInferenceSettings_SetsFieldsAndContext()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        var settings = new InferenceSettings
        {
            ShowHiddenGeometry = true,
            GridEnabled = true,
            GridStep = 25,
            ShowLabels = false,
            TolerancePx = 14,
        };

        viewModel.ApplyInferenceSettings(settings);

        Assert.True(viewModel.ShowHiddenGeometry);
        Assert.True(viewModel.GridSnapping);
        Assert.False(viewModel.ShowSnapLabels);
        Assert.Equal(25, viewModel.InferenceSettings.GridStep);
        Assert.Equal(14, viewModel.Tools.Context.Inference.TolerancePx);
        Assert.True(viewModel.Tools.Context.Inference.GridEnabled);
    }

    [Fact]
    public void Store_RoundTripsSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "smetana-inference-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, InferenceSettingsStore.FileName);
        try
        {
            var store = new InferenceSettingsStore(path);
            var settings = new InferenceSettings
            {
                GridEnabled = true,
                GridStep = 5,
                LengthSnappingEnabled = true,
                LengthPrecision = 0.05,
                ShowHiddenGeometry = true,
                ShowLabels = false,
                TolerancePx = 11,
                Categories = SnapFilter.Endpoints | SnapFilter.Midpoints,
            };

            store.Save(settings);
            InferenceSettings loaded = store.Load();

            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Store_MissingOrBrokenFile_ReturnsDefaults()
    {
        string directory = Path.Combine(Path.GetTempPath(), "smetana-inference-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, InferenceSettingsStore.FileName);
        try
        {
            var store = new InferenceSettingsStore(path);
            Assert.Equal(InferenceSettings.Default, store.Load());

            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ это не json");
            Assert.Equal(InferenceSettings.Default, store.Load());

            File.WriteAllText(path, "{\"version\": 99, \"tolerancePx\": 5}");
            Assert.Equal(InferenceSettings.Default, store.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LengthSnapping_TogglesIntoSettings()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        Assert.False(viewModel.LengthSnapping);

        viewModel.LengthSnapping = true;

        Assert.True(viewModel.InferenceSettings.LengthSnappingEnabled);
        Assert.True(viewModel.Tools.Context.Inference.LengthSnappingEnabled);
    }

    [Fact]
    public void GridStep_CyclesThroughPresets_AndLabelShowsValue()
    {
        ProjectTabViewModel viewModel = CreateViewModel();
        Assert.Equal(InferenceSettings.DefaultGridStep, viewModel.GridStep);
        Assert.Contains("10", viewModel.GridStepMenuLabel);

        viewModel.CycleGridStepCommand.Execute(null);
        Assert.Equal(100, viewModel.GridStep);
        Assert.Equal(100, viewModel.Tools.Context.Inference.GridStep);

        viewModel.CycleGridStepCommand.Execute(null);
        Assert.Equal(0.1, viewModel.GridStep);

        viewModel.SetGridStep(1);
        Assert.Equal(1, viewModel.InferenceSettings.GridStep);
        Assert.Equal([0.1, 1, 10, 100], ProjectTabViewModel.GridStepPresets);
    }

    [Fact]
    public void MainWindow_AppliesStoredSettingsToNewProject()
    {
        string directory = Path.Combine(Path.GetTempPath(), "smetana-inference-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, InferenceSettingsStore.FileName);
        try
        {
            var store = new InferenceSettingsStore(path);
            store.Save(new InferenceSettings { GridEnabled = true, GridStep = 50, ShowHiddenGeometry = true });

            var viewModel = new MainWindowViewModel(
                new LocalizationService(),
                new SmetaNA.EditorUI.Themes.ThemeService(),
                new SmetaNA.EditorUI.Sessions.EditorSession(),
                store);
            viewModel.NewProjectCommand.Execute(null);

            ProjectTabViewModel project = Assert.IsType<ProjectTabViewModel>(viewModel.SelectedProject);
            Assert.True(project.GridSnapping);
            Assert.True(project.ShowHiddenGeometry);
            Assert.Equal(50, project.Tools.Context.Inference.GridStep);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LocaleKeys_ForW4Toggles_AreTranslated()
    {
        var localization = new LocalizationService();

        foreach (string key in new[]
        {
            LocalizationKeys.ViewShowHiddenGeometry,
            LocalizationKeys.ViewGridSnapping,
            LocalizationKeys.ViewSnapLabels,
            LocalizationKeys.SnapHiddenGeometryOn,
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(localization.Get(key, EditorCulture.Russian)));
            Assert.False(string.IsNullOrWhiteSpace(localization.Get(key, EditorCulture.English)));
        }
    }
}
