namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel
{
    private void ShowAll()
    {
        ActiveScope = "all";
        ActiveType = "all";
    }

    private void ShowFavorites() => ActiveScope = "favorites";
    private void ShowRecent() => ActiveScope = "recent";
    private void ShowWebTypes() => ActiveType = "web";
    private void ShowCardTypes() => ActiveType = "card";
    private void ShowNoteTypes() => ActiveType = "note";
    private void ShowAuthenticatorTypes() => ActiveType = "authenticator";
    private void ShowApiKeyTypes() => ActiveType = "api";

    private void ResetFilters()
    {
        Error = string.Empty;
        SearchText = string.Empty;
        ActiveScope = "all";
        ActiveType = "all";
        CurrentPage = 1;
        ApplyFilter();
    }

    private void CycleSort()
    {
        Error = string.Empty;
        _sortMode = _sortMode switch
        {
            AllItemsSortMode.UpdatedDescending => AllItemsSortMode.Alphabetical,
            AllItemsSortMode.Alphabetical => AllItemsSortMode.TypeThenTitle,
            _ => AllItemsSortMode.UpdatedDescending
        };

        CurrentPage = 1;
        ApplyFilter();
    }

    private void GoPreviousPage()
    {
        if (CanGoPrevious)
            CurrentPage--;
    }

    private void GoNextPage()
    {
        if (CanGoNext)
            CurrentPage++;
    }

    private void GoToPage(PageChipVm? page)
    {
        if (page is not null)
            CurrentPage = page.Number;
    }
}
