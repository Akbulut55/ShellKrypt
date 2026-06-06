using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddCardFindings(VaultItemRow row, byte[] vaultKey, List<HealthAuditIssue> issues)
    {
        var payload = JsonSerializer.Deserialize<CardPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        var title = SafeName(payload.Title, "Credit card");
        var expiry = GetCardExpiryEnd(payload.ExpiryMonth, payload.ExpiryYear);
        if (expiry is null)
            return;

        var now = DateTimeOffset.UtcNow;
        if (expiry < now)
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.Card,
                HealthAuditSeverity.Medium,
                HealthAuditCategory.ExpiredCard,
                title,
                "Expired card",
                $"This card expired in {FormatExpiry(payload.ExpiryMonth, payload.ExpiryYear)}. Update or remove it if it is no longer valid.",
                HealthAuditRecommendedAction.OpenCard);
            return;
        }

        if (expiry <= now.AddDays(ExpiringCardDays))
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.Card,
                HealthAuditSeverity.Low,
                HealthAuditCategory.ExpiringCard,
                title,
                "Card expiring soon",
                $"This card expires in {FormatExpiry(payload.ExpiryMonth, payload.ExpiryYear)}. Prepare a replacement if this card is still in use.",
                HealthAuditRecommendedAction.OpenCard);
        }
    }
}
