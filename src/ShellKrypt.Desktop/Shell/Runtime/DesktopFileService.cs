using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.Shell.Runtime;

internal sealed class DesktopFileService : IDesktopFileService
{
    public string GetSuggestedVaultPath(string displayName) => DefaultPaths.GetSuggestedVaultPath(displayName);
    public string GetSuggestedExportPath(string displayName, string extension) => DefaultPaths.GetSuggestedExportPath(displayName, extension);
    public string EnsureExistingVaultFile(string path) => VaultFileGuard.EnsureExistingVaultFile(path);
    public string EnsureVaultFilePath(string path) => VaultFileGuard.EnsureVaultFilePath(path);
    public string EnsureSafeVaultDeletionTarget(string path, string? activeVaultPath) => VaultFileGuard.EnsureSafeVaultDeletionTarget(path, activeVaultPath);
    public void EnsureDifferentPaths(string sourcePath, string targetPath, string message) => VaultFileGuard.EnsureDifferentPaths(sourcePath, targetPath, message);
    public void DeleteVaultAndKnownSidecars(string path, string? activeVaultPath) => VaultFileGuard.DeleteVaultAndKnownSidecars(path, activeVaultPath);
    public void ClearDatabasePools() => SqliteConnection.ClearAllPools();
}
