using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IActivityRecorder
{
    ActivityLogService Store { get; }
    event EventHandler? Changed;
    void Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null);
}
