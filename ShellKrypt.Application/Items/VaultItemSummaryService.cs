using ShellKrypt.Application.Common;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed class VaultItemSummaryService : IVaultItemSummaryService
{
    private const int RecentWindowDays = 30;

    private readonly IItemRepository _repository;
    private readonly IVaultItemPayloadReader _payloadReader;
    private readonly Func<DateTimeOffset> _utcNow;

    public VaultItemSummaryService(
        IItemRepository repository,
        IVaultItemPayloadReader payloadReader,
        Func<DateTimeOffset>? utcNow = null)
    {
        _repository = repository;
        _payloadReader = payloadReader;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<VaultItemSummaryResult> ListAsync(
        string vaultPath,
        byte[] vaultKey,
        ItemListQuery query,
        CancellationToken ct = default)
    {
        var rows = await _repository.ListAsync(vaultPath, vaultKey, ct);
        var passwords = new List<string>();
        var all = rows.Select(row => BuildSummary(row, vaultKey, passwords)).ToArray();
        var counts = BuildCounts(all, passwords);
        var filtered = ApplyQuery(all, NormalizeQuery(query)).ToArray();
        var page = BuildPage(filtered, NormalizeQuery(query));

        return new VaultItemSummaryResult(all, page, counts);
    }

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

    private VaultItemSummaryCounts BuildCounts(IReadOnlyList<VaultItemSummary> items, IReadOnlyList<string> webPasswords)
    {
        var now = _utcNow();
        return new VaultItemSummaryCounts(
            Total: items.Count,
            WebLogins: items.Count(item => item.Type == ItemType.Web),
            Cards: items.Count(item => item.Type == ItemType.Card),
            Notes: items.Count(item => item.Type == ItemType.Note),
            Authenticators: items.Count(item => item.Type == ItemType.Authenticator),
            ApiKeys: items.Count(item => item.Type == ItemType.ApiKey),
            WeakPasswords: webPasswords.Count(IsWeakPassword),
            ReusedPasswords: CountReusedPasswords(webPasswords),
            ExpiringSoonCards: items.Count(item => item.IsCardExpiryUrgent(now.LocalDateTime.Date)),
            CreatedThisMonth: items.Count(item => DateTimeOffset.TryParse(item.CreatedAtUtc, out var created)
                                                  && created.ToUniversalTime().Year == now.Year
                                                  && created.ToUniversalTime().Month == now.Month));
    }

    private IEnumerable<VaultItemSummary> ApplyQuery(
        IReadOnlyList<VaultItemSummary> items,
        ItemListQuery query)
    {
        IEnumerable<VaultItemSummary> filtered = items;

        filtered = query.ScopeFilter switch
        {
            ItemListFilters.Favorites => filtered.Where(item => item.Favorite),
            ItemListFilters.Recent => filtered.Where(item => item.IsRecent(_utcNow(), RecentWindowDays)),
            _ => filtered
        };

        filtered = query.TypeFilter switch
        {
            ItemListFilters.Web => filtered.Where(item => item.Type == ItemType.Web),
            ItemListFilters.Card => filtered.Where(item => item.Type == ItemType.Card),
            ItemListFilters.Note => filtered.Where(item => item.Type == ItemType.Note),
            ItemListFilters.Authenticator => filtered.Where(item => item.Type == ItemType.Authenticator),
            ItemListFilters.Api => filtered.Where(item => item.Type == ItemType.ApiKey),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(query.SearchText))
            filtered = filtered.Where(item => item.SearchText.Contains(query.SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        return query.SortMode switch
        {
            ItemListSortModes.Alphabetical => filtered.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ItemListSortModes.TypeThenTitle => filtered
                .OrderBy(item => item.Type switch
                {
                    ItemType.Web => 0,
                    ItemType.Card => 1,
                    ItemType.Note => 2,
                    ItemType.Authenticator => 3,
                    ItemType.ApiKey => 4,
                    _ => 99
                })
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(GetUpdatedSortValue).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static PagedResult<VaultItemSummary> BuildPage(IReadOnlyList<VaultItemSummary> filtered, ItemListQuery query)
    {
        var pageSize = Math.Max(1, query.PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(filtered.Count, 1) / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new PagedResult<VaultItemSummary>(items, filtered.Count, page, pageSize);
    }

    private static ItemListQuery NormalizeQuery(ItemListQuery query)
        => query with
        {
            SearchText = query.SearchText?.Trim() ?? string.Empty,
            TypeFilter = string.IsNullOrWhiteSpace(query.TypeFilter) ? ItemListFilters.All : query.TypeFilter,
            ScopeFilter = string.IsNullOrWhiteSpace(query.ScopeFilter) ? ItemListFilters.All : query.ScopeFilter,
            SortMode = string.IsNullOrWhiteSpace(query.SortMode) ? ItemListSortModes.UpdatedDescending : query.SortMode,
            Page = Math.Max(1, query.Page),
            PageSize = Math.Max(1, query.PageSize)
        };

    private static string BuildSearchText(params string?[] parts)
        => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);

    private static string FirstNonEmpty(params string?[] parts)
        => parts.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part))?.Trim() ?? string.Empty;

    private static string TrimSnippet(string? text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var value = text.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static string MaskCardNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return string.Empty;

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        return $"**** **** **** {digits[^4..]}";
    }

    private static string MaskApiKeyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Encrypted API field";

        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? "****" : $"**** **** {trimmed[^4..]}";
    }

    private static bool IsWeakPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return true;

        var value = password.Trim();
        if (value.Length < 12)
            return true;

        var hasLetter = value.Any(char.IsLetter);
        var hasDigit = value.Any(char.IsDigit);
        var hasSymbol = value.Any(ch => !char.IsLetterOrDigit(ch));
        return !(hasLetter && hasDigit && hasSymbol);
    }

    private static int CountReusedPasswords(IEnumerable<string> passwords)
    {
        return passwords
            .Where(password => !string.IsNullOrWhiteSpace(password))
            .GroupBy(password => password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count());
    }

    private static DateTimeOffset GetUpdatedSortValue(VaultItemSummary item)
        => DateTimeOffset.TryParse(item.UpdatedAtUtc, out var updated) ? updated : DateTimeOffset.MinValue;
}
