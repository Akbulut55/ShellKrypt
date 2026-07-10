using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.Services.QuickFill;

internal sealed class UnsupportedAutoTypeBackend : IQuickFillAutoTypeBackend
{
    public Task<bool> SendAsync(nint targetWindowHandle, IReadOnlyList<AutoTypeStep> steps, CancellationToken ct = default)
        => Task.FromResult(false);
}
