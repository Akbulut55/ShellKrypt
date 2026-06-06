using System;
using System.IO;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public ActivityLogService ActivityLogService => _activityLogService;

    public void LogActivity(
        string category,
        string title,
        string detail,
        string severity = "info",
        string? vaultPath = null,
        string? affectedItem = null)
    {
        var targetVaultPath = string.IsNullOrWhiteSpace(vaultPath) ? VaultPath : vaultPath;
        var entry = new ActivityLogEntry(
            Id: Guid.NewGuid().ToString("N"),
            TimestampUtc: DateTimeOffset.UtcNow.ToString("O"),
            Category: string.IsNullOrWhiteSpace(category) ? "system" : category.Trim().ToLowerInvariant(),
            Title: title.Trim(),
            Detail: detail.Trim(),
            Severity: string.IsNullOrWhiteSpace(severity) ? "info" : severity.Trim().ToLowerInvariant(),
            VaultPath: targetVaultPath)
        {
            AffectedItem = string.IsNullOrWhiteSpace(affectedItem) ? null : affectedItem.Trim()
        };

        try
        {
            _activityLogService.Append(entry, _state.VaultKey);
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    private static string GetVaultDisplayName(string? vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
            return "Vault";

        return Path.GetFileNameWithoutExtension(vaultPath);
    }
}
