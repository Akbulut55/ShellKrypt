using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;

public partial class WebLoginsViewModel : ViewModelBase
{
    internal const string AllUsernameFilterKey = "username:all";
    internal const string AllEmailFilterKey = "email:all";
    internal const string SortNewestKey = "newest";
    internal const string SortOldestKey = "oldest";
    internal const string SortTitleAscendingKey = "title:asc";
    internal const string SortTitleDescendingKey = "title:desc";
    internal const string SortWebsiteAscendingKey = "website:asc";
    internal const string SortWebsiteDescendingKey = "website:desc";

    private readonly DesktopFeatureServices _desktop;
    private readonly IWebLoginService _service;
    private readonly List<WebLoginRowVm> _all = [];
    private readonly List<WebLoginRowVm> _filtered = [];

    public ObservableCollection<WebLoginRowVm> Rows { get; } = [];
    public ObservableCollection<SelectionOptionVm> UsernameFilters { get; } = [];
    public ObservableCollection<SelectionOptionVm> EmailFilters { get; } = [];
    public ObservableCollection<SelectionOptionVm> SortOptions { get; } = [];
    public WebLoginEditorViewModel Editor { get; }

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private SelectionOptionVm? selectedUsernameFilter;
    [ObservableProperty] private SelectionOptionVm? selectedEmailFilter;
    [ObservableProperty] private SelectionOptionVm? selectedSortOption;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isLoading;

    public WebLoginsViewModel(DesktopFeatureServices desktop, IWebLoginService service, IPasswordGenerator passwordGenerator, Func<string?, Task> refreshAllItemsAsync)
    {
        _desktop = desktop;
        _service = service;
        Editor = new WebLoginEditorViewModel(desktop, service, passwordGenerator, HandleMutationAsync, refreshAllItemsAsync);
        RebuildStaticOptions();
        _ = LoadAsync();
    }

