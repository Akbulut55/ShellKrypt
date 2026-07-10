namespace ShellKrypt.Infrastructure.Vaulting;

public static class VaultFileGuard
{
    public const string VaultExtension = ".skvault";
    public const string BackupExtension = ".skbx";
    public const string JsonExtension = ".json";
    public const string CsvExtension = ".csv";
    private static readonly string[] KnownVaultSidecarSuffixes = ["-wal", "-shm", "-journal"];

    public static string NormalizeFullPath(string path, string paramName = "path")
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", paramName);

        return Path.GetFullPath(path.Trim());
    }

    public static string EnsureVaultFilePath(string path, string paramName = "path")
    {
        var fullPath = NormalizeFullPath(path, paramName);
        if (!string.Equals(Path.GetExtension(fullPath), VaultExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vault files must use the .skvault extension.");

        return fullPath;
    }

    public static string EnsureExistingVaultFile(string path, string paramName = "path")
    {
        var fullPath = EnsureVaultFilePath(path, paramName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Vault file not found.", fullPath);

        return fullPath;
    }

    public static string EnsureSafeVaultDeletionTarget(string path, string? expectedPath = null)
    {
        var fullPath = EnsureExistingVaultFile(path);
        if (!string.IsNullOrWhiteSpace(expectedPath))
        {
            var expected = EnsureVaultFilePath(expectedPath, nameof(expectedPath));
            if (!string.Equals(fullPath, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a vault path that does not match the selected vault.");
        }

        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName) || string.Equals(fullPath, Path.GetPathRoot(fullPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete an unsafe vault path.");

        return fullPath;
    }

    public static void DeleteVaultAndKnownSidecars(string path, string? expectedPath = null)
    {
        var fullPath = EnsureSafeVaultDeletionTarget(path, expectedPath);

        foreach (var suffix in KnownVaultSidecarSuffixes)
            DeleteKnownSidecarIfExists(fullPath, suffix);

        File.Delete(fullPath);
    }

    private static void DeleteKnownSidecarIfExists(string fullVaultPath, string suffix)
    {
        if (!KnownVaultSidecarSuffixes.Contains(suffix, StringComparer.Ordinal))
            throw new InvalidOperationException("Unsupported vault sidecar suffix.");

        var sidecar = Path.GetFullPath(fullVaultPath + suffix);
        if (!string.Equals(sidecar, fullVaultPath + suffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete an unsafe vault sidecar path.");

        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }

    public static string EnsureNotActiveVaultTarget(string activeVaultPath, string targetPath, string targetLabel)
    {
        var active = EnsureVaultFilePath(activeVaultPath, nameof(activeVaultPath));
        var target = NormalizeFullPath(targetPath, nameof(targetPath));
        if (string.Equals(active, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{targetLabel} cannot overwrite the active vault file.");

        return target;
    }

    public static string EnsureExtension(string path, string extension, string label)
    {
        var fullPath = NormalizeFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{label} must use the {extension} extension.");

        return fullPath;
    }

    public static void EnsureDifferentPaths(string sourcePath, string targetPath, string message)
    {
        var source = NormalizeFullPath(sourcePath, nameof(sourcePath));
        var target = NormalizeFullPath(targetPath, nameof(targetPath));
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(message);
    }
}
