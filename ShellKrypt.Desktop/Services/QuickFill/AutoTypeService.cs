using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Services.QuickFill;

public sealed class AutoTypeService
{
    private readonly IQuickFillAutoTypeBackend _backend = QuickFillAutoTypeBackendSelector.Select();

    public Task<bool> SendAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct = default)
        => _backend.SendAsync(targetWindowHandle, steps, ct);
}

internal interface IQuickFillAutoTypeBackend
{
    Task<bool> SendAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct = default);
}

internal static class QuickFillAutoTypeBackendSelector
{
    public static IQuickFillAutoTypeBackend Select()
    {
        if (OperatingSystem.IsWindows())
            return new CompositeAutoTypeBackend();

        if (OperatingSystem.IsLinux() && !QuickFillLinuxSession.IsWayland)
            return new CompositeAutoTypeBackend();

        return new UnsupportedAutoTypeBackend();
    }
}
