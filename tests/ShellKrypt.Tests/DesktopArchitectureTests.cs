using Xunit;
using ShellKrypt.Desktop.Bootstrap;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Tests;

public sealed class DesktopArchitectureTests
{
    [Fact]
    public void UnlockAndReload_RecreateWorkspacesAndDeactivateTransientState()
    {
        var root = DesktopBootstrap.CreateMainWindowViewModel();
        var applicationServices = root.DesktopFeatures;

        root.OnUnlocked(new byte[32]);
        var firstShell = Assert.IsType<ShellViewModel>(root.Current);
        firstShell.Authenticator.Editor.OpenAdd();
        Assert.True(firstShell.Authenticator.Editor.IsOpen);

        root.ReloadShell();

        var secondShell = Assert.IsType<ShellViewModel>(root.Current);
        Assert.NotSame(firstShell, secondShell);
        Assert.False(firstShell.Authenticator.Editor.IsOpen);
        Assert.Same(applicationServices, root.DesktopFeatures);
        root.Lock();
    }

    [Fact]
    public void InfrastructureConstruction_IsConfinedToBootstrap()
    {
        var desktop = DesktopRoot();
        var concretePrefixes = new[] { "new Sqlite", "new FileApp", "new FileVault", "new EncryptedVault", "new VaultPlaintext", "new VaultCsv" };
        var violations = Sources(desktop)
            .Where(path => !IsUnder(path, Path.Combine(desktop, "Bootstrap")))
            .Where(path => concretePrefixes.Any(prefix => File.ReadAllText(path).Contains(prefix, StringComparison.Ordinal)))
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Features_DoNotDependOnMainWindowViewModelOrBootstrapCatalog()
    {
        var desktop = DesktopRoot();
        var features = Path.Combine(desktop, "Features");
        var violations = Sources(features)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("MainWindowViewModel", StringComparison.Ordinal) ||
                       source.Contains("DesktopServiceCatalog", StringComparison.Ordinal);
            })
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void BootstrapCatalog_DoesNotEscapeBootstrapOrFactories()
    {
        var desktop = DesktopRoot();
        var violations = Sources(desktop)
            .Where(path => !IsUnder(path, Path.Combine(desktop, "Bootstrap")))
            .Where(path => File.ReadAllText(path).Contains("DesktopServiceCatalog", StringComparison.Ordinal))
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void HistoricalLifecycleNamespaces_AreAbsent()
    {
        var desktop = DesktopRoot();
        var forbidden = new[] { "BeforeUnlock", "AfterUnlock", "ViewModels.App", "Views.App" };
        var violations = Directory.EnumerateFiles(desktop, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => forbidden.Any(value => path.Contains(value, StringComparison.Ordinal) || File.ReadAllText(path).Contains(value, StringComparison.Ordinal)))
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> Sources(string root)
        => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories);

    private static bool IsUnder(string path, string directory)
        => Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path);

    private static string DesktopRoot()
        => Path.Combine(FindRepositoryRoot(), "src", "ShellKrypt.Desktop");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ShellKrypt.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the ShellKrypt repository root.");
    }
}
