using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildNoteSummary(VaultItemRow row, byte[] vaultKey, IReadOnlyList<string> labels)
    {
        var payload = _payloadReader.ReadNote(row, vaultKey);
        var title = FirstNonEmpty(payload.Title, "Untitled markdown note");
        var snippet = TrimSnippet(payload.Content, 72);
        var subtitle = FirstNonEmpty(snippet, "Encrypted markdown note");
        var copyValue = FirstNonEmpty(payload.Content, title);
        var searchText = BuildSearchText(
            title,
            snippet,
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new VaultItemSummary(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            "N/A",
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue);
    }
}
