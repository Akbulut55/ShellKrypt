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
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public string TotalItemsDeltaText => CreatedThisMonthCount <= 0 ? "0 items this month" : $"+{CreatedThisMonthCount} items this month";
    public string WeakPasswordSubtitle => WeakPasswordCount <= 0 ? "0 passwords needs attention" : $"{WeakPasswordCount} passwords needs attention";
    public string ReusedPasswordSubtitle => ReusedPasswordCount <= 0 ? "No overlap found" : "Security risk";
    public string ExpiringSoonCardSubtitle => ExpiringSoonCardCount switch
    {
        0 => "No urgent renewals",
        1 => "1 card expires within 3 months",
        _ => $"{ExpiringSoonCardCount} cards expire within 3 months"
    };
    public string FooterSummary => $"Showing {Rows.Count} of {FilteredCount} items";

    public string AddItemButtonText => ActiveType switch
    {
        "web" => "+ Add Login",
        "card" => "+ Add Card",
        "note" => "+ Add Note",
        "authenticator" => "+ Add Authenticator",
        "api" => "+ Add API Key",
        _ => "+ Add Item"
    };

    public string EmptyStateTitle => ActiveScope switch
    {
        "favorites" => "No favorites match this view",
        "recent" => "No recent items found",
        _ => "No vault items match this view"
    };

    public string EmptyStateSubtitle => ActiveScope switch
    {
        "favorites" => "Mark items as favorites from their dedicated sections, then they will surface here.",
        "recent" => "Try a wider search, or switch back to all items to inspect the full vault.",
        _ => "Adjust the search query or category filter to surface another item set."
    };
}
