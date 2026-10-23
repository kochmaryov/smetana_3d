using SmetaNA.Core.Geometry;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.PrintVolume;
using SmetaNA.Rendering.PrintVolume;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Модуль зоны печати в UI (задача 4.5): выбор профиля принтера, свойства
/// профиля (габариты, сопло, высота слоя), переключатель показа зоны и
/// локализация RU/EN строк модуля.
/// </summary>
public sealed class PrintVolumeModuleTests
{
    /// <summary>Заглушка сервиса локализации со сменяемым языком.</summary>
    private sealed class StubLocalization : ILocalizationService
    {
        public EditorCulture Culture { get; private set; } = EditorCulture.Russian;

        public event Action? CultureChanged;

        public string Get(string key) => Get(key, Culture);

        public string Get(string key, EditorCulture culture) => PrintVolumeStrings.Get(key, culture);

        public void SetCulture(EditorCulture culture)
        {
            if (Culture == culture)
            {
                return;
            }

            Culture = culture;
            CultureChanged?.Invoke();
        }
    }

    private static void ResetState()
    {
        PrintVolumeState.Current = null;
    }

    [Fact]
    public void Register_ExposesCatalogProfiles()
    {
        Assert.Equal(PrinterProfileCatalog.All, RegisterPrintVolume.Profiles);
        Assert.True(RegisterPrintVolume.Profiles.Count >= 3);
    }

    [Fact]
    public void ViewModel_ShowsProfileProperties()
    {
        ResetState();
        try
        {
            var viewModel = new PrintVolumeViewModel(new StubLocalization());

            Assert.Equal(PrinterProfileCatalog.All.Count, viewModel.Profiles.Count);
            Assert.Same(PrinterProfileCatalog.Default, viewModel.SelectedProfile!.Profile);
            Assert.False(viewModel.IsVolumeVisible);
            Assert.Equal("Prusa Research i3 MK3S+", viewModel.ProfileName);
            Assert.Contains("250", viewModel.VolumeText);
            Assert.Contains("210", viewModel.VolumeText);
            Assert.Contains("0,4", viewModel.NozzleText);
            Assert.Contains("0,05", viewModel.LayerText);
            Assert.Contains("0,35", viewModel.LayerText);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.NotesText));
        }
        finally
        {
            ResetState();
        }
    }

    [Fact]
    public void ViewModel_TogglesPrintVolumeState()
    {
        ResetState();
        try
        {
            var viewModel = new PrintVolumeViewModel(new StubLocalization());
            Assert.Null(PrintVolumeState.Current);

            viewModel.IsVolumeVisible = true;
            Assert.Same(PrinterProfileCatalog.Default, PrintVolumeState.Current);

            viewModel.SelectedProfile = viewModel.Profiles[2];
            Assert.Same(PrinterProfileCatalog.All[2], PrintVolumeState.Current);

            viewModel.IsVolumeVisible = false;
            Assert.Null(PrintVolumeState.Current);
        }
        finally
        {
            ResetState();
        }
    }

    [Fact]
    public void Register_ApplyAndClear_ReachViewportState()
    {
        ResetState();
        try
        {
            PrinterProfile profile = PrinterProfileCatalog.All[1];
            RegisterPrintVolume.Apply(profile);
            Assert.Same(profile, PrintVolumeState.Current);

            RegisterPrintVolume.Clear();
            Assert.Null(PrintVolumeState.Current);
        }
        finally
        {
            ResetState();
        }
    }

    [Fact]
    public void ViewModel_KeepsProfileOnLanguageSwitch()
    {
        ResetState();
        try
        {
            var localization = new StubLocalization();
            var viewModel = new PrintVolumeViewModel(localization);
            viewModel.SelectedProfile = viewModel.Profiles[1];
            viewModel.IsVolumeVisible = true;

            localization.SetCulture(EditorCulture.English);

            Assert.Same(PrinterProfileCatalog.All[1], viewModel.SelectedProfile!.Profile);
            Assert.Same(PrinterProfileCatalog.All[1], PrintVolumeState.Current);
            Assert.Equal("Creality Ender-3", viewModel.ProfileName);
            Assert.Contains("0.4", viewModel.NozzleText);
            Assert.Equal("Print Volume", viewModel.Title);
        }
        finally
        {
            ResetState();
        }
    }

    [Fact]
    public void ModuleStrings_AreLocalizedInBothCultures()
    {
        string[] keys =
        [
            PrintVolumeStrings.Title,
            PrintVolumeStrings.ProfileLabel,
            PrintVolumeStrings.ShowVolume,
            PrintVolumeStrings.VolumeLabel,
            PrintVolumeStrings.NozzleLabel,
            PrintVolumeStrings.LayerLabel,
            PrintVolumeStrings.NotesLabel,
            PrintVolumeStrings.MillimeterUnit,
        ];

        foreach (string key in keys)
        {
            string russian = PrintVolumeStrings.Get(key, EditorCulture.Russian);
            string english = PrintVolumeStrings.Get(key, EditorCulture.English);
            Assert.False(string.IsNullOrWhiteSpace(russian), key);
            Assert.False(string.IsNullOrWhiteSpace(english), key);
            Assert.NotEqual(russian, english);
            Assert.NotEqual(key, russian);
            Assert.NotEqual(key, english);
        }
    }

    [Fact]
    public void ProfileOption_SummaryUsesCultureUnit()
    {
        var option = new PrinterProfileOption(PrinterProfileCatalog.Default, EditorCulture.Russian);
        Assert.Contains("мм", option.Summary);

        option.SetCulture(EditorCulture.English);
        Assert.Contains("mm", option.Summary);
    }
}
