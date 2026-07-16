using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _root;
    private readonly ActivityLogService _store;
    private readonly List<ActivityItemVm> _allItems = new();
    private readonly List<ActivityItemVm> _filteredItems = new();

    public ObservableCollection<ActivityItemVm> Items { get; } = new();

    [ObservableProperty] private ActivityItemVm? selectedItem;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeCategory = "all";
    [ObservableProperty] private string error = "";

    public ActivityViewModel(DesktopFeatureServices root, ActivityLogService store)
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
    public string ItemsSummary => T(_root, "Activity.ItemsSummary", FilteredEventCount);
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
        ? T(_root, "Activity.Empty.NoneTitle")
        : T(_root, "Activity.Empty.NoMatchTitle");
    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "Activity.Empty.NoneSubtitle")
        : T(_root, "Activity.Empty.NoMatchSubtitle");
    public bool HasSelectedItem => SelectedItem is not null;
    public string SelectedEventId => SelectedItem?.Id ?? T(_root, "Activity.Metadata.NoEvent");
    public string SelectedTimestamp => SelectedItem is null ? T(_root, "Activity.Metadata.NoTimestamp") : FormatMetadataTimestamp(SelectedItem.Entry.TimestampUtc);
    public string SelectedCategory => SelectedItem?.CategoryLabel ?? T(_root, "Activity.Category.System");
    public string SelectedStatus => SelectedItem?.SeverityChipText ?? T(_root, "Activity.Severity.Info");
    public string SelectedStatusForeground => SelectedItem?.SeverityForeground ?? "InfoBrush";
    public string SelectedStatusBackground => SelectedItem?.SeverityBackground ?? "InfoMutedBrush";
    public string SelectedAffectedItem => SelectedItem?.AffectedItemDisplay ?? T(_root, "Activity.Metadata.NoItem");
    public string SelectedVaultPath => SelectedItem?.Entry.VaultPath ?? T(_root, "Activity.Metadata.LocalSession");
    public string SelectedDetail => SelectedItem?.Detail ?? T(_root, "Activity.Metadata.SelectEvent");
    public string SelectedIntegrityHash => SelectedItem is null ? T(_root, "Settings.Profile.Unavailable") : ComputeIntegrityHash(SelectedItem.Entry);

    private string CurrentVaultDisplayName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? T(_root, "Activity.CurrentVault")
        : Path.GetFileNameWithoutExtension(_root.VaultPath);

    partial void OnSearchTextChanged(string value) => ApplyFilter(resetPage: true);

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

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

    public override void RefreshLocalization()
    {
        foreach (var item in _allItems)
            item.RefreshLocalization();

        NotifyLocalized(
            nameof(ItemsSummary),
            nameof(EmptyStateTitle),
            nameof(EmptyStateSubtitle),
            nameof(SelectedEventId),
            nameof(SelectedTimestamp),
            nameof(SelectedCategory),
            nameof(SelectedStatus),
            nameof(SelectedAffectedItem),
            nameof(SelectedVaultPath),
            nameof(SelectedDetail),
            nameof(SelectedIntegrityHash));
    }
}
