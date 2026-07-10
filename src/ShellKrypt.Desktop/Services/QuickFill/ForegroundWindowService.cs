using System;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.Services.QuickFill;

public sealed class ForegroundWindowService
{
    private readonly IQuickFillTargetCaptureBackend _backend = QuickFillTargetCaptureBackendSelector.Select();

    public string Status => _backend.Status;

    public QuickFillTargetContext Capture() => _backend.Capture();
}

internal interface IQuickFillTargetCaptureBackend
{
    string Status { get; }
    QuickFillTargetContext Capture();
}

internal static class QuickFillTargetCaptureBackendSelector
{
    public static IQuickFillTargetCaptureBackend Select()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsForegroundWindowBackend();

        if (OperatingSystem.IsLinux())
        {
            if (QuickFillLinuxSession.IsWayland)
                return new UnsupportedForegroundWindowBackend("Target capture is limited by this Wayland compositor. Use X11 for automatic target capture, or create the entry from the manager.");

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
                return new LinuxX11ForegroundWindowBackend();
        }

        return new UnsupportedForegroundWindowBackend("");
    }
}
