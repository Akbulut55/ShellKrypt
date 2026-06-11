using System;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel
{
    public bool IsAllScopeActive => string.Equals(ActiveScope, "all", StringComparison.Ordinal);
    public bool IsFavoritesScopeActive => string.Equals(ActiveScope, "favorites", StringComparison.Ordinal);
    public bool IsRecentScopeActive => string.Equals(ActiveScope, "recent", StringComparison.Ordinal);

    public bool IsAllTypeActive => string.Equals(ActiveType, "all", StringComparison.Ordinal);
    public bool IsWebTypeActive => string.Equals(ActiveType, "web", StringComparison.Ordinal);
    public bool IsCardTypeActive => string.Equals(ActiveType, "card", StringComparison.Ordinal);
    public bool IsNoteTypeActive => string.Equals(ActiveType, "note", StringComparison.Ordinal);
    public bool IsAuthenticatorTypeActive => string.Equals(ActiveType, "authenticator", StringComparison.Ordinal);
    public bool IsApiKeyTypeActive => string.Equals(ActiveType, "api", StringComparison.Ordinal);

    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int TotalPages => DesktopPagination.GetTotalPages(FilteredCount, PageSize);
    public string PageSummary => T(_root, "Common.PageSummary", CurrentPage, TotalPages);
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public string TotalItemsDeltaText => CreatedThisMonthCount <= 0
        ? T(_root, "AllItems.CreatedThisMonth.None")
        : T(_root, "AllItems.CreatedThisMonth.Count", CreatedThisMonthCount);
    public string WeakPasswordSubtitle => WeakPasswordCount <= 0
        ? T(_root, "AllItems.WeakPasswords.None")
        : T(_root, "AllItems.WeakPasswords.Count", WeakPasswordCount);
    public string ReusedPasswordSubtitle => ReusedPasswordCount <= 0 ? T(_root, "AllItems.ReusedPasswords.None") : T(_root, "AllItems.ReusedPasswords.Risk");
    public string ExpiringSoonCardSubtitle => ExpiringSoonCardCount switch
    {
        0 => T(_root, "AllItems.ExpiringCards.None"),
        1 => T(_root, "AllItems.ExpiringCards.One"),
        _ => T(_root, "AllItems.ExpiringCards.Many", ExpiringSoonCardCount)
    };
    public string FooterSummary => T(_root, "AllItems.FooterSummary", Rows.Count, FilteredCount);

    public string AllFilterLabel => T(_root, "AllItems.Filter.All", TotalCount);
    public string WebFilterLabel => T(_root, "AllItems.Filter.Logins", WebCount);
    public string CardFilterLabel => T(_root, "AllItems.Filter.Cards", CardCount);
    public string AuthenticatorFilterLabel => T(_root, "AllItems.Filter.Authenticator", AuthenticatorCount);
    public string ApiKeyFilterLabel => T(_root, "AllItems.Filter.ApiKeys", ApiKeyCount);
    public string NoteFilterLabel => T(_root, "AllItems.Filter.Notes", NoteCount);

    public string AddItemButtonText => ActiveType switch
    {
        "web" => T(_root, "AllItems.Add.Login"),
        "card" => T(_root, "AllItems.Add.Card"),
        "note" => T(_root, "AllItems.Add.Note"),
        "authenticator" => T(_root, "AllItems.Add.Authenticator"),
        "api" => T(_root, "AllItems.Add.ApiKey"),
        _ => T(_root, "AllItems.Add.Item")
    };

    public string EmptyStateTitle => ActiveScope switch
    {
        "favorites" => T(_root, "AllItems.Empty.FavoritesTitle"),
        "recent" => T(_root, "AllItems.Empty.RecentTitle"),
        _ => T(_root, "AllItems.Empty.DefaultTitle")
    };

    public string EmptyStateSubtitle => ActiveScope switch
    {
        "favorites" => T(_root, "AllItems.Empty.FavoritesSubtitle"),
        "recent" => T(_root, "AllItems.Empty.RecentSubtitle"),
        _ => T(_root, "AllItems.Empty.DefaultSubtitle")
    };
}
