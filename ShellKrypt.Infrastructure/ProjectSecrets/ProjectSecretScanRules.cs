namespace ShellKrypt.Infrastructure.ProjectSecrets;

public static class ProjectSecretScanRules
{
    public static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", "node_modules", "bin", "obj", "build", "dist", "out", "publish",
        "artifacts", "coverage", ".vscode", ".idea", ".next", ".nuxt", ".cache", "target", "vendor", "packages"
    };

    public static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".pdf", ".zip", ".7z", ".tar", ".gz", ".rar",
        ".exe", ".dll", ".so", ".dylib", ".class", ".jar", ".bin", ".sqlite", ".db"
    };

    public static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".fs", ".vb", ".csproj", ".sln", ".slnx", ".json", ".yaml", ".yml", ".toml", ".xml",
        ".props", ".targets", ".config", ".env", ".example", ".template", ".js", ".jsx", ".ts", ".tsx",
        ".mjs", ".cjs", ".py", ".java", ".kt", ".kts", ".go", ".rs", ".php", ".rb", ".swift", ".sh",
        ".bash", ".zsh", ".ps1", ".md", ".txt"
    };

    public static bool ShouldSkipDirectory(string directory)
        => IgnoredDirectories.Contains(Path.GetFileName(directory));

    public static bool ShouldScanFile(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        if (IgnoredExtensions.Contains(extension))
            return false;

        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
            return true;

        return IncludedExtensions.Contains(extension);
    }
}
