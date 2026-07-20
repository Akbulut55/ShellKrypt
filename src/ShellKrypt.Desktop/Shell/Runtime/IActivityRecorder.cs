using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IActivityRecorder
{
    event EventHandler<ActivityRecorderChangedEventArgs>? Changed;
    ActivityLogOperationResult Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null);
}

public sealed class ActivityRecorderChangedEventArgs(ActivityLogOperationResult result) : EventArgs
{
    public ActivityLogOperationResult Result { get; } = result;
}
