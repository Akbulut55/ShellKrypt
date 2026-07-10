using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildAuthenticatorSummary(VaultItemRow row, byte[] vaultKey, IReadOnlyList<string> labels)
    {
        var payload = _payloadReader.ReadAuthenticator(row, vaultKey);
        var normalizedType = payload.KeyType?.Trim().ToLowerInvariant();
        var isHotp = normalizedType is "counter-based" or "counter" or "hotp";
        var title = FirstNonEmpty(payload.ServiceName, payload.Issuer, "Authenticator");
        var subtitle = isHotp ? "Counter based code" : "Time based code";
        var identifier = isHotp ? $"Counter {Math.Max(0, payload.Counter)}" : "Rotates every 30 seconds";
        var searchText = BuildSearchText(
            title,
            subtitle,
            identifier,
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new VaultItemSummary(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            string.Empty);
    }
}
