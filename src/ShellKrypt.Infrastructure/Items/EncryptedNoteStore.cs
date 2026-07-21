using System.Text.Json;
using ShellKrypt.Application.Notes;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class EncryptedNoteStore(IItemRepository repo) : INoteStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        try
        {
            var rows = await repo.ListAsync(vaultPath, vaultKey, ct);
            var notes = new List<NoteEntry>();
            var skipped = 0;

            foreach (var row in rows.Where(row => row.Header.Type == ItemType.Note))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<NotePayload>(
                        VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload), JsonOpts);
                    if (payload is null)
                    {
                        skipped++;
                        continue;
                    }

                    notes.Add(new NoteEntry(row.Header.Id, payload.Title, payload.Content ?? "", row.Header.Favorite,
                        row.Header.CreatedAtUtc, row.Header.UpdatedAtUtc));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    skipped++;
                }
            }

            return new(notes, skipped, NoteFailureKind.None);
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

    public Task<NoteOperationResult> InsertAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
        => WriteAsync(() => repo.InsertAsync(vaultPath, ToHeader(entry), Encrypt(vaultKey, entry), ct), NoteFailureKind.WriteFailed, ct);

    public Task<NoteOperationResult> UpdateAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default)
        => WriteAsync(() => repo.UpdateAsync(vaultPath, ToHeader(entry), Encrypt(vaultKey, entry), ct), NoteFailureKind.WriteFailed, ct);

    public Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => WriteAsync(() => repo.DeleteAsync(vaultPath, id, ct), NoteFailureKind.DeleteFailed, ct);

    private static VaultItemHeader ToHeader(NoteEntry entry)
        => new(entry.Id, ItemType.Note, entry.Favorite, entry.CreatedAtUtc, entry.UpdatedAtUtc);

    private static byte[] Encrypt(byte[] vaultKey, NoteEntry entry)
    {
        var header = ToHeader(entry);
        var payload = new NotePayload(entry.Title, entry.Content);
        return VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));
    }

    private static async Task<NoteOperationResult> WriteAsync(
        Func<Task> operation,
        NoteFailureKind failureKind,
        CancellationToken ct)
    {
        try
        {
            await operation();
            return NoteOperationResult.Succeeded;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(failureKind);
        }
    }
}
