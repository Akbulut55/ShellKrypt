using System;
using System.Linq;
using System.Threading.Tasks;
using ShellKrypt.Application.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel
{
    private async Task RefreshAsync()
    {
        await LoadAsync(SelectedRow?.Id);
    }

    public Task RefreshAfterMutationAsync(string? selectItemId = null)
        => LoadAsync(selectItemId);

    private async Task LoadAsync(string? selectItemId = null)
    {
        await LoadPageAsync(selectItemId, refreshCounts: true);
    }

    private void ApplyFilter()
    {
        _ = LoadPageAsync(SelectedRow?.Id, refreshCounts: true);
    }

    private void RefreshVisibleRows()
    {
        _ = LoadPageAsync(SelectedRow?.Id, refreshCounts: false);
    }

    private async Task LoadPageAsync(string? selectItemId, bool refreshCounts)
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _summaryService.ListAsync(
                _root.VaultPath,
                _root.VaultKey,
                new ItemListQuery(
                    SearchText,
                    ActiveType,
                    ActiveScope,
                    SortModeToQueryValue(_sortMode),
                    CurrentPage,
                    PageSize));

            Rows.Clear();
            foreach (var row in result.Page.Items.Select(ToEntry))
                Rows.Add(row);

            if (_currentPage != result.Page.Page)
            {
                _currentPage = result.Page.Page;
                OnPropertyChanged(nameof(CurrentPage));
            }

            FilteredCount = result.Page.TotalCount;

            if (refreshCounts)
            {
                TotalCount = result.Counts.Total;
                WebCount = result.Counts.WebLogins;
                CardCount = result.Counts.Cards;
                NoteCount = result.Counts.Notes;
                AuthenticatorCount = result.Counts.Authenticators;
                ApiKeyCount = result.Counts.ApiKeys;
                WeakPasswordCount = result.Counts.WeakPasswords;
                ReusedPasswordCount = result.Counts.ReusedPasswords;
                ExpiringSoonCardCount = result.Counts.ExpiringSoonCards;
                CreatedThisMonthCount = result.Counts.CreatedThisMonth;
            }

            if (!string.IsNullOrWhiteSpace(selectItemId))
                SelectedRow = Rows.FirstOrDefault(x => x.Id == selectItemId) ?? Rows.FirstOrDefault();
            else if (SelectedRow is not null)
                SelectedRow = Rows.FirstOrDefault(x => x.Id == SelectedRow.Id) ?? Rows.FirstOrDefault();
            else
                SelectedRow = Rows.FirstOrDefault();

            RefreshPageChips();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageSummary));
            OnPropertyChanged(nameof(FooterSummary));
            OnPropertyChanged(nameof(EmptyStateTitle));
            OnPropertyChanged(nameof(EmptyStateSubtitle));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshPageChips()
    {
        PageChips.Clear();

        var totalPages = TotalPages;
        if (totalPages <= 0)
            return;

        var start = Math.Max(1, CurrentPage - 1);
        var end = Math.Min(totalPages, start + 2);

        if (end - start < 2)
            start = Math.Max(1, end - 2);

        for (var page = start; page <= end; page++)
            PageChips.Add(new PageChipVm(page, page == CurrentPage));
    }

    private AllItemEntry ToEntry(VaultItemSummary summary)
        => new(
            _root.Localization,
            summary.Id,
            summary.Type,
            summary.Title,
            summary.Subtitle,
            summary.Identifier,
            summary.Labels,
            summary.SearchText,
            summary.Favorite,
            summary.CreatedAtUtc,
            summary.UpdatedAtUtc,
            summary.CopyValue,
            summary.ExpiryMonth,
            summary.ExpiryYear);

    private static string SortModeToQueryValue(AllItemsSortMode mode)
        => mode switch
        {
            AllItemsSortMode.Alphabetical => ItemListSortModes.Alphabetical,
            AllItemsSortMode.TypeThenTitle => ItemListSortModes.TypeThenTitle,
            _ => ItemListSortModes.UpdatedDescending
        };
}
