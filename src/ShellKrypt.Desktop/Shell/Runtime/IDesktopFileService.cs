namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IDesktopFileService
{
    string GetSuggestedVaultPath(string displayName);
    string GetSuggestedExportPath(string displayName, string extension);
    string EnsureExistingVaultFile(string path);
    string EnsureVaultFilePath(string path);
    string EnsureSafeVaultDeletionTarget(string path, string? activeVaultPath);
    void EnsureDifferentPaths(string sourcePath, string targetPath, string message);
    void DeleteVaultAndKnownSidecars(string path, string? activeVaultPath);
    void ClearDatabasePools();
}
