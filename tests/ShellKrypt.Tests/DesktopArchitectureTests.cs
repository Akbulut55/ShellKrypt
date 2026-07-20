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
        var applicationServices = root.Session;

        root.Navigation.OnUnlocked(new byte[32]);
        var firstShell = Assert.IsType<ShellViewModel>(root.Current);
        firstShell.Authenticator.Editor.OpenAdd();
        Assert.True(firstShell.Authenticator.Editor.IsOpen);

        root.Navigation.ReloadShell();

        var secondShell = Assert.IsType<ShellViewModel>(root.Current);
        Assert.NotSame(firstShell, secondShell);
        Assert.False(firstShell.Authenticator.Editor.IsOpen);
        Assert.Same(applicationServices, root.Session);
        root.Navigation.Lock();
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
                       source.Contains("DesktopServiceCatalog", StringComparison.Ordinal) ||
                       source.Contains("DesktopFeatureServices", StringComparison.Ordinal) ||
                       source.Contains("ShellKrypt.Infrastructure", StringComparison.Ordinal);
            })
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void RootViewModels_DoNotConstructServicesOrWorkspaces()
    {
        var desktop = DesktopRoot();
        var files = new[]
        {
            Path.Combine(desktop, "Shell", "MainWindowViewModel.cs"),
            Path.Combine(desktop, "Shell", "ShellViewModel.cs")
        };
        var forbidden = new[] { "DesktopServiceCatalog", "new Sqlite", "new File", "new ShellWorkspaces", "new WebLoginsViewModel", "new SettingsViewModel" };
        var violations = files
            .Where(File.Exists)
            .Where(path => forbidden.Any(value => File.ReadAllText(path).Contains(value, StringComparison.Ordinal)))
            .Select(path => Relative(desktop, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void RemovedBroadFeatureServiceCatalog_RemainsAbsent()
    {
        var desktop = DesktopRoot();
        var violations = Sources(desktop)
            .Where(path => File.ReadAllText(path).Contains("DesktopFeatureServices", StringComparison.Ordinal))
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

    [Fact]
    public void ActivityLogs_UseTypedCompiledViewsAndFocusedCollaborators()
    {
        var activity = Path.Combine(DesktopRoot(), "Features", "ActivityLogs");
        var views = Directory.EnumerateFiles(activity, "*.axaml", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("Styles.axaml", StringComparison.Ordinal));
        var bindingViolations = views
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("x:CompileBindings=\"False\"", StringComparison.Ordinal)
                    || !source.Contains("x:DataType=", StringComparison.Ordinal);
            })
            .Select(path => Path.GetFileName(path))
            .ToArray();
        var retiredPartials = Directory.EnumerateFiles(activity, "ActivityViewModel.*.cs", SearchOption.TopDirectoryOnly).ToArray();

        Assert.Empty(bindingViolations);
        Assert.Empty(retiredPartials);
        Assert.True(File.Exists(Path.Combine(activity, "ActivityLogListViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(activity, "ActivityLogDetailsViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(activity, "ActivityLogManagementViewModel.cs")));

        var activityView = File.ReadAllText(Path.Combine(activity, "ActivityView.axaml"));
        Assert.DoesNotContain("DisplayMemberBinding", activityView, StringComparison.Ordinal);
        Assert.Equal(3, activityView.Split("x:DataType=\"local:ActivityFilterOptionVm\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("TimestampDisplay", File.ReadAllText(Path.Combine(activity, "ActivityLogListView.axaml")), StringComparison.Ordinal);
    }

    [Fact]
    public void ActivityRecorder_DoesNotExposeBackingStore()
    {
        var contract = File.ReadAllText(Path.Combine(DesktopRoot(), "Shell", "Runtime", "IActivityRecorder.cs"));
        Assert.DoesNotContain("ActivityLogService Store", contract, StringComparison.Ordinal);
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
