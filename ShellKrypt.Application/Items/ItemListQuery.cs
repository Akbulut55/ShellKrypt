namespace ShellKrypt.Application.Items;

public static class ItemListFilters
{
    public const string All = "all";
    public const string Favorites = "favorites";
    public const string Recent = "recent";
    public const string Web = "web";
    public const string Card = "card";
    public const string Note = "note";
    public const string Authenticator = "authenticator";
    public const string Api = "api";
}

public static class ItemListSortModes
{
    public const string UpdatedDescending = "updated_desc";
    public const string Alphabetical = "alphabetical";
    public const string TypeThenTitle = "type_title";
}

public sealed record ItemListQuery(
    string SearchText,
    string TypeFilter,
    string ScopeFilter,
    string SortMode,
    int Page,
    int PageSize)
{
    public static ItemListQuery Default(int pageSize) => new(
        SearchText: string.Empty,
        TypeFilter: ItemListFilters.All,
        ScopeFilter: ItemListFilters.All,
        SortMode: ItemListSortModes.UpdatedDescending,
        Page: 1,
        PageSize: pageSize);
}
