using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ApiKeyService
{
    public async Task<IReadOnlyList<ApiKeyEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var apiKeys = new List<ApiKeyEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.ApiKey))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            apiKeys.Add(ToEntry(row.Header, payload));
        }

        return apiKeys;
    }
}
