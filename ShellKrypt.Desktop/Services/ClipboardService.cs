using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;

namespace ShellKrypt.Desktop.Services;

public sealed class ClipboardService
{
    private readonly object _gate = new();
    private IClipboard? _clipboard;
    private CancellationTokenSource? _pendingClearCts;

    public void Attach(IClipboard? clipboard)
    {
        lock (_gate)
        {
            _clipboard = clipboard;
        }
    }

    public async Task CopyAsync(string text, TimeSpan clearAfter, CancellationToken ct = default)
    {
        IClipboard? clipboard;
        CancellationTokenSource? pendingClear = null;

        lock (_gate)
        {
            clipboard = _clipboard;
            CancelPendingClear_NoLock();

            if (clipboard is not null && clearAfter > TimeSpan.Zero)
            {
                pendingClear = new CancellationTokenSource();
                _pendingClearCts = pendingClear;
            }
        }

        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(text);

        if (pendingClear is null)
            return;

        _ = ClearAfterDelayAsync(clipboard, text, clearAfter, pendingClear, ct);
    }

    public async Task ClearAsync()
    {
        IClipboard? clipboard;

        lock (_gate)
        {
            clipboard = _clipboard;
            CancelPendingClear_NoLock();
        }

        if (clipboard is null)
            return;

        await SafeClearAsync(clipboard);
    }

    public async Task<Bitmap?> TryGetBitmapAsync()
    {
        IClipboard? clipboard;

        lock (_gate)
        {
            clipboard = _clipboard;
        }

        if (clipboard is null)
            return null;

        try
        {
            return await clipboard.TryGetBitmapAsync();
        }
        catch
        {
            return null;
        }
    }

    private async Task ClearAfterDelayAsync(
        IClipboard clipboard,
        string expectedText,
        TimeSpan delay,
        CancellationTokenSource pendingClear,
        CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(pendingClear.Token, ct);
            await Task.Delay(delay, linked.Token);

            if (linked.IsCancellationRequested)
                return;

            var current = await clipboard.TryGetTextAsync();
            if (string.Equals(current, expectedText, StringComparison.Ordinal))
                await SafeClearAsync(clipboard);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_pendingClearCts, pendingClear))
                {
                    _pendingClearCts.Dispose();
                    _pendingClearCts = null;
                }
            }
        }
    }

    private static async Task SafeClearAsync(IClipboard clipboard)
    {
        try
        {
            await clipboard.ClearAsync();
        }
        catch
        {
        }
    }

    private void CancelPendingClear_NoLock()
    {
        if (_pendingClearCts is null)
            return;

        _pendingClearCts.Cancel();
        _pendingClearCts.Dispose();
        _pendingClearCts = null;
    }
}
