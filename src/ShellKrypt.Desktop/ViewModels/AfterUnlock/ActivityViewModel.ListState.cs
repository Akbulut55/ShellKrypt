using System;
using System.Globalization;
using System.Linq;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel
{
    public void ReloadFromStore()
    {
        try
        {
            Error = string.Empty;
            _allItems.Clear();
            foreach (var entry in _store.Load(_root.VaultPath, _root.IsUnlocked ? _root.VaultKey : null).OrderByDescending(x => x.TimestampUtc, StringComparer.Ordinal))
                _allItems.Add(new ActivityItemVm(entry, _root.Localization));

            ApplyFilter(resetPage: false);
            OnPropertyChanged(nameof(TotalEvents));
            OnPropertyChanged(nameof(TodayCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(VaultEventCount));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ApplyFilter(bool resetPage)
    {
        _filteredItems.Clear();

        var items = _allItems.AsEnumerable();

        if (string.Equals(ActiveCategory, "items", StringComparison.Ordinal))
            items = items.Where(item => IsItemCategory(item.Category));
        else if (!string.Equals(ActiveCategory, "all", StringComparison.Ordinal))
            items = items.Where(item => string.Equals(item.Category, ActiveCategory, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            items = items.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Detail.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.VaultDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _filteredItems.AddRange(items);

        RenderItems();

        OnPropertyChanged(nameof(FilteredEventCount));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private void RenderItems()
    {
        Items.Clear();

        foreach (var item in _filteredItems)
            Items.Add(item);

        SelectedItem = Items.FirstOrDefault();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private static bool IsToday(string timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
           && parsed.ToLocalTime().Date == DateTime.Now.Date;

    private static bool IsItemCategory(string category)
        => category is "web" or "cards" or "api_keys" or "authenticator" or "notes";
}
