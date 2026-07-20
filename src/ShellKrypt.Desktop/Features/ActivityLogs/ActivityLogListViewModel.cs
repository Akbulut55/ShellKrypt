using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed partial class ActivityLogListViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly TimeProvider _timeProvider;
    private readonly List<ActivityItemVm> _allItems = [];
    private readonly List<ActivityItemVm> _filteredItems = [];

    [ObservableProperty] private ActivityItemVm? selectedItem;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string activeCategory = "all";
    [ObservableProperty] private ActivityFilterOptionVm? selectedSeverityOption;
    [ObservableProperty] private ActivityFilterOptionVm? selectedDateRangeOption;
    [ObservableProperty] private ActivityFilterOptionVm? selectedSortOption;

    public ActivityLogListViewModel(LocalizationService localization, TimeProvider timeProvider)
    {
        _localization = localization;
        _timeProvider = timeProvider;
        SeverityOptions =
        [
            new("all", "Activity.Filter.AllSeverities", localization),
            new("info", "Activity.Severity.Info", localization),
            new("success", "Activity.Severity.Success", localization),
            new("warning", "Activity.Severity.Warning", localization),
            new("danger", "Activity.Severity.Danger", localization)
        ];
        DateRangeOptions =
        [
            new("all", "Activity.DateRange.All", localization),
            new("today", "Activity.DateRange.Today", localization),
            new("last7", "Activity.DateRange.Last7Days", localization),
            new("last30", "Activity.DateRange.Last30Days", localization)
        ];
        SortOptions =
        [
            new("newest", "Activity.Sort.Newest", localization),
            new("oldest", "Activity.Sort.Oldest", localization)
        ];
        selectedSeverityOption = SeverityOptions[0];
        selectedDateRangeOption = DateRangeOptions[0];
        selectedSortOption = SortOptions[0];
    }

    public ObservableCollection<ActivityItemVm> Items { get; } = [];
    public IReadOnlyList<ActivityFilterOptionVm> SeverityOptions { get; }
    public IReadOnlyList<ActivityFilterOptionVm> DateRangeOptions { get; }
    public IReadOnlyList<ActivityFilterOptionVm> SortOptions { get; }
    public IReadOnlyList<ActivityItemVm> FilteredItems => _filteredItems;
    public IReadOnlyList<ActivityItemVm> AllItemsInSelectedSortOrder => Sort(_allItems).ToArray();
    public int TotalEvents => _allItems.Count;
    public int FilteredEventCount => _filteredItems.Count;
    public bool HasStoredItems => _allItems.Count > 0;
    public bool HasVisibleItems => _filteredItems.Count > 0;
    public bool HasNarrowingFilter => !string.IsNullOrWhiteSpace(SearchText)
        || ActiveCategory != "all"
        || SelectedSeverityOption?.Id != "all"
        || SelectedDateRangeOption?.Id != "all";
    public string ItemsSummary => _localization.Get("Activity.ItemsSummary", FilteredEventCount, TotalEvents);
    public bool IsAllFilterActive => ActiveCategory == "all";
    public bool IsVaultFilterActive => ActiveCategory == "vault";
    public bool IsItemsFilterActive => ActiveCategory == "items";
    public bool IsAuditFilterActive => ActiveCategory == "audit";
    public bool IsSettingsFilterActive => ActiveCategory == "settings";
    public bool IsTransferFilterActive => ActiveCategory == "transfer";
    public string EmptyStateTitle => HasStoredItems ? _localization.Get("Activity.Empty.NoMatchTitle") : _localization.Get("Activity.Empty.NoneTitle");
    public string EmptyStateSubtitle => HasStoredItems ? _localization.Get("Activity.Empty.NoMatchSubtitle") : _localization.Get("Activity.Empty.NoneSubtitle");
    public ActivityAppliedFilters AppliedFilters => new(
        ActiveCategory,
        SelectedSeverityOption?.Id ?? "all",
        SelectedDateRangeOption?.Id ?? "all",
        SelectedSortOption?.Id ?? "newest",
        !string.IsNullOrWhiteSpace(SearchText));

    public event EventHandler? FilteredItemsChanged;

    public void Load(IReadOnlyList<ActivityLogEntry> entries)
    {
        var selectedId = SelectedItem?.Id;
        _allItems.Clear();
        _allItems.AddRange(entries.Select(entry => new ActivityItemVm(entry, _localization, _timeProvider)));
        ApplyFilter(selectedId);
        NotifyCounts();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(SelectedItem?.Id);
    partial void OnSelectedSeverityOptionChanged(ActivityFilterOptionVm? value) => ApplyFilter(SelectedItem?.Id);
    partial void OnSelectedDateRangeOptionChanged(ActivityFilterOptionVm? value) => ApplyFilter(SelectedItem?.Id);
    partial void OnSelectedSortOptionChanged(ActivityFilterOptionVm? value) => ApplyFilter(SelectedItem?.Id);

    partial void OnActiveCategoryChanged(string value)
    {
        NotifyCategoryState();
        ApplyFilter(SelectedItem?.Id);
    }

    [RelayCommand] private void ShowAll() => ActiveCategory = "all";
    [RelayCommand] private void ShowVault() => ActiveCategory = "vault";
    [RelayCommand] private void ShowItems() => ActiveCategory = "items";
    [RelayCommand] private void ShowAudit() => ActiveCategory = "audit";
    [RelayCommand] private void ShowSettings() => ActiveCategory = "settings";
    [RelayCommand] private void ShowTransfer() => ActiveCategory = "transfer";

    public void RefreshLocalization()
    {
        foreach (var item in _allItems)
            item.RefreshLocalization();
        foreach (var option in SeverityOptions.Concat(DateRangeOptions).Concat(SortOptions))
            option.RefreshLocalization();
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private void ApplyFilter(string? preferredSelectionId)
    {
        IEnumerable<ActivityItemVm> query = _allItems;
        if (ActiveCategory == "items")
            query = query.Where(item => IsItemCategory(item.Category));
        else if (ActiveCategory != "all")
            query = query.Where(item => item.Category == ActiveCategory);

        if (SelectedSeverityOption?.Id is { } severity and not "all")
            query = query.Where(item => item.Severity == severity);
        if (SelectedDateRangeOption?.Id is { } dateRange and not "all")
            query = query.Where(item => IsInDateRange(item.Entry.TimestampUtc, dateRange));
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            query = query.Where(item => item.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.Detail.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.AffectedItemDisplay.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.VaultDisplay.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        query = Sort(query);

        _filteredItems.Clear();
        _filteredItems.AddRange(query);
        Items.Clear();
        foreach (var item in _filteredItems)
            Items.Add(item);

        SelectedItem = preferredSelectionId is null ? Items.FirstOrDefault() : Items.FirstOrDefault(item => item.Id == preferredSelectionId) ?? Items.FirstOrDefault();
        NotifyCounts();
        FilteredItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsInDateRange(string timestampUtc, string range)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            return false;
        var localNow = _timeProvider.GetLocalNow();
        var localTimestamp = TimeZoneInfo.ConvertTime(timestamp, _timeProvider.LocalTimeZone);
        var startDate = range switch
        {
            "today" => localNow.Date,
            "last7" => localNow.Date.AddDays(-6),
            "last30" => localNow.Date.AddDays(-29),
            _ => DateTime.MinValue
        };
        var start = new DateTimeOffset(startDate, _timeProvider.LocalTimeZone.GetUtcOffset(startDate));
        return localTimestamp >= start && localTimestamp <= localNow;
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(TotalEvents));
        OnPropertyChanged(nameof(FilteredEventCount));
        OnPropertyChanged(nameof(HasStoredItems));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(HasNarrowingFilter));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
        OnPropertyChanged(nameof(AppliedFilters));
    }

    private void NotifyCategoryState()
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsVaultFilterActive));
        OnPropertyChanged(nameof(IsItemsFilterActive));
        OnPropertyChanged(nameof(IsAuditFilterActive));
        OnPropertyChanged(nameof(IsSettingsFilterActive));
        OnPropertyChanged(nameof(IsTransferFilterActive));
    }

    private static bool IsItemCategory(string category)
        => category is "web" or "cards" or "api_keys" or "authenticator" or "notes" or "project_secrets";

    private IOrderedEnumerable<ActivityItemVm> Sort(IEnumerable<ActivityItemVm> items)
        => SelectedSortOption?.Id == "oldest"
            ? items.OrderBy(item => item.Entry.TimestampUtc, StringComparer.Ordinal)
            : items.OrderByDescending(item => item.Entry.TimestampUtc, StringComparer.Ordinal);
}
