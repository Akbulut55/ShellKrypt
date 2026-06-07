using Avalonia.Media;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.UI.Shared.Theming;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
public sealed class ThemeRegistryTests
{
    [Fact]
    public void ThemeRegistry_DefinesConsistentPalettes()
    {
        var themes = ShellKryptThemePalettes.All;
        var referenceKeys = ShellKryptThemePalettes.Default.Palette.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(5, themes.Count);
        Assert.Equal(themes.Count, themes.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            AppSettings.KnownThemeIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            themes.Select(theme => theme.Id).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        foreach (var theme in themes)
        {
            Assert.False(string.IsNullOrWhiteSpace(theme.Id));
            Assert.False(string.IsNullOrWhiteSpace(theme.DisplayName));
            Assert.Equal(referenceKeys, theme.Palette.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());

            foreach (var color in theme.Palette.Values)
                Color.Parse(color);
        }
    }

    [Fact]
    public void SettingsViewModel_ExposesRegisteredThemesAndUpdatesRootThemeId()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var root = new MainWindowViewModel();
        var settings = new SettingsViewModel(root, null!, new VaultRegistryService(new FileVaultRegistryStore()));
        var ocean = Assert.Single(settings.ThemeOptions, option => option.Id == ShellKryptThemePalettes.OceanId);

        Assert.Equal(ShellKryptThemePalettes.All.Count, settings.ThemeOptions.Count);

        settings.SelectThemeCommand.Execute(ocean);

        Assert.Equal(ShellKryptThemePalettes.OceanId, root.ThemeId);
        Assert.Equal("Ocean", settings.ThemeModeLabel);
        Assert.True(ocean.IsSelected);
    }

    private sealed class AppRootScope : IDisposable
    {
        private readonly string? _previousRoot;

        public AppRootScope(string appRoot)
        {
            _previousRoot = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, appRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, _previousRoot);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.ThemeRegistry.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
