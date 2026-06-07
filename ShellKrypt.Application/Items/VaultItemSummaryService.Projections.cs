using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildSummary(VaultItemRow row, byte[] vaultKey, ICollection<string> webPasswords)
    {
        var labels = row.Labels.Select(label => label.Name).ToArray();

        return row.Header.Type switch
        {
            ItemType.Web => BuildWebSummary(row, vaultKey, labels, webPasswords),
            ItemType.Card => BuildCardSummary(row, vaultKey, labels),
            ItemType.Note => BuildNoteSummary(row, vaultKey, labels),
            ItemType.Authenticator => BuildAuthenticatorSummary(row, vaultKey, labels),
            ItemType.ApiKey => BuildApiKeySummary(row, vaultKey, labels),
            _ => new VaultItemSummary(
                row.Header.Id,
                row.Header.Type,
                "Unknown item",
                "Encrypted vault item",
                "N/A",
                labels,
                string.Join(" ", labels),
                row.Header.Favorite,
                row.Header.CreatedAtUtc,
                row.Header.UpdatedAtUtc,
                string.Empty)
        };
    }

}
