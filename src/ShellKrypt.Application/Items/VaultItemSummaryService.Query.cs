using ShellKrypt.Application.Common;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
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
            ItemListFilters.Project => filtered.Where(item => item.Type == ItemType.ProjectSecret),
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
                    ItemType.ProjectSecret => 5,
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
}
