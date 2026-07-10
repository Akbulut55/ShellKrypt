using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
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
        SelectedProviderFilter = AllProviderFilter;
        SelectedSortOption = SortNewest;
        ApplyFilter();

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

        if (!string.IsNullOrWhiteSpace(SelectedProviderFilter) &&
            !string.Equals(SelectedProviderFilter, AllProviderFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row => string.Equals(row.ProviderDisplay, SelectedProviderFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(row => row.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = SelectedSortOption switch
        {
            SortOldest => filtered.OrderBy(row => ParseTimestamp(row.UpdatedAtUtc)),
            SortNameAscending => filtered.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SortNameDescending => filtered.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SortProviderAscending => filtered
                .OrderBy(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SortProviderDescending => filtered
                .OrderByDescending(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(row => ParseTimestamp(row.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        RenderPage();
        NotifySummaryChanged();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var row in _filtered)
            Rows.Add(row);

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private void RefreshProviderFilters()
    {
        var previous = SelectedProviderFilter;
        DesktopFilterOptions.RebuildStringOptions(ProviderFilters, AllProviderFilter, _all.Select(row => row.ProviderDisplay));

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
