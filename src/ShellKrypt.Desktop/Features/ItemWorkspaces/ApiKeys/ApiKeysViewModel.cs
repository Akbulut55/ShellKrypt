using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;

public partial class ApiKeysViewModel : ViewModelBase
{
    internal const string DefaultFieldType = "API Key";
    private readonly ItemWorkspaceRuntime _desktop; private readonly IApiKeyService _service; private readonly Func<Task> _notifyReferencesChanged; private readonly List<ApiKeyRowVm> _all = []; private readonly List<ApiKeyRowVm> _filtered = [];
    public ObservableCollection<ApiKeyRowVm> Rows { get; } = []; public ObservableCollection<SelectionOptionVm> ProviderFilters { get; } = []; public ObservableCollection<SelectionOptionVm> SortOptions { get; } = []; public ApiKeyEditorViewModel Editor { get; }
    [ObservableProperty] private string searchText = ""; [ObservableProperty] private SelectionOptionVm? selectedProviderFilter; [ObservableProperty] private SelectionOptionVm? selectedSortOption; [ObservableProperty] private string error = ""; [ObservableProperty] private bool isLoading;
    public ApiKeysViewModel(ItemWorkspaceRuntime desktop, IApiKeyService service, Func<string?, Task> refreshAllItemsAsync, Func<Task>? notifyReferencesChanged = null) { _desktop = desktop; _service = service; _notifyReferencesChanged = notifyReferencesChanged ?? (() => Task.CompletedTask); Editor = new(desktop, service, HandleMutationAsync, refreshAllItemsAsync); BuildSortOptions(); _ = LoadAsync(); }
    public int ProviderCount => _all.Select(row => row.ProviderDisplay).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int SensitiveFieldCount => _all.Sum(row => row.Fields.Count(itemField => itemField.IsSensitive));
    public bool HasProviders => ProviderCount > 0;
    public bool HasSensitiveFields => SensitiveFieldCount > 0;
    public string ProviderSummary => T(_desktop.Localization, "ItemWorkspace.Summary.Providers", ProviderCount);
    public string SensitiveFieldSummary => T(_desktop.Localization, "ItemWorkspace.Summary.SensitiveFields", SensitiveFieldCount);
    public string ResultText => T(_desktop.Localization, "ApiKeys.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool ShowRows => HasRows && !IsLoading && !HasError;
    public bool ShowEmptyState => !HasRows && !IsLoading && !HasError;
    public string EmptyTitle => _all.Count == 0 ? T(_desktop.Localization, "ApiKeys.Empty.NoneTitle") : T(_desktop.Localization, "ApiKeys.Empty.NoMatchTitle");
    public string EmptySubtitle => _all.Count == 0 ? T(_desktop.Localization, "ApiKeys.Empty.NoneSubtitle") : T(_desktop.Localization, "ApiKeys.Empty.NoMatchSubtitle");
    partial void OnSearchTextChanged(string value) => ApplyFilter(); partial void OnSelectedProviderFilterChanged(SelectionOptionVm? value) => ApplyFilter(); partial void OnSelectedSortOptionChanged(SelectionOptionVm? value) => ApplyFilter(); partial void OnErrorChanged(string value) { OnPropertyChanged(nameof(HasError)); NotifyVisibilityState(); } partial void OnIsLoadingChanged(bool value) => NotifyVisibilityState();
    [RelayCommand] private void AddNew() { ResetRevealState(); Editor.OpenAdd(); } [RelayCommand] private void ShowDetails(ApiKeyRowVm? row) { if (row is not null) { ResetRevealState(); Editor.OpenDetails(row); } }
    [RelayCommand] private void TogglePrimarySecret(ApiKeyRowVm? row) { if (row?.PrimaryField is { } field) field.IsValueVisible = !field.IsValueVisible; row?.NotifyFieldsChanged(); }
    [RelayCommand] private async Task CopyPrimarySecretAsync(ApiKeyRowVm? row) { if (row is null || row.PrimaryCopyValue.Length == 0) return; await _desktop.Clipboard.CopyAsync(row.PrimaryCopyValue); _desktop.Activity.Log("api_keys", "API key copied", $"Copied API key for {row.Name}.", "info", affectedItem: row.Name); }
    public async Task<bool> OpenEntryByIdAsync(string itemId) { if (_all.Count == 0) await LoadAsync(); var row = _all.FirstOrDefault(item => item.Id == itemId); if (row is null) { await LoadAsync(); row = _all.FirstOrDefault(item => item.Id == itemId); } if (row is null) return false; ResetFilters(); Editor.OpenDetails(row); return true; }
    private async Task LoadAsync() { Error = ""; if (_desktop.Session.VaultPath is null) { Error = T(_desktop.Localization, "Common.NoVaultSelected"); return; } IsLoading = true; try { _all.Clear(); _all.AddRange((await _service.ListAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey)).Select(entry => new ApiKeyRowVm(entry, _desktop.Localization))); RefreshFilters(); ApplyFilter(); } catch (Exception ex) { Error = ex.Message; } finally { IsLoading = false; } }
    private async Task HandleMutationAsync(ApiKeyEntry? entry, string? deletedId) { if (deletedId is not null) _all.RemoveAll(row => row.Id == deletedId); else if (entry is not null) { var row = _all.FirstOrDefault(item => item.Id == entry.Id); if (row is null) _all.Insert(0, new(entry, _desktop.Localization)); else row.Apply(entry); } RefreshFilters(); ApplyFilter(); await _notifyReferencesChanged(); }
    private void ApplyFilter() { IEnumerable<ApiKeyRowVm> query = _all; var text = SearchText.Trim(); if (SelectedProviderFilter?.Key is { } provider && provider != "all") query = query.Where(row => string.Equals(row.ProviderDisplay, provider, StringComparison.OrdinalIgnoreCase)); if (text.Length > 0) query = query.Where(row => row.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Provider.Contains(text, StringComparison.OrdinalIgnoreCase) || row.User.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Notes.Contains(text, StringComparison.OrdinalIgnoreCase)); query = SelectedSortOption?.Key switch { "oldest" => query.OrderBy(row => Parse(row.UpdatedAtUtc)), "name:asc" => query.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase), "name:desc" => query.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase), "provider:asc" => query.OrderBy(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase), "provider:desc" => query.OrderByDescending(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase), _ => query.OrderByDescending(row => Parse(row.UpdatedAtUtc)) }; _filtered.Clear(); _filtered.AddRange(query); Rows.Clear(); foreach (var row in _filtered) Rows.Add(row); NotifyState(); }
    private void BuildSortOptions() { var selected = SelectedSortOption?.Key ?? "newest"; SortOptions.Clear(); SortOptions.Add(new("newest", T(_desktop.Localization, "ItemWorkspace.Sort.Newest"))); SortOptions.Add(new("oldest", T(_desktop.Localization, "ItemWorkspace.Sort.Oldest"))); SortOptions.Add(new("name:asc", T(_desktop.Localization, "ItemWorkspace.Sort.NameAscending"))); SortOptions.Add(new("name:desc", T(_desktop.Localization, "ItemWorkspace.Sort.NameDescending"))); SortOptions.Add(new("provider:asc", T(_desktop.Localization, "ItemWorkspace.Sort.ProviderAscending"))); SortOptions.Add(new("provider:desc", T(_desktop.Localization, "ItemWorkspace.Sort.ProviderDescending"))); SelectedSortOption = SortOptions.First(option => option.Key == selected); }
    private void RefreshFilters() { var selected = SelectedProviderFilter?.Key; ProviderFilters.Clear(); ProviderFilters.Add(new("all", T(_desktop.Localization, "ItemWorkspace.Filter.ProviderAll"))); foreach (var value in _all.Select(row => row.ProviderDisplay).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) ProviderFilters.Add(new(value, value)); SelectedProviderFilter = ProviderFilters.FirstOrDefault(item => item.Key == selected) ?? ProviderFilters[0]; }
    private void ResetFilters() { SearchText = ""; SelectedProviderFilter = ProviderFilters.FirstOrDefault(); SelectedSortOption = SortOptions.FirstOrDefault(); ApplyFilter(); }
    private void NotifyState() { OnPropertyChanged(nameof(ResultText)); OnPropertyChanged(nameof(HasRows)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptySubtitle)); OnPropertyChanged(nameof(ProviderCount)); OnPropertyChanged(nameof(SensitiveFieldCount)); OnPropertyChanged(nameof(HasProviders)); OnPropertyChanged(nameof(HasSensitiveFields)); OnPropertyChanged(nameof(ProviderSummary)); OnPropertyChanged(nameof(SensitiveFieldSummary)); NotifyVisibilityState(); }
    private void NotifyVisibilityState() { OnPropertyChanged(nameof(ShowRows)); OnPropertyChanged(nameof(ShowEmptyState)); }
    private static DateTimeOffset Parse(string value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    private void ResetRevealState() { foreach (var row in _all) foreach (var field in row.Fields) field.IsValueVisible = false; }
    public override void RefreshLocalization() { foreach (var row in _all) row.RefreshLocalization(); BuildSortOptions(); RefreshFilters(); Editor.RefreshLocalization(); NotifyState(); }
}
