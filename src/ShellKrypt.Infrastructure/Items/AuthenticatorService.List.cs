using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService
{
    public async Task<IReadOnlyList<AuthenticatorEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var entries = new List<AuthenticatorEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Authenticator))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            entries.Add(ToEntry(row.Header, payload));
        }

        return entries;
    }

    private async Task<AuthenticatorEntry?> GetEntryAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct)
    {
        var entries = await ListAsync(vaultPath, vaultKey, ct);
        return entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }
}
