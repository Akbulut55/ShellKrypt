using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddWebLogin(VaultItemRow row, byte[] vaultKey, List<WebLoginHealthItem> webLogins)
    {
        var payload = JsonSerializer.Deserialize<WebPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        webLogins.Add(new WebLoginHealthItem(
            row.Header.Id,
            SafeName(payload.Title, "Web login"),
            payload.Username,
            payload.Password,
            ParseUpdated(row.Header.UpdatedAtUtc)));
    }

    private sealed record WebLoginHealthItem(
        string Id,
        string Title,
        string Username,
        string Password,
        DateTimeOffset UpdatedAtUtc);
}
