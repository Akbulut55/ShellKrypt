using System.Text.RegularExpressions;
using ShellKrypt.Application.Backups;
using ShellKrypt.Core.Backups;

namespace ShellKrypt.Infrastructure.Backups;

public sealed class AutomaticBackupFileStore : IAutomaticBackupFileStore
{
    public string BuildBackupPath(string backupDirectory, string vaultPath, DateTimeOffset nowUtc)
    {
        var vaultName = Path.GetFileNameWithoutExtension(vaultPath);
        var safeVaultName = NormalizeFileSegment(string.IsNullOrWhiteSpace(vaultName) ? "Vault" : vaultName);
        return Path.Combine(backupDirectory, $"ShellKrypt-{safeVaultName}-Auto-{nowUtc:yyyyMMdd-HHmmss}.skbx");
    }

    public IReadOnlyList<string> EnumerateBackupFiles(string backupDirectory, string vaultPath)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
            return [];

        var safeVaultName = NormalizeFileSegment(Path.GetFileNameWithoutExtension(vaultPath));
        var prefix = $"ShellKrypt-{safeVaultName}-Auto-";
        var pattern = new Regex(
            $"^{Regex.Escape(prefix)}\\d{{8}}-\\d{{6}}\\.skbx$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return Directory
            .EnumerateFiles(backupDirectory, $"{prefix}*.skbx", SearchOption.TopDirectoryOnly)
            .Where(path => pattern.IsMatch(Path.GetFileName(path)))
            .ToArray();
    }

    public int ApplyRetention(string backupDirectory, string vaultPath, int retentionCount)
    {
        var safeRetentionCount = Math.Clamp(
            retentionCount,
            BackupScheduleSettings.MinRetentionCount,
            BackupScheduleSettings.MaxRetentionCount);

        var files = EnumerateBackupFiles(backupDirectory, vaultPath)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(safeRetentionCount)
            .ToArray();

        foreach (var file in files)
            file.Delete();

        return files.Length;
    }

    private static string NormalizeFileSegment(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "Vault" : value.Trim();
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var chars = text.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch).ToArray();
        var normalized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "Vault" : normalized[..Math.Min(normalized.Length, 80)];
    }
}
