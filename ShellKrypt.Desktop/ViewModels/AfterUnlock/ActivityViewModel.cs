using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ActivityItemVm : ObservableObject
{
    public ActivityItemVm(ActivityLogEntry entry)
    {
        Entry = entry;
    }

    public ActivityLogEntry Entry { get; }
    public string Id => Entry.Id;
    public string Category => Entry.Category;
    public string Title => Entry.Title;
    public string Detail => Entry.Detail;
    public string Severity => Entry.Severity;
    public string VaultDisplay => string.IsNullOrWhiteSpace(Entry.VaultPath) ? "ShellKrypt" : Path.GetFileNameWithoutExtension(Entry.VaultPath);
    public string SessionIdDisplay => $"SES-{Id[..4].ToUpperInvariant()}";
    public string TimestampColumnDisplay => FormatColumnTimestamp(Entry.TimestampUtc);
    public string AffectedItemDisplay => !string.IsNullOrWhiteSpace(Entry.AffectedItem)
        ? Entry.AffectedItem
        : string.IsNullOrWhiteSpace(Entry.VaultPath) ? Detail : VaultDisplay;
    public string CategoryLabel => Entry.Category switch
    {
        "vault" => "Vault",
        "web" => "Web Logins",
        "cards" => "Credit Cards",
        "notes" => "Markdown Notes",
        "authenticator" => "Authenticator",
        "api_keys" => "API Keys",
        "audit" => "Security Audit",
        "generator" => "Generator",
        "settings" => "Settings",
        "transfer" => "Export",
        "activity" => "Activity Logs",
        _ => "System"
    };
    public string TimestampDisplay => FormatTimestamp(Entry.TimestampUtc);
    public string SeverityChipText => Entry.Severity switch
    {
        "warning" => "Warning",
        "success" => "Success",
        "danger" => "Danger",
        _ => "Info"
    };
    public string SeverityForeground => Entry.Severity switch
    {
        "warning" => "WarningForegroundBrush",
        "success" => "SuccessForegroundBrush",
        "danger" => "DangerBrush",
        _ => "InfoBrush"
    };
    public string SeverityBackground => Entry.Severity switch
    {
        "warning" => "WarningMutedBrush",
        "success" => "SuccessMutedBrush",
        "danger" => "DangerMutedBrush",
        _ => "InfoMutedBrush"
    };
    public string IconGlyph => Entry.Category switch
    {
        "vault" => "VA",
        "web" => "WB",
        "cards" => "CC",
        "notes" => "MD",
        "authenticator" => "AU",
        "api_keys" => "AK",
        "audit" => "SE",
        "generator" => "GE",
        "settings" => "ST",
        "transfer" => "IO",
        "activity" => "AC",
        _ => "SY"
    };

    private static string FormatColumnTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "--:--:--";

        return parsed.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return local.ToString("MMM d, yyyy • HH:mm", CultureInfo.InvariantCulture);
    }
}

