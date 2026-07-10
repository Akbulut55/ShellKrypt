using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services.QuickFill;

internal sealed class UnsupportedForegroundWindowBackend(string status) : IQuickFillTargetCaptureBackend
{
    public string Status { get; } = status;

    public QuickFillTargetContext Capture() => new("", "");
}
