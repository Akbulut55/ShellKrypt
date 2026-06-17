using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class QuickFillEntryService
{
    public async Task<IReadOnlyList<QuickFillEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var entries = new List<QuickFillEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.QuickFillEntry))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            entries.Add(ToEntry(row.Header, payload));
        }

        return entries;
    }
}
