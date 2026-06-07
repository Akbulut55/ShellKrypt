using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
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
