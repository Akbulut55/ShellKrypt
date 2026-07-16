using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.Services.Runtime;

public sealed class ActivityRecorder(ActivityLogService store, IVaultSessionController session) : IActivityRecorder
{
    public ActivityLogService Store => store;
    public event EventHandler? Changed;

    public void Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null)
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

        try
        {
            store.Append(entry, session.IsUnlocked ? session.VaultKey : null);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }
}
