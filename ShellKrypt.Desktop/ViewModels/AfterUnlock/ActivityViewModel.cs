using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private const int PageSize = 8;

    private readonly MainWindowViewModel _root;
    private readonly ActivityLogService _store;
    private readonly List<ActivityItemVm> _allItems = new();
    private readonly List<ActivityItemVm> _filteredItems = new();

    public ObservableCollection<ActivityItemVm> Items { get; } = new();

    [ObservableProperty] private ActivityItemVm? selectedItem;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeCategory = "all";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int currentPage = 1;

    public ActivityViewModel(MainWindowViewModel root, ActivityLogService store)
    {
        _root = root;
        _store = store;
        _root.ActivityChanged += (_, _) => ReloadFromStore();
        ReloadFromStore();
    }

    public bool HasItems => Items.Count > 0;
    public bool HasFilteredItems => _filteredItems.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int TotalEvents => _allItems.Count;
    public int FilteredEventCount => _filteredItems.Count;
    public int TotalPages => DesktopPagination.GetTotalPages(_filteredItems.Count, PageSize);
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public string ItemsSummary => $"Showing {Items.Count} of {FilteredEventCount} events";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public int TodayCount => _allItems.Count(item => IsToday(item.Entry.TimestampUtc));
    public int WarningCount => _allItems.Count(item => item.Severity is "warning" or "danger");
    public int VaultEventCount => _allItems.Count(item => string.Equals(item.Category, "vault", StringComparison.Ordinal));
    public bool IsAllFilterActive => ActiveCategory == "all";
    public bool IsVaultFilterActive => ActiveCategory == "vault";
    public bool IsItemsFilterActive => ActiveCategory == "items";
    public bool IsAuditFilterActive => ActiveCategory == "audit";
    public bool IsSettingsFilterActive => ActiveCategory == "settings";
    public bool IsTransferFilterActive => ActiveCategory == "transfer";
    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No activity recorded yet"
        : "No activity matches this search";
    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? "Vault lifecycle actions, item changes, copied secrets, scans, and import or export operations for this vault will appear here."
        : "Try a different term or clear the current activity filter.";
    public bool HasSelectedItem => SelectedItem is not null;
    public string SelectedEventId => SelectedItem?.Id ?? "No event selected";
    public string SelectedTimestamp => SelectedItem is null ? "No timestamp" : FormatMetadataTimestamp(SelectedItem.Entry.TimestampUtc);
    public string SelectedCategory => SelectedItem?.CategoryLabel ?? "System";
    public string SelectedStatus => SelectedItem?.SeverityChipText ?? "Info";
    public string SelectedStatusForeground => SelectedItem?.SeverityForeground ?? "InfoBrush";
    public string SelectedStatusBackground => SelectedItem?.SeverityBackground ?? "InfoMutedBrush";
    public string SelectedAffectedItem => SelectedItem?.AffectedItemDisplay ?? "No item selected";
    public string SelectedVaultPath => SelectedItem?.Entry.VaultPath ?? "ShellKrypt local session";
    public string SelectedDetail => SelectedItem?.Detail ?? "Select an event to inspect its metadata.";
    public string SelectedIntegrityHash => SelectedItem is null ? "Unavailable" : ComputeIntegrityHash(SelectedItem.Entry);

    private string CurrentVaultDisplayName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Current vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);

    partial void OnSearchTextChanged(string value) => ApplyFilter(resetPage: true);

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnCurrentPageChanged(int value)
    {
        RenderPage();
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedItemChanged(ActivityItemVm? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(SelectedEventId));
        OnPropertyChanged(nameof(SelectedTimestamp));
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(SelectedStatus));
        OnPropertyChanged(nameof(SelectedStatusForeground));
        OnPropertyChanged(nameof(SelectedStatusBackground));
        OnPropertyChanged(nameof(SelectedAffectedItem));
        OnPropertyChanged(nameof(SelectedVaultPath));
        OnPropertyChanged(nameof(SelectedDetail));
        OnPropertyChanged(nameof(SelectedIntegrityHash));
    }

    partial void OnActiveCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsVaultFilterActive));
        OnPropertyChanged(nameof(IsItemsFilterActive));
        OnPropertyChanged(nameof(IsAuditFilterActive));
        OnPropertyChanged(nameof(IsSettingsFilterActive));
        OnPropertyChanged(nameof(IsTransferFilterActive));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
        ApplyFilter(resetPage: true);
    }
}