    public int TotalLoginsCount => _all.Count;
    public int ReusedPasswordCount => _all.Where(row => !string.IsNullOrWhiteSpace(row.Password)).GroupBy(row => row.Password, StringComparer.Ordinal).Where(group => group.Count() > 1).Sum(group => group.Count());
    public int WeakPasswordCount => _all.Count(row => PasswordIsWeak(row.Password));
    public bool HasReusedPasswords => ReusedPasswordCount > 0;
    public bool HasWeakPasswords => WeakPasswordCount > 0;
    public string ReusedPasswordSummary => T(_desktop.Localization, ReusedPasswordCount == 1 ? "WebLogins.Summary.ReusedOne" : "WebLogins.Summary.ReusedMany", ReusedPasswordCount);
    public string WeakPasswordSummary => T(_desktop.Localization, WeakPasswordCount == 1 ? "WebLogins.Summary.WeakOne" : "WebLogins.Summary.WeakMany", WeakPasswordCount);
    public string ResultText => T(_desktop.Localization, "WebLogins.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool ShowRows => HasRows && !IsLoading && !HasError;
    public bool ShowEmptyState => !HasRows && !IsLoading && !HasError;
    public string EmptyTitle => _all.Count == 0 ? T(_desktop.Localization, "WebLogins.Empty.NoneTitle") : T(_desktop.Localization, "WebLogins.Empty.NoMatchTitle");
    public string EmptySubtitle => _all.Count == 0 ? T(_desktop.Localization, "WebLogins.Empty.NoneSubtitle") : T(_desktop.Localization, "WebLogins.Empty.NoMatchSubtitle");

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedUsernameFilterChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnSelectedEmailFilterChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnErrorChanged(string value) { OnPropertyChanged(nameof(HasError)); NotifyVisibilityState(); }
    partial void OnIsLoadingChanged(bool value) => NotifyVisibilityState();

    [RelayCommand] private void AddNew() { ResetRevealState(); Editor.OpenAdd(); }
    [RelayCommand] private void ShowDetails(WebLoginRowVm? row) { if (row is not null) { ResetRevealState(); Editor.OpenDetails(row); } }
    [RelayCommand] private void TogglePassword(WebLoginRowVm? row) { if (row is not null) row.IsPasswordVisible = !row.IsPasswordVisible; }

    [RelayCommand]
    private async Task CopyPasswordAsync(WebLoginRowVm? row)
    {
        Error = "";
        if (row is null || string.IsNullOrWhiteSpace(row.Password)) { Error = T(_desktop.Localization, "WebLogins.Error.NoPassword"); return; }
        await _desktop.Clipboard.CopyAsync(row.Password);
        _desktop.Activity.Log("web", "Web login password copied", $"Copied password for {row.Title}.", "info", affectedItem: row.Title);
    }

    public async Task<bool> OpenForRemediationAsync(string itemId, bool generateReplacementPassword)
    {
        if (_all.Count == 0) await LoadAsync();
        var row = _all.FirstOrDefault(item => item.Id == itemId);
        if (row is null) { await LoadAsync(); row = _all.FirstOrDefault(item => item.Id == itemId); }
        if (row is null) return false;
        ResetFilters();
        Editor.OpenDetails(row, editImmediately: true, generateReplacementPassword);
        return true;
    }

    private async Task LoadAsync()
    {
        Error = "";
        if (_desktop.Session.VaultPath is null) { Error = T(_desktop.Localization, "Common.NoVaultSelected"); return; }
        IsLoading = true;
        try
        {
            _all.Clear();
            var entries = await _service.ListAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey);
            _all.AddRange(entries.Select(ToRow));
            RefreshDynamicFilters();
            ApplyFilter();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    private Task HandleMutationAsync(WebLoginEntry? entry, string? deletedId)
    {
        if (!string.IsNullOrWhiteSpace(deletedId))
            _all.RemoveAll(row => row.Id == deletedId);
        else if (entry is not null)
        {
            var row = _all.FirstOrDefault(item => item.Id == entry.Id);
            if (row is null) _all.Insert(0, ToRow(entry)); else ApplyEntry(row, entry);
        }
        RefreshDynamicFilters();
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        IEnumerable<WebLoginRowVm> query = _all;
        var username = SelectedUsernameFilter?.Key;
        var email = SelectedEmailFilter?.Key;
        var text = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(username) && username != AllUsernameFilterKey) query = query.Where(row => string.Equals(row.Username, username, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(email) && email != AllEmailFilterKey) query = query.Where(row => string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase));
        if (text.Length > 0) query = query.Where(row => row.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Url.Contains(text, StringComparison.OrdinalIgnoreCase) || row.UrlHost.Contains(text, StringComparison.OrdinalIgnoreCase));
        query = SelectedSortOption?.Key switch
        {
            SortOldestKey => query.OrderBy(row => ParseTimestamp(row.UpdatedAtUtc)),
            SortTitleAscendingKey => query.OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
            SortTitleDescendingKey => query.OrderByDescending(row => row.Title, StringComparer.OrdinalIgnoreCase),
            SortWebsiteAscendingKey => query.OrderBy(row => row.UrlHost, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
            SortWebsiteDescendingKey => query.OrderByDescending(row => row.UrlHost, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(row => ParseTimestamp(row.UpdatedAtUtc))
        };
        _filtered.Clear(); _filtered.AddRange(query);
        Rows.Clear(); foreach (var row in _filtered) Rows.Add(row);
        NotifyListState();
    }

    private void RebuildStaticOptions()
    {
        var selectedKey = SelectedSortOption?.Key ?? SortNewestKey;
        SortOptions.Clear();
        SortOptions.Add(new(SortNewestKey, T(_desktop.Localization, "ItemWorkspace.Sort.Newest"))); SortOptions.Add(new(SortOldestKey, T(_desktop.Localization, "ItemWorkspace.Sort.Oldest")));
        SortOptions.Add(new(SortTitleAscendingKey, T(_desktop.Localization, "ItemWorkspace.Sort.NameAscending"))); SortOptions.Add(new(SortTitleDescendingKey, T(_desktop.Localization, "ItemWorkspace.Sort.NameDescending")));
        SortOptions.Add(new(SortWebsiteAscendingKey, T(_desktop.Localization, "ItemWorkspace.Sort.WebsiteAscending"))); SortOptions.Add(new(SortWebsiteDescendingKey, T(_desktop.Localization, "ItemWorkspace.Sort.WebsiteDescending")));
        SelectedSortOption = SortOptions.First(option => option.Key == selectedKey);
    }

    private void RefreshDynamicFilters()
    {
        var selectedUsername = SelectedUsernameFilter?.Key;
        var selectedEmail = SelectedEmailFilter?.Key;
        UsernameFilters.Clear(); UsernameFilters.Add(new(AllUsernameFilterKey, T(_desktop.Localization, "ItemWorkspace.Filter.UsernameAll")));
        foreach (var value in _all.Select(row => row.Username).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) UsernameFilters.Add(new(value, value));
        EmailFilters.Clear(); EmailFilters.Add(new(AllEmailFilterKey, T(_desktop.Localization, "ItemWorkspace.Filter.EmailAll")));
        foreach (var value in _all.Select(row => row.Email).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) EmailFilters.Add(new(value, value));
        SelectedUsernameFilter = UsernameFilters.FirstOrDefault(item => item.Key == selectedUsername) ?? UsernameFilters[0];
        SelectedEmailFilter = EmailFilters.FirstOrDefault(item => item.Key == selectedEmail) ?? EmailFilters[0];
    }

    private void ResetFilters() { SearchText = ""; SelectedUsernameFilter = UsernameFilters.FirstOrDefault(); SelectedEmailFilter = EmailFilters.FirstOrDefault(); SelectedSortOption = SortOptions.FirstOrDefault(); ApplyFilter(); }
    private void NotifyListState() { OnPropertyChanged(nameof(ResultText)); OnPropertyChanged(nameof(HasRows)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptySubtitle)); OnPropertyChanged(nameof(TotalLoginsCount)); OnPropertyChanged(nameof(ReusedPasswordCount)); OnPropertyChanged(nameof(WeakPasswordCount)); OnPropertyChanged(nameof(HasReusedPasswords)); OnPropertyChanged(nameof(HasWeakPasswords)); OnPropertyChanged(nameof(ReusedPasswordSummary)); OnPropertyChanged(nameof(WeakPasswordSummary)); NotifyVisibilityState(); }
    private void NotifyVisibilityState() { OnPropertyChanged(nameof(ShowRows)); OnPropertyChanged(nameof(ShowEmptyState)); }
    private WebLoginRowVm ToRow(WebLoginEntry entry) => new(_desktop.Localization, entry.Id, entry.Title, entry.Username, entry.Password, entry.Url, entry.Notes, entry.CreatedAtUtc, entry.UpdatedAtUtc, false, entry.Email);
    private static void ApplyEntry(WebLoginRowVm row, WebLoginEntry entry) { row.Title = entry.Title; row.Url = entry.Url; row.Username = entry.Username; row.Email = entry.Email; row.Password = entry.Password; row.Notes = entry.Notes; row.MarkSaved(entry.UpdatedAtUtc); }
    private static DateTimeOffset ParseTimestamp(string value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    private static bool PasswordIsWeak(string? value) => !string.IsNullOrWhiteSpace(value) && (value.Length < 12 || new[] { value.Any(char.IsLower), value.Any(char.IsUpper), value.Any(char.IsDigit), value.Any(ch => !char.IsLetterOrDigit(ch)) }.Count(found => found) < 3);
    private void ResetRevealState() { foreach (var row in _all) row.IsPasswordVisible = false; }

    public override void RefreshLocalization() { foreach (var row in _all) row.RefreshLocalization(); RebuildStaticOptions(); RefreshDynamicFilters(); Editor.RefreshLocalization(); NotifyListState(); }
}
