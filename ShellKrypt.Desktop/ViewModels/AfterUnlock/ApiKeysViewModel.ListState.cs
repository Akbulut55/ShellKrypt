using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
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

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_all.Count == 0)
            await LoadAsync();

        var row = _all.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (row is null)
            return false;

        SearchText = "";
        SelectedEnvironmentFilter = AllEnvironmentFilter;
        SelectedProviderFilter = AllProviderFilter;
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

        if (_root.VaultPath is null)
        {
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        try
        {
            _all.Clear();
            Rows.Clear();

            var entries = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
            _all.AddRange(entries.Select(entry => new ApiKeyRowVm(entry, _root.Localization)));

            RefreshProviderFilters();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ApplyFilter() => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        IEnumerable<ApiKeyRowVm> filtered = _all;
        var query = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(SelectedEnvironmentFilter) &&
            !string.Equals(SelectedEnvironmentFilter, AllEnvironmentFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row => string.Equals(row.EnvironmentDisplay, SelectedEnvironmentFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedProviderFilter) &&
            !string.Equals(SelectedProviderFilter, AllProviderFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row => string.Equals(row.ProviderDisplay, SelectedProviderFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(row => row.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = SelectedSortOption switch
        {
            SortProvider => filtered
                .OrderBy(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SortAlphabetical => filtered.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(row => ParseTimestamp(row.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = DesktopPagination.ClampPage(CurrentPage, _filtered.Count, PageSize);

        RenderPage();
        NotifySummaryChanged();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var row in DesktopPagination.Page(_filtered, CurrentPage, PageSize))
            Rows.Add(row);

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void RefreshProviderFilters()
    {
        var previousEnvironment = SelectedEnvironmentFilter;
        var previous = SelectedProviderFilter;
        DesktopFilterOptions.RebuildStringOptions(EnvironmentFilters, AllEnvironmentFilter, _all.Select(row => row.EnvironmentDisplay));
        DesktopFilterOptions.RebuildStringOptions(ProviderFilters, AllProviderFilter, _all.Select(row => row.ProviderDisplay));

        SelectedEnvironmentFilter = DesktopFilterOptions.KeepSelectedOrDefault(EnvironmentFilters, previousEnvironment, AllEnvironmentFilter);
        SelectedProviderFilter = DesktopFilterOptions.KeepSelectedOrDefault(ProviderFilters, previous, AllProviderFilter);
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SensitiveFieldCount));
        OnPropertyChanged(nameof(ProviderCount));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
}
