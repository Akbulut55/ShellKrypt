namespace ShellKrypt.Desktop.Features.MarkdownNotes;

public sealed class NoteAutoSaveController(TimeProvider timeProvider, Func<TimeSpan> delay)
{
    private CancellationTokenSource? _pending;
    private readonly SemaphoreSlim _singleFlight = new(1, 1);

    public void Schedule(long revision, Func<long, CancellationToken, Task> save)
    {
        Cancel();
        _pending = new CancellationTokenSource();
        _ = RunAsync(revision, save, _pending.Token);
    }

    public void Cancel()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private async Task RunAsync(long revision, Func<long, CancellationToken, Task> save, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay(), timeProvider, ct);
            await _singleFlight.WaitAsync(ct);
            try
            {
                await save(revision, ct);
            }
            finally
            {
                _singleFlight.Release();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
