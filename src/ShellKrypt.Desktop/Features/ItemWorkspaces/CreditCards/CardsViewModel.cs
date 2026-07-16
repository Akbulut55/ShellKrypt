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

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;

public partial class CardsViewModel : ViewModelBase
{
    private readonly DesktopFeatureServices _desktop;
    private readonly ICardService _service;
    private readonly List<CardRowVm> _all = [];
    private readonly List<CardRowVm> _filtered = [];
    public ObservableCollection<CardRowVm> Rows { get; } = [];
    public ObservableCollection<SelectionOptionVm> BankFilters { get; } = [];
    public ObservableCollection<SelectionOptionVm> CardTypeFilters { get; } = [];
    public ObservableCollection<SelectionOptionVm> SortOptions { get; } = [];
    public CardEditorViewModel Editor { get; }

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private SelectionOptionVm? selectedBankFilter;
    [ObservableProperty] private SelectionOptionVm? selectedCardTypeFilter;
    [ObservableProperty] private SelectionOptionVm? selectedSortOption;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isLoading;

    public CardsViewModel(DesktopFeatureServices desktop, ICardService service, Func<string?, Task> refreshAllItemsAsync)
    { _desktop = desktop; _service = service; Editor = new(desktop, service, HandleMutationAsync, refreshAllItemsAsync); BuildSortOptions(); _ = LoadAsync(); }

    public int ExpiringSoonCount => _all.Count(row => row.IsExpiryUrgent);
    public int ExpiredCardsCount => _all.Count(row => row.IsExpired);
    public bool HasExpiringSoonCards => ExpiringSoonCount > 0;
    public bool HasExpiredCards => ExpiredCardsCount > 0;
    public string ExpiringSoonSummary => T(_desktop.Localization, ExpiringSoonCount == 1 ? "Cards.Summary.ExpiringOne" : "Cards.Summary.ExpiringMany", ExpiringSoonCount);
    public string ExpiredSummary => T(_desktop.Localization, ExpiredCardsCount == 1 ? "Cards.Summary.ExpiredOne" : "Cards.Summary.ExpiredMany", ExpiredCardsCount);
    public string ResultText => T(_desktop.Localization, "Cards.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool ShowRows => HasRows && !IsLoading && !HasError;
    public bool ShowEmptyState => !HasRows && !IsLoading && !HasError;
    public string EmptyTitle => _all.Count == 0 ? T(_desktop.Localization, "Cards.Empty.NoneTitle") : T(_desktop.Localization, "Cards.Empty.NoMatchTitle");
    public string EmptySubtitle => _all.Count == 0 ? T(_desktop.Localization, "Cards.Empty.NoneSubtitle") : T(_desktop.Localization, "Cards.Empty.NoMatchSubtitle");

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedBankFilterChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnSelectedCardTypeFilterChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SelectionOptionVm? value) => ApplyFilter();
    partial void OnErrorChanged(string value) { OnPropertyChanged(nameof(HasError)); NotifyVisibilityState(); }
    partial void OnIsLoadingChanged(bool value) => NotifyVisibilityState();
    [RelayCommand] private void AddNew() { ResetRevealState(); Editor.OpenAdd(); }
    [RelayCommand] private void ShowDetails(CardRowVm? row) { if (row is not null) { ResetRevealState(); Editor.OpenDetails(row); } }
    [RelayCommand] private void ToggleSecrets(CardRowVm? row) { if (row is not null) row.IsSecretsVisible = !row.IsSecretsVisible; }
    [RelayCommand] private async Task CopyCardNumberAsync(CardRowVm? row) { if (row is null) return; await _desktop.Clipboard.CopyAsync(row.Number); _desktop.Activity.Log("cards", "Card number copied", $"Copied card number for {row.Title}.", "info", affectedItem: row.Title); }

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    { if (_all.Count == 0) await LoadAsync(); var row = _all.FirstOrDefault(item => item.Id == itemId); if (row is null) { await LoadAsync(); row = _all.FirstOrDefault(item => item.Id == itemId); } if (row is null) return false; ResetFilters(); Editor.OpenDetails(row); return true; }

