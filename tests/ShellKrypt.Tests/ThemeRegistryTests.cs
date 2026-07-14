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

        Assert.Equal(2, themes.Count);
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
    public void ThemeRegistry_MeetsRequiredContrastRatios()
    {
        foreach (var theme in ShellKryptThemePalettes.All)
        {
            AssertContrast(theme, "TextPrimaryBrush", "CanvasBrush", 4.5);
            AssertContrast(theme, "TextPrimaryBrush", "SurfaceBrush", 4.5);
            AssertContrast(theme, "TextPrimaryBrush", "SurfaceRaisedBrush", 4.5);
            AssertContrast(theme, "TextMutedBrush", "CanvasBrush", 4.5);
            AssertContrast(theme, "TextMutedBrush", "SurfaceBrush", 4.5);
            AssertContrast(theme, "AccentForegroundBrush", "AccentBrush", 4.5);
            AssertContrast(theme, "AccentTextBrush", "CanvasBrush", 4.5);
            AssertContrast(theme, "FocusRingBrush", "InputBackgroundBrush", 3.0);
        }
    }

    private static void AssertContrast(
        ShellKryptThemeDefinition theme,
        string foregroundKey,
        string backgroundKey,
        double minimum)
    {
        var foreground = RelativeLuminance(Color.Parse(theme.Palette[foregroundKey]));
        var background = RelativeLuminance(Color.Parse(theme.Palette[backgroundKey]));
        var ratio = (Math.Max(foreground, background) + 0.05) / (Math.Min(foreground, background) + 0.05);

        Assert.True(
            ratio >= minimum,
            $"{theme.DisplayName}: {foregroundKey} on {backgroundKey} has contrast {ratio:F2}, expected {minimum:F1} or greater.");
    }

    private static double RelativeLuminance(Color color)
        => 0.2126 * Linearize(color.R / 255d) +
           0.7152 * Linearize(color.G / 255d) +
           0.0722 * Linearize(color.B / 255d);

    private static double Linearize(double component)
        => component <= 0.04045 ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);

    [Fact]
    public void SettingsViewModel_ExposesRegisteredThemesAndUpdatesRootThemeId()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var root = new MainWindowViewModel();
        var settings = new SettingsViewModel(root, null!, new VaultRegistryService(new FileVaultRegistryStore()));
        var light = Assert.Single(settings.ThemeOptions, option => option.Id == ShellKryptThemePalettes.LightId);

        Assert.Equal(ShellKryptThemePalettes.All.Count, settings.ThemeOptions.Count);

        settings.SelectedThemeOption = light;

        Assert.Equal(ShellKryptThemePalettes.LightId, root.ThemeId);
        Assert.Equal("Light", settings.ThemeModeLabel);
        Assert.True(light.IsSelected);
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
