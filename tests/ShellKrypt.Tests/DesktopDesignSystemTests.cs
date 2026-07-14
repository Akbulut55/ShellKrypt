using System.Text.RegularExpressions;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class DesktopDesignSystemTests
{
    private static readonly Regex HexColorPattern = new("#[0-9a-fA-F]{6,8}", RegexOptions.Compiled);

    [Fact]
    public void DesktopViews_DoNotContainHardCodedColors()
    {
        var desktopRoot = Path.Combine(FindRepositoryRoot(), "src", "ShellKrypt.Desktop");
        var approvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(desktopRoot, "App.axaml"),
            Path.Combine(desktopRoot, "Resources", "Styles", "DesignTokens.axaml")
        };

        var violations = Directory.EnumerateFiles(desktopRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(path => !approvedFiles.Contains(path))
            .Where(path => HexColorPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(desktopRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, $"Hard-coded colors found in: {string.Join(", ", violations)}");
    }

    [Fact]
    public void DesktopSources_DoNotReferenceRemovedThemes()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = new[] { Path.Combine(root, "src") };
        var removedIds = new[] { "crimson", "ocean", "forest" };
        var violations = new List<string>();

        foreach (var sourceRoot in sourceRoots)
        {
            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                         .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                        path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)))
            {
                var text = File.ReadAllText(path);
                if (removedIds.Any(id => text.Contains(id, StringComparison.OrdinalIgnoreCase)))
                    violations.Add(Path.GetRelativePath(root, path));
            }
        }

        Assert.True(violations.Count == 0, $"Removed themes referenced by: {string.Join(", ", violations)}");
    }

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
