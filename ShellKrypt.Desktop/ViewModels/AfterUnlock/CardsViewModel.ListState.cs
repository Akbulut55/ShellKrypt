using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_all.Count == 0)
            await LoadAsync();

        var row = _all.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (row is null)
        {
            await LoadAsync();
            row = _all.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (row is null)
                return false;
        }

        SearchText = "";
        SelectedBankFilter = AllBankFilter;
        SelectedCardTypeFilter = AllCardTypeFilter;
        SelectedSortOption = SortNewest;
        ApplyFilter();

        var index = _filtered.FindIndex(item => string.Equals(item.Id, row.Id, StringComparison.Ordinal));
        CurrentPage = index < 0 ? 1 : (index / PageSize) + 1;
        RenderPage();
        ShowDetails(row);
        return true;
    }

    private async Task LoadAsync()
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            _all.Clear();
            Rows.Clear();

            var entries = await _cardService.ListAsync(_root.VaultPath, _root.VaultKey);
            _all.AddRange(entries.Select(ToRow));

            RefreshCardFilters();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void PreviousPage()
    {
        if (!CanGoPreviousPage)
            return;

        CurrentPage--;
        RenderPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage()
    {
        if (!CanGoNextPage)
            return;

        CurrentPage++;
        RenderPage();
    }

    private void ApplyFilter() => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        IEnumerable<CardRowVm> filtered = _all;
        var q = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(SelectedBankFilter) &&
            !string.Equals(SelectedBankFilter, AllBankFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.BankDisplay, SelectedBankFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedCardTypeFilter) &&
            !string.Equals(SelectedCardTypeFilter, AllCardTypeFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.CardType, SelectedCardTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Bank ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Cardholder ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.NumberDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.IssuerDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.CardType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.ExpiryDisplay.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedSortOption switch
        {
            SortExpiry => filtered.OrderBy(GetExpirySortKey),
            SortAlphabetical => filtered.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(r => ParseTimestamp(r.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = DesktopPagination.ClampPage(CurrentPage, _filtered.Count, PageSize);

        RenderPage();
        NotifyCardSummaryChanged();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var row in DesktopPagination.Page(_filtered, CurrentPage, PageSize))
            Rows.Add(row);

        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCardSummaryChanged()
    {
        OnPropertyChanged(nameof(ActiveCardsCount));
        OnPropertyChanged(nameof(ExpiringSoonCount));
        OnPropertyChanged(nameof(ExpiredCardsCount));
        OnPropertyChanged(nameof(ActiveCardsSummary));
        OnPropertyChanged(nameof(ExpiringSoonSummary));
        OnPropertyChanged(nameof(ExpiredCardsSummary));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private void RefreshCardFilters()
    {
        var selectedBank = SelectedBankFilter;
        var selectedType = SelectedCardTypeFilter;

        DesktopFilterOptions.RebuildStringOptions(BankFilters, AllBankFilter, _all.Select(row => row.BankDisplay));
        DesktopFilterOptions.RebuildStringOptions(CardTypeFilters, AllCardTypeFilter, CardTypeOptions.Concat(_all.Select(row => row.CardType)));

        SelectedBankFilter = DesktopFilterOptions.KeepSelectedOrDefault(BankFilters, selectedBank, AllBankFilter);
        SelectedCardTypeFilter = DesktopFilterOptions.KeepSelectedOrDefault(CardTypeFilters, selectedType, AllCardTypeFilter);
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static DateTime GetExpirySortKey(CardRowVm row)
    {
        if (!int.TryParse(row.ExpiryMonth, out var month) || month is < 1 or > 12)
            return DateTime.MaxValue;
        if (!int.TryParse(row.ExpiryYear, out var year))
            return DateTime.MaxValue;
        if (year < 100)
            year += 2000;

        return new DateTime(year, month, DateTime.DaysInMonth(year, month));
    }
}
