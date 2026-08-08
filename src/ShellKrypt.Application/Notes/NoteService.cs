namespace ShellKrypt.Application.Notes;

public sealed class NoteService(
    INoteStore store,
    TimeProvider? timeProvider = null,
    Func<Guid>? createId = null) : INoteService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Func<Guid> _createId = createId ?? Guid.NewGuid;

    public async Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vaultPath) || vaultKey is not { Length: > 0 })
            return new([], 0, NoteFailureKind.Unavailable);

        try
        {
            var result = await store.LoadAsync(vaultPath, vaultKey, ct);
            if (!result.Success)
                return new([], 0, result.FailureKind);

            var entries = new List<NoteEntry>(result.Entries.Count);
            var skipped = result.SkippedCorruptEntries;
            foreach (var entry in result.Entries)
            {
                var title = entry.Title?.Trim() ?? "";
                if (title.Length == 0)
                {
                    skipped++;
                    continue;
                }
                entries.Add(entry with { Title = title, Content = entry.Content ?? "" });
            }
            return new(entries, skipped, NoteFailureKind.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new([], 0, NoteFailureKind.ReadFailed);
        }
    }

    public async Task<NoteMutationResult> AddAsync(string vaultPath, byte[] vaultKey, NoteInput input, CancellationToken ct = default)
    {
        if (input is null)
            return NoteMutationResult.Failed(NoteFailureKind.ValidationFailed);
        if (!TryNormalize(input, out var normalized) || string.IsNullOrWhiteSpace(vaultPath) || vaultKey is not { Length: > 0 })
            return NoteMutationResult.Failed(string.IsNullOrWhiteSpace(input.Title) ? NoteFailureKind.ValidationFailed : NoteFailureKind.Unavailable);

        var now = _timeProvider.GetUtcNow().ToString("O");
        var entry = new NoteEntry(_createId().ToString("N"), normalized.Title, normalized.Content ?? "", normalized.Favorite, now, now);
        var result = await SafeWriteAsync(() => store.InsertAsync(vaultPath, vaultKey, entry, ct), ct);
        return result.Success ? NoteMutationResult.Succeeded(entry) : NoteMutationResult.Failed(result.FailureKind);
    }

    public async Task<NoteMutationResult> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, NoteInput input, CancellationToken ct = default)
    {
        if (input is null)
            return NoteMutationResult.Failed(NoteFailureKind.ValidationFailed);
        if (!TryNormalize(input, out var normalized) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(createdAtUtc))
            return NoteMutationResult.Failed(NoteFailureKind.ValidationFailed);
        if (string.IsNullOrWhiteSpace(vaultPath) || vaultKey is not { Length: > 0 })
            return NoteMutationResult.Failed(NoteFailureKind.Unavailable);

        var entry = new NoteEntry(id, normalized.Title, normalized.Content ?? "", normalized.Favorite, createdAtUtc, _timeProvider.GetUtcNow().ToString("O"));
        var result = await SafeWriteAsync(() => store.UpdateAsync(vaultPath, vaultKey, entry, ct), ct);
        return result.Success ? NoteMutationResult.Succeeded(entry) : NoteMutationResult.Failed(result.FailureKind);
    }

    public async Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vaultPath) || string.IsNullOrWhiteSpace(id))
            return new(NoteFailureKind.Unavailable);

        try
        {
            return await store.DeleteAsync(vaultPath, id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(NoteFailureKind.DeleteFailed);
        }
    }

    private static bool TryNormalize(NoteInput input, out NoteInput normalized)
    {
        var title = input.Title?.Trim() ?? "";
        normalized = new(title, input.Content ?? "", input.Favorite);
        return title.Length > 0;
    }

    private static async Task<NoteOperationResult> SafeWriteAsync(Func<Task<NoteOperationResult>> write, CancellationToken ct)
    {
        try
        {
            var result = await write();
            return result.Success ? result : new(NoteFailureKind.WriteFailed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(NoteFailureKind.WriteFailed);
        }
    }
}
