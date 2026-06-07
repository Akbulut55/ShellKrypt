using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildWebSummary(
        VaultItemRow row,
        byte[] vaultKey,
        IReadOnlyList<string> labels,
        ICollection<string> webPasswords)
    {
        var payload = _payloadReader.ReadWeb(row, vaultKey);
        var title = FirstNonEmpty(payload.Title, payload.Url, payload.Username, "Untitled login");
        var subtitle = FirstNonEmpty(payload.Url, "Encrypted login");
        var identifier = FirstNonEmpty(payload.Username, "N/A");
        var copyValue = FirstNonEmpty(payload.Username, payload.Password, payload.Url, title);
        var searchText = BuildSearchText(
            title,
            subtitle,
            identifier,
            payload.Notes,
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        webPasswords.Add(payload.Password ?? string.Empty);

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
            copyValue);
    }
}