    private async Task LoadAsync()
    {
        Error = ""; if (_desktop.Session.VaultPath is null) { Error = T(_desktop.Localization, "Common.NoVaultSelected"); return; } IsLoading = true;
        try { _all.Clear(); _all.AddRange((await _service.ListAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey)).Select(ToRow)); RefreshFilters(); ApplyFilter(); }
        catch (Exception ex) { Error = ex.Message; } finally { IsLoading = false; }
    }

    private Task HandleMutationAsync(CardEntry? entry, string? deletedId)
    { if (deletedId is not null) _all.RemoveAll(row => row.Id == deletedId); else if (entry is not null) { var row = _all.FirstOrDefault(item => item.Id == entry.Id); if (row is null) _all.Insert(0, ToRow(entry)); else ApplyEntry(row, entry); } RefreshFilters(); ApplyFilter(); return Task.CompletedTask; }

    private void ApplyFilter()
    {
        IEnumerable<CardRowVm> query = _all; var text = SearchText.Trim();
        if (SelectedBankFilter?.Key is { } bank && bank != "all") query = query.Where(row => string.Equals(row.BankDisplay, bank, StringComparison.OrdinalIgnoreCase));
        if (SelectedCardTypeFilter?.Key is { } type && type != "all") query = query.Where(row => string.Equals(row.CardType, type, StringComparison.OrdinalIgnoreCase));
        if (text.Length > 0) query = query.Where(row => row.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Bank.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Cardholder.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Notes.Contains(text, StringComparison.OrdinalIgnoreCase) || row.IssuerDisplay.Contains(text, StringComparison.OrdinalIgnoreCase));
        query = SelectedSortOption?.Key switch { "oldest" => query.OrderBy(row => Parse(row.UpdatedAtUtc)), "title:asc" => query.OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase), "title:desc" => query.OrderByDescending(row => row.Title, StringComparer.OrdinalIgnoreCase), "expiry:asc" => query.OrderBy(Expiry), "expiry:desc" => query.OrderByDescending(Expiry), _ => query.OrderByDescending(row => Parse(row.UpdatedAtUtc)) };
        _filtered.Clear(); _filtered.AddRange(query); Rows.Clear(); foreach (var row in _filtered) Rows.Add(row); NotifyState();
    }

    private void BuildSortOptions() { var selected = SelectedSortOption?.Key ?? "newest"; SortOptions.Clear(); SortOptions.Add(new("newest", T(_desktop.Localization, "ItemWorkspace.Sort.Newest"))); SortOptions.Add(new("oldest", T(_desktop.Localization, "ItemWorkspace.Sort.Oldest"))); SortOptions.Add(new("title:asc", T(_desktop.Localization, "ItemWorkspace.Sort.NameAscending"))); SortOptions.Add(new("title:desc", T(_desktop.Localization, "ItemWorkspace.Sort.NameDescending"))); SortOptions.Add(new("expiry:asc", T(_desktop.Localization, "ItemWorkspace.Sort.ExpiryAscending"))); SortOptions.Add(new("expiry:desc", T(_desktop.Localization, "ItemWorkspace.Sort.ExpiryDescending"))); SelectedSortOption = SortOptions.First(option => option.Key == selected); }
    private void RefreshFilters()
    { var bank = SelectedBankFilter?.Key; var type = SelectedCardTypeFilter?.Key; BankFilters.Clear(); BankFilters.Add(new("all", T(_desktop.Localization, "ItemWorkspace.Filter.BankAll"))); foreach (var value in _all.Select(row => row.BankDisplay).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) BankFilters.Add(new(value, value)); CardTypeFilters.Clear(); CardTypeFilters.Add(new("all", T(_desktop.Localization, "ItemWorkspace.Filter.TypeAll"))); foreach (var value in _all.Select(row => row.CardType).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) CardTypeFilters.Add(new(value, value)); SelectedBankFilter = BankFilters.FirstOrDefault(item => item.Key == bank) ?? BankFilters[0]; SelectedCardTypeFilter = CardTypeFilters.FirstOrDefault(item => item.Key == type) ?? CardTypeFilters[0]; }
    private void ResetFilters() { SearchText = ""; SelectedBankFilter = BankFilters.FirstOrDefault(); SelectedCardTypeFilter = CardTypeFilters.FirstOrDefault(); SelectedSortOption = SortOptions.FirstOrDefault(); ApplyFilter(); }
    private void NotifyState() { OnPropertyChanged(nameof(ResultText)); OnPropertyChanged(nameof(HasRows)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptySubtitle)); OnPropertyChanged(nameof(ExpiringSoonCount)); OnPropertyChanged(nameof(ExpiredCardsCount)); OnPropertyChanged(nameof(HasExpiringSoonCards)); OnPropertyChanged(nameof(HasExpiredCards)); OnPropertyChanged(nameof(ExpiringSoonSummary)); OnPropertyChanged(nameof(ExpiredSummary)); NotifyVisibilityState(); }
    private void NotifyVisibilityState() { OnPropertyChanged(nameof(ShowRows)); OnPropertyChanged(nameof(ShowEmptyState)); }
    private CardRowVm ToRow(CardEntry entry) => new(_desktop.Localization, entry.Id, entry.Title, entry.Bank, entry.Cardholder, entry.Number, entry.ExpiryMonth.ToString("00"), entry.ExpiryYear.ToString(), entry.Cvc, entry.Notes, entry.Issuer, entry.CardType, entry.CreatedAtUtc, entry.UpdatedAtUtc);
    private static void ApplyEntry(CardRowVm row, CardEntry entry) { row.Title = entry.Title; row.Bank = entry.Bank; row.Cardholder = entry.Cardholder; row.Number = entry.Number; row.ExpiryMonth = entry.ExpiryMonth.ToString("00"); row.ExpiryYear = entry.ExpiryYear.ToString(); row.Cvc = entry.Cvc; row.Notes = entry.Notes; row.Issuer = entry.Issuer; row.CardType = entry.CardType; row.MarkSaved(entry.UpdatedAtUtc); }
    private static DateTimeOffset Parse(string value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    private static DateTime Expiry(CardRowVm row) => int.TryParse(row.ExpiryMonth, out var month) && month is >= 1 and <= 12 && int.TryParse(row.ExpiryYear, out var year) ? new DateTime(year < 100 ? year + 2000 : year, month, DateTime.DaysInMonth(year < 100 ? year + 2000 : year, month)) : DateTime.MaxValue;
    private void ResetRevealState() { foreach (var row in _all) row.IsSecretsVisible = false; }
    public override void RefreshLocalization() { foreach (var row in _all) row.RefreshLocalization(); BuildSortOptions(); RefreshFilters(); Editor.RefreshLocalization(); NotifyState(); }
}
