using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class NoteService
{
    public async Task<IReadOnlyList<NoteEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var notes = new List<NoteEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Note))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            notes.Add(ToEntry(row.Header, payload));
        }

        return notes;
    }
}
