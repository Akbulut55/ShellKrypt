using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class ActivityRecorder(ActivityLogService store, IVaultSessionController session) : IActivityRecorder
{
    public event EventHandler<ActivityRecorderChangedEventArgs>? Changed;

    public ActivityLogOperationResult Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null)
    {
        var targetVaultPath = string.IsNullOrWhiteSpace(vaultPath) ? session.VaultPath : vaultPath;
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

        var result = store.Append(entry, session.IsUnlocked ? session.VaultKey : null);
        Changed?.Invoke(this, new ActivityRecorderChangedEventArgs(result));
        return result;
    }
}
