using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildCardSummary(VaultItemRow row, byte[] vaultKey, IReadOnlyList<string> labels)
    {
        var payload = _payloadReader.ReadCard(row, vaultKey);
        var title = FirstNonEmpty(payload.Title, "Untitled card");
        var maskedNumber = MaskCardNumber(payload.Number);
        var subtitle = FirstNonEmpty(maskedNumber, "Encrypted card");
        var identifier = FirstNonEmpty(payload.Cardholder, maskedNumber, "N/A");
        var digits = new string((payload.Number ?? string.Empty).Where(char.IsDigit).ToArray());
        var copyValue = FirstNonEmpty(digits, payload.Cardholder, title);
        var searchText = BuildSearchText(
            title,
            subtitle,
            identifier,
            payload.Notes,
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
            copyValue,
            payload.ExpiryMonth,
            payload.ExpiryYear);
    }
}
