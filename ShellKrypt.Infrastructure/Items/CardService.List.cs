using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class CardService
{
    public async Task<IReadOnlyList<CardEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var cards = new List<CardEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Card))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            cards.Add(ToEntry(row.Header, payload));
        }

        return cards;
    }
}
