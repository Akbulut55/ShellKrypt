using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Infrastructure.Services;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
public sealed class DesktopRuntimeServicesTests
{
    [Fact]
    public void VaultSessionController_TracksPathAndClearsKey()
    {
        var state = new AppState();
        var session = new VaultSessionController(state);
        var key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var changes = 0;
        session.StateChanged += (_, _) => changes++;

        session.SetVaultPath(Path.Combine(Path.GetTempPath(), "runtime-test.skvault"));
        session.SetVaultKey(key);

        Assert.True(session.IsUnlocked);
        Assert.Same(key, session.VaultKey);

        session.ClearSensitive();

        Assert.False(session.IsUnlocked);
        Assert.All(key, value => Assert.Equal(0, value));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void DesktopSettingsController_NormalizesPersistsAndAppliesSettings()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.Root);
        var store = new AppSettingsService(new FileAppSettingsStore());
        var localization = new LocalizationService();
        var sessionSecurity = new SessionSecurityService();
        var appearance = new CapturingAppearanceService();
        var controller = new DesktopSettingsController(store, localization, sessionSecurity, appearance);
        var changes = 0;
        controller.Changed += (_, _) => changes++;

        controller.ThemeId = "removed-theme";
        controller.LanguageId = "tr";
        controller.AutoLockMinutes = 30;
        controller.MarkdownAutoSaveSeconds = 0;

        var persisted = store.Load();
        Assert.Equal(AppSettings.DefaultThemeId, controller.ThemeId);
        Assert.Equal("tr", persisted.LanguageId);
        Assert.Equal(30, persisted.AutoLockMinutes);
        Assert.Equal(1, persisted.MarkdownAutoSaveSeconds);
        Assert.Equal(30, sessionSecurity.Settings.AutoLockMinutes);
        Assert.Equal("tr", localization.CurrentLanguageId);
        Assert.Equal("tr", appearance.LastLanguageId);
        Assert.Equal(3, changes);
    }

    private sealed class CapturingAppearanceService : IDesktopAppearanceService
    {
        public string LastThemeId { get; private set; } = "";
        public string LastLanguageId { get; private set; } = "";

        public void ApplyTheme(string themeId) => LastThemeId = themeId;
        public void ApplyLocalization(LocalizationService localization) => LastLanguageId = localization.CurrentLanguageId;
    }

    private sealed class AppRootScope : IDisposable
    {
        private readonly string? _previous;

        public AppRootScope(string root)
        {
            _previous = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, root);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, _previous);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.Runtime.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