public partial class ActivityViewModel : ViewModelBase
{
    private const int PageSize = 8;

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true
    };

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

    [RelayCommand]
    private void ShowAll() => ActiveCategory = "all";

    [RelayCommand]
    private void ShowVault() => ActiveCategory = "vault";

    [RelayCommand]
    private void ShowItems() => ActiveCategory = "items";

    [RelayCommand]
    private void ShowAudit() => ActiveCategory = "audit";

    [RelayCommand]
    private void ShowSettings() => ActiveCategory = "settings";

    [RelayCommand]
    private void ShowTransfer() => ActiveCategory = "transfer";

    [RelayCommand]
    private void Refresh() => ReloadFromStore();

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void PreviousPage()
    {
        if (CanGoPreviousPage)
            CurrentPage--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage()
    {
        if (CanGoNextPage)
            CurrentPage++;
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        Error = string.Empty;

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Clear Activity Log?",
            "Clear this vault's activity history?",
            "This only deletes activity entries for the current vault. Vault items remain untouched.",
            "Clear Activity");

        if (!confirmed)
            return;

        _store.Clear(_root.VaultPath, _root.IsUnlocked ? _root.VaultKey : null);
        ReloadFromStore();
        _root.LogActivity("activity", "Activity logs cleared", "The current vault activity feed was cleared.", "warning", affectedItem: CurrentVaultDisplayName);
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        Error = string.Empty;

        if (_allItems.Count == 0)
        {
            Error = "No activity logs to export.";
            return;
        }

        var suggestedName = $"ShellKrypt-{SanitizeFileName(CurrentVaultDisplayName)}-activity-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json";
        var exportPath = await _root.PickSaveFileAsync(
            "Export activity logs report",
            suggestedName,
            ".json",
            [".json"],
            "JSON report");

        if (string.IsNullOrWhiteSpace(exportPath))
            return;

        await File.WriteAllTextAsync(exportPath, BuildActivityReportJson());
        Error = "Activity report exported as plaintext JSON. Protect it and delete it when finished.";
        _root.LogActivity("activity", "Activity report exported", $"Saved {_allItems.Count} activity log entries to {Path.GetFileName(exportPath)}.", "info", affectedItem: Path.GetFileName(exportPath));
    }

    public void ReloadFromStore()
    {
        try
        {
            Error = string.Empty;
            _allItems.Clear();
            foreach (var entry in _store.Load(_root.VaultPath, _root.IsUnlocked ? _root.VaultKey : null).OrderByDescending(x => x.TimestampUtc, StringComparer.Ordinal))
                _allItems.Add(new ActivityItemVm(entry));

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

        IEnumerable<ActivityItemVm> items = _allItems;

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

        var targetPage = resetPage ? 1 : DesktopPagination.ClampPage(CurrentPage, _filteredItems.Count, PageSize);
        if (CurrentPage != targetPage)
            CurrentPage = targetPage;
        else
            RenderPage();

        OnPropertyChanged(nameof(FilteredEventCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void RenderPage()
    {
        Items.Clear();

        foreach (var item in DesktopPagination.Page(_filteredItems, CurrentPage, PageSize))
            Items.Add(item);

        SelectedItem = Items.FirstOrDefault();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private static bool IsToday(string timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
           && parsed.ToLocalTime().Date == DateTime.Now.Date;

    private static string FormatMetadataTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        return parsed.ToLocalTime().ToString("MMM d, yyyy | HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string ComputeIntegrityHash(ActivityLogEntry entry)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{entry.Id}|{entry.TimestampUtc}|{entry.Category}|{entry.Title}|{entry.Detail}|{entry.Severity}|{entry.VaultPath}|{entry.AffectedItem}");
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static bool IsItemCategory(string category)
        => category is "web" or "cards" or "api_keys" or "authenticator" or "notes";

    private string BuildActivityReportJson()
    {
        var report = new ActivityLogReport(
            ReportType: "ShellKrypt Plaintext Activity Logs Report",
            Vault: CurrentVaultDisplayName,
            GeneratedAt: DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            TotalEvents: _allItems.Count,
            Events: _allItems
                .OrderByDescending(item => item.Entry.TimestampUtc, StringComparer.Ordinal)
                .Select(item => new ActivityLogReportEvent(
                    Id: item.Id,
                    TimestampUtc: item.Entry.TimestampUtc,
                    TimestampLocal: FormatMetadataTimestamp(item.Entry.TimestampUtc),
                    Category: item.CategoryLabel,
                    Status: item.SeverityChipText,
                    Event: item.Title,
                    AffectedItem: item.AffectedItemDisplay,
                    Detail: item.Detail,
                    IntegrityHash: ComputeIntegrityHash(item.Entry)))
                .ToArray());

        return JsonSerializer.Serialize(report, ReportJsonOptions);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "vault" : sanitized;
    }

    private string CurrentVaultDisplayName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Current vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);

    private sealed record ActivityLogReport(
        string ReportType,
        string Vault,
        string GeneratedAt,
        int TotalEvents,
        IReadOnlyList<ActivityLogReportEvent> Events);

    private sealed record ActivityLogReportEvent(
        string Id,
        string TimestampUtc,
        string TimestampLocal,
        string Category,
        string Status,
        string Event,
        string AffectedItem,
        string Detail,
        string IntegrityHash);
}
