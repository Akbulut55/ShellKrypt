using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class WebLoginService
{
    public async Task<IReadOnlyList<WebLoginEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var logins = new List<WebLoginEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Web))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            logins.Add(ToEntry(row.Header, payload));
        }

        return logins;
    }
}
