using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public ActivityLogService ActivityLogService => _activityRecorder.Store;

    public void LogActivity(
        string category,
        string title,
        string detail,
        string severity = "info",
        string? vaultPath = null,
        string? affectedItem = null)
        => _activityRecorder.Log(category, title, detail, severity, vaultPath, affectedItem);

    private static string GetVaultDisplayName(string? vaultPath)
        => string.IsNullOrWhiteSpace(vaultPath) ? "Vault" : Path.GetFileNameWithoutExtension(vaultPath);
}
