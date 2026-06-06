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

    private VaultItemSummary BuildApiKeySummary(VaultItemRow row, byte[] vaultKey, IReadOnlyList<string> labels)
    {
        var payload = _payloadReader.ReadApiKey(row, vaultKey);
        var title = FirstNonEmpty(payload.Name, "Untitled API key");
        var provider = FirstNonEmpty(payload.Provider, "Unknown provider");
        var environment = FirstNonEmpty(payload.Environment, "Production");
        var primaryField = payload.Fields
            .OrderBy(field => field.SortOrder)
            .FirstOrDefault(field => field.IsSensitive && field.IsCopyable)
            ?? payload.Fields.OrderBy(field => field.SortOrder).FirstOrDefault(field => field.IsCopyable)
            ?? payload.Fields.OrderBy(field => field.SortOrder).FirstOrDefault();
        var fieldSummary = primaryField is null
            ? "No fields"
            : $"{primaryField.Label}: {MaskApiKeyValue(primaryField.Value)}";
        var identifier = FirstNonEmpty(provider, environment, primaryField?.Label, "N/A");
        var copyValue = FirstNonEmpty(primaryField?.Value, title);
        var searchText = BuildSearchText(
            title,
            provider,
            environment,
            payload.Notes,
            string.Join(" ", payload.Fields.Select(field => $"{field.Label} {field.FieldType}")),
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new VaultItemSummary(
            row.Header.Id,
            row.Header.Type,
            title,
            fieldSummary,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue);
    }
}
