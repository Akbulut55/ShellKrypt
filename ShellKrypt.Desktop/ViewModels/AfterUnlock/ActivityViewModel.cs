using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Services;

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
    public string AffectedItemDisplay => string.IsNullOrWhiteSpace(Entry.VaultPath) ? Detail : VaultDisplay;
    public string CategoryLabel => Entry.Category switch
    {
        "vault" => "Vault",
        "notes" => "Markdown Notes",
        "authenticator" => "Authenticator",
        "settings" => "Settings",
        "transfer" => "Export",
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
        "warning" => "#FFD1AA",
        "success" => "#57F1DB",
        "danger" => "#FFB4AB",
        _ => "#BACAC5"
    };
    public string SeverityBackground => Entry.Severity switch
    {
        "warning" => "#3A3228",
        "success" => "#174544",
        "danger" => "#3A2426",
        _ => "#2A2A2A"
    };
    public string IconGlyph => Entry.Category switch
    {
        "vault" => "VA",
        "notes" => "MD",
        "authenticator" => "AU",
        "settings" => "ST",
        "transfer" => "IO",
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
    private readonly MainWindowViewModel _root;
    private readonly ActivityLogStore _store;
    private readonly List<ActivityItemVm> _allItems = new();

    public ObservableCollection<ActivityItemVm> Items { get; } = new();

    [ObservableProperty] private ActivityItemVm? selectedItem;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string activeCategory = "all";
    [ObservableProperty] private string error = "";

    public ActivityViewModel(MainWindowViewModel root, ActivityLogStore store)
    {
        _root = root;
        _store = store;
        _root.ActivityChanged += (_, _) => ReloadFromStore();
        ReloadFromStore();
    }

    public bool HasItems => Items.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int TotalEvents => _allItems.Count;
    public int TodayCount => _allItems.Count(item => IsToday(item.Entry.TimestampUtc));
    public int WarningCount => _allItems.Count(item => item.Severity is "warning" or "danger");
    public int VaultEventCount => _allItems.Count(item => string.Equals(item.Category, "vault", StringComparison.Ordinal));
    public bool IsAllFilterActive => ActiveCategory == "all";
    public bool IsVaultFilterActive => ActiveCategory == "vault";
    public bool IsNotesFilterActive => ActiveCategory == "notes";
    public bool IsSettingsFilterActive => ActiveCategory == "settings";
    public bool IsTransferFilterActive => ActiveCategory == "transfer";
    public string EmptyStateTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No activity recorded yet"
        : "No activity matches this search";
    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? "Vault lifecycle actions, note changes, and import or export operations will appear here."
        : "Try a different term or clear the current activity filter.";
    public bool HasSelectedItem => SelectedItem is not null;
    public string SelectedEventId => SelectedItem?.Id ?? "No event selected";
    public string SelectedTimestamp => SelectedItem is null ? "No timestamp" : FormatMetadataTimestamp(SelectedItem.Entry.TimestampUtc);
    public string SelectedCategory => SelectedItem?.CategoryLabel ?? "System";
    public string SelectedStatus => SelectedItem?.SeverityChipText ?? "Info";
    public string SelectedStatusForeground => SelectedItem?.SeverityForeground ?? "#BACAC5";
    public string SelectedStatusBackground => SelectedItem?.SeverityBackground ?? "#2A2A2A";
    public string SelectedAffectedItem => SelectedItem?.AffectedItemDisplay ?? "No item selected";
    public string SelectedVaultPath => SelectedItem?.Entry.VaultPath ?? "ShellKrypt local session";
    public string SelectedDetail => SelectedItem?.Detail ?? "Select an event to inspect its metadata.";
    public string SelectedIntegrityHash => SelectedItem is null ? "Unavailable" : ComputeIntegrityHash(SelectedItem.Entry);
    public string SecurityNoteText => "Auditing is enabled locally. Activity logs stay on this device only.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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
        OnPropertyChanged(nameof(IsNotesFilterActive));
        OnPropertyChanged(nameof(IsSettingsFilterActive));
        OnPropertyChanged(nameof(IsTransferFilterActive));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowAll() => ActiveCategory = "all";

    [RelayCommand]
    private void ShowVault() => ActiveCategory = "vault";

    [RelayCommand]
    private void ShowNotes() => ActiveCategory = "notes";

    [RelayCommand]
    private void ShowSettings() => ActiveCategory = "settings";

    [RelayCommand]
    private void ShowTransfer() => ActiveCategory = "transfer";

    [RelayCommand]
    private void Refresh() => ReloadFromStore();

    [RelayCommand]
    private async Task ClearAsync()
    {
        Error = string.Empty;

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Clear Activity Log?",
            "Clear the local activity history?",
            "This only deletes the local activity feed. Vault items remain untouched.",
            "Clear Activity");

        if (!confirmed)
            return;

        _store.Clear();
        ReloadFromStore();
        _root.LogActivity("system", "Activity log cleared", "The local activity feed was cleared.", "warning");
    }

    [RelayCommand]
    private async Task ExportSelectedAsync()
    {
        if (SelectedItem is null)
            return;

        var payload =
            $"Event ID: {SelectedEventId}{Environment.NewLine}" +
            $"Timestamp: {SelectedTimestamp}{Environment.NewLine}" +
            $"Category: {SelectedCategory}{Environment.NewLine}" +
            $"Status: {SelectedStatus}{Environment.NewLine}" +
            $"Affected Item: {SelectedAffectedItem}{Environment.NewLine}" +
            $"Vault Path: {SelectedVaultPath}{Environment.NewLine}" +
            $"Detail: {SelectedDetail}{Environment.NewLine}" +
            $"Integrity Hash: {SelectedIntegrityHash}";

        await _root.CopyToClipboardAsync(payload);
        _root.LogActivity("settings", "Activity report copied", $"Copied metadata for {SelectedItem.Title}.", "info");
    }

    public void ReloadFromStore()
    {
        try
        {
            Error = string.Empty;
            _allItems.Clear();
            foreach (var entry in _store.Load().OrderByDescending(x => x.TimestampUtc, StringComparer.Ordinal))
                _allItems.Add(new ActivityItemVm(entry));

            ApplyFilter();
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

    private void ApplyFilter()
    {
        Items.Clear();

        IEnumerable<ActivityItemVm> items = _allItems;

        if (!string.Equals(ActiveCategory, "all", StringComparison.Ordinal))
            items = items.Where(item => string.Equals(item.Category, ActiveCategory, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            items = items.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Detail.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.VaultDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
            Items.Add(item);

        SelectedItem = Items.FirstOrDefault();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
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
        var bytes = Encoding.UTF8.GetBytes($"{entry.Id}|{entry.TimestampUtc}|{entry.Category}|{entry.Title}|{entry.Detail}|{entry.Severity}|{entry.VaultPath}");
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
