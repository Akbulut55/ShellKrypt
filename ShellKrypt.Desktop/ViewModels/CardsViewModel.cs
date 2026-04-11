using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm : ObservableObject
{
    public string Id { get; }
    public bool IsNew { get; private set; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string bank;
    [ObservableProperty] private string cardholder;
    [ObservableProperty] private string number;
    [ObservableProperty] private string expiryMonth;
    [ObservableProperty] private string expiryYear;
    [ObservableProperty] private string cvc;
    [ObservableProperty] private string notes;
    [ObservableProperty] private string issuer;
    [ObservableProperty] private bool isFavorite;

    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isSecretsVisible;

    private string _origTitle = "";
    private string _origBank = "";
    private string _origCardholder = "";
    private string _origNumber = "";
    private string _origExpiryMonth = "";
    private string _origExpiryYear = "";
    private string _origCvc = "";
    private string _origNotes = "";
    private string _origIssuer = "";
    private bool _origFavorite;

    public CardRowVm(
        string id,
        string title,
        string bank,
        string cardholder,
        string number,
        string expiryMonth,
        string expiryYear,
        string cvc,
        string notes,
        string issuer,
        bool favorite,
        string createdAtUtc,
        string updatedAtUtc,
        bool isNew)
    {
        Id = id;
        Title = title ?? "";
        Bank = bank ?? "";
        Cardholder = cardholder ?? "";
        Number = number ?? "";
        ExpiryMonth = expiryMonth ?? "";
        ExpiryYear = expiryYear ?? "";
        Cvc = cvc ?? "";
        Notes = notes ?? "";
        Issuer = string.IsNullOrWhiteSpace(issuer) ? DetectIssuer(number) : issuer;
        IsFavorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsNew = isNew;
    }

    public bool IsViewing => !IsEditing;

    public string IconLetter => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();

    public string NumberDisplay
        => IsSecretsVisible ? FormatCardNumber(Number) : MaskCardNumber(Number);

    public string CvcDisplay
        => IsSecretsVisible ? Cvc : (string.IsNullOrWhiteSpace(Cvc) ? "" : "***");

    public string ExpiryDisplay
        => $"{(string.IsNullOrWhiteSpace(ExpiryMonth) ? "MM" : ExpiryMonth)} / {FormatExpiryYear(ExpiryYear)}";

    public string NotesDisplay => DisplayOrPlaceholder("Notes", Notes);
    public string SubtitleDisplay => string.IsNullOrWhiteSpace(Notes)
        ? (string.IsNullOrWhiteSpace(Cardholder) ? "Encrypted card" : Cardholder.Trim())
        : Notes.Trim();
    public string BankDisplay => string.IsNullOrWhiteSpace(Bank) ? "Unassigned" : Bank.Trim();
    public string IssuerDisplay => string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
    public bool IsExpiryUrgent => TryGetExpiryDate(out var expiry) && expiry <= DateTime.Today.AddMonths(3);
    public string FavoriteGlyph => IsFavorite ? "*" : "";
    public string SecretsActionLabel => IsSecretsVisible ? "Hide" : "View";

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsViewing));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnBankChanged(string value) => OnPropertyChanged(nameof(BankDisplay));
    partial void OnNumberChanged(string value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        if (string.IsNullOrWhiteSpace(Issuer))
            OnPropertyChanged(nameof(IssuerDisplay));
    }
    partial void OnCvcChanged(string value) => OnPropertyChanged(nameof(CvcDisplay));
    partial void OnExpiryMonthChanged(string value) => NotifyExpiryChanged();
    partial void OnExpiryYearChanged(string value) => NotifyExpiryChanged();
    partial void OnCardholderChanged(string value) => OnPropertyChanged(nameof(SubtitleDisplay));
    partial void OnNotesChanged(string value)
    {
        OnPropertyChanged(nameof(NotesDisplay));
        OnPropertyChanged(nameof(SubtitleDisplay));
    }
    partial void OnIssuerChanged(string value) => OnPropertyChanged(nameof(IssuerDisplay));
    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteGlyph));
    partial void OnIsSecretsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        OnPropertyChanged(nameof(CvcDisplay));
        OnPropertyChanged(nameof(SecretsActionLabel));
    }

    public void BeginEdit()
    {
        _origTitle = Title;
        _origBank = Bank;
        _origCardholder = Cardholder;
        _origNumber = Number;
        _origExpiryMonth = ExpiryMonth;
        _origExpiryYear = ExpiryYear;
        _origCvc = Cvc;
        _origNotes = Notes;
        _origIssuer = Issuer;
        _origFavorite = IsFavorite;
        IsEditing = true;
    }

    public void CancelEdit(bool removeIfNew, Action<CardRowVm> removeRow)
    {
        if (removeIfNew && IsNew)
        {
            removeRow(this);
            return;
        }

        Title = _origTitle;
        Bank = _origBank;
        Cardholder = _origCardholder;
        Number = _origNumber;
        ExpiryMonth = _origExpiryMonth;
        ExpiryYear = _origExpiryYear;
        Cvc = _origCvc;
        Notes = _origNotes;
        Issuer = _origIssuer;
        IsFavorite = _origFavorite;
        IsEditing = false;
    }

    public void MarkSaved()
    {
        Issuer = string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
        IsNew = false;
        IsEditing = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
    }

    internal static string FormatCardNumber(string? number, int maxDigits = 19, bool includeTrailingSeparator = false)
    {
        var digits = new string((number ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());
        if (digits.Length == 0)
            return "";

        var groups = new List<string>();
        for (var i = 0; i < digits.Length; i += 4)
        {
            var length = Math.Min(4, digits.Length - i);
            groups.Add(digits.Substring(i, length));
        }

        var formatted = string.Join(" ", groups);
        if (includeTrailingSeparator && digits.Length % 4 == 0 && digits.Length < maxDigits)
            return formatted + " ";

        return formatted;
    }

    internal static string DetectIssuer(string? number)
    {
        var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("4", StringComparison.Ordinal))
            return "Visa";
        if (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal))
            return "Amex";
        if (digits.Length >= 2 && int.TryParse(digits[..2], out var prefix2))
        {
            if (prefix2 is >= 51 and <= 55)
                return "Mastercard";
            if (prefix2 is 36 or 38 or 39)
                return "Diners Club";
            if (prefix2 == 62)
                return "UnionPay";
            if (prefix2 == 65)
                return "Discover";
        }
        if (digits.Length >= 3 && int.TryParse(digits[..3], out var prefix3))
        {
            if (prefix3 is >= 300 and <= 305)
                return "Diners Club";
            if (prefix3 is >= 644 and <= 649)
                return "Discover";
        }
        if (digits.StartsWith("35", StringComparison.Ordinal))
            return "JCB";
        if (digits.StartsWith("6011", StringComparison.Ordinal))
            return "Discover";
        if (digits.Length >= 4 && int.TryParse(digits[..4], out var prefix4) && prefix4 is >= 2221 and <= 2720)
            return "Mastercard";

        return "Card";
    }

    private static string MaskCardNumber(string? n)
    {
        if (string.IsNullOrWhiteSpace(n))
            return "";

        var digits = new string(n.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        var last4 = digits[^4..];
        return $"**** **** **** {last4}";
    }

    private void NotifyExpiryChanged()
    {
        OnPropertyChanged(nameof(ExpiryDisplay));
        OnPropertyChanged(nameof(IsExpiryUrgent));
    }

    private static string FormatExpiryYear(string year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return "YY";

        var trimmed = year.Trim();
        return trimmed.Length >= 2 ? trimmed[^2..] : trimmed;
    }

    private bool TryGetExpiryDate(out DateTime expiry)
    {
        expiry = DateTime.MaxValue;
        if (!int.TryParse(ExpiryMonth, out var month) || month is < 1 or > 12)
            return false;
        if (!int.TryParse(ExpiryYear, out var year))
            return false;
        if (year < 100)
            year += 2000;

        expiry = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return true;
    }

    private static string DisplayOrPlaceholder(string label, string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        return text.Length > 120 ? $"{label}: {text[..117]}..." : $"{label}: {text}";
    }
}

public partial class CardsViewModel : ViewModelBase
{
    private const int PageSize = 3;
    private const string AllNetworkFilter = "Network: All";
    private const string SortNewest = "Sort: Newest";
    private const string SortExpiry = "Exp. Date";
    private const string SortAlphabetical = "Alphabetical";
    private const string DefaultIssuer = "Card";

    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    private readonly List<CardRowVm> _all = new();
    private readonly List<CardRowVm> _filtered = new();
    private bool _formattingAddNumber;
    private string _lastAutoAddIssuer = DefaultIssuer;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<CardRowVm> Rows { get; } = new();
    public ObservableCollection<string> NetworkFilters { get; } = new()
    {
        AllNetworkFilter,
        "Visa",
        "Mastercard",
        "Amex",
        "Discover",
        "JCB",
        "UnionPay",
        "Diners Club",
        "Card"
    };
    public ObservableCollection<string> IssuerOptions { get; } = new()
    {
        "Visa",
        "Mastercard",
        "Amex",
        "Discover",
        "JCB",
        "UnionPay",
        "Diners Club",
        "Card"
    };
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        SortNewest,
        SortExpiry,
        SortAlphabetical
    };

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedNetworkFilter = AllNetworkFilter;
    [ObservableProperty] private string selectedSortOption = SortNewest;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private bool isAddCardModalOpen;
    [ObservableProperty] private string addTitle = "";
    [ObservableProperty] private string addBank = "";
    [ObservableProperty] private string addCardholder = "";
    [ObservableProperty] private string addIssuer = DefaultIssuer;
    [ObservableProperty] private string addNumber = "";
    [ObservableProperty] private string addExpiryMonth = "";
    [ObservableProperty] private string addExpiryYear = "";
    [ObservableProperty] private string addCvc = "";
    [ObservableProperty] private string addNotes = "";
    [ObservableProperty] private bool addFavorite;
    [ObservableProperty] private string error = "";

    public int TotalCardsCount => _all.Count;
    public int ActiveCardsCount => _all.Count(row => !row.IsExpiryUrgent);
    public int ExpiringSoonCount => _all.Count(row => row.IsExpiryUrgent);
    public string PrimaryCardTitle => _all.FirstOrDefault(row => row.IsFavorite)?.Title
                                      ?? _all.FirstOrDefault()?.Title
                                      ?? "No card selected";
    public string PrimaryCardNote => _all.Any(row => row.IsFavorite)
        ? "Marked as vault favorite"
        : _all.Count > 0
            ? "Most recent saved card"
            : "Add a card to assign one";
    public string ItemsSummary => $"SHOWING {Rows.Count} OF {_filtered.Count} CARDS";
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
    public string PageSummary => $"{CurrentPage} / {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;

    public CardsViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedNetworkFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();
    partial void OnAddNumberChanged(string value)
    {
        if (_formattingAddNumber)
            return;

        var formatted = CardRowVm.FormatCardNumber(value, includeTrailingSeparator: true);
        if (!string.Equals(value, formatted, StringComparison.Ordinal))
        {
            _formattingAddNumber = true;
            AddNumber = formatted;
            _formattingAddNumber = false;
        }

        UpdateAddIssuerFromNumber(formatted);
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        ClearAddCardForm();
        IsAddCardModalOpen = true;
    }

    [RelayCommand]
    private void BeginEdit(CardRowVm row)
    {
        Error = "";
        row.BeginEdit();
    }

    [RelayCommand]
    private void Cancel(CardRowVm row)
    {
        Error = "";
        row.CancelEdit(removeIfNew: true, removeRow: RemoveRow);
        ApplyFilter(resetPage: false);
    }

    [RelayCommand]
    private void ToggleSecrets(CardRowVm row)
        => row.IsSecretsVisible = !row.IsSecretsVisible;

    [RelayCommand]
    private void CancelAddCard()
    {
        Error = "";
        ClearAddCardForm();
        IsAddCardModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveAddCardAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var digits = new string((AddNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 12) { Error = "Card number looks too short."; return; }

        if (!int.TryParse(AddExpiryMonth, out var mm) || mm < 1 || mm > 12)
        {
            Error = "Expiry month must be 1-12.";
            return;
        }

        if (!int.TryParse(AddExpiryYear, out var yy) || yy < 2000 || yy > 2100)
        {
            Error = "Expiry year must be like 2026.";
            return;
        }

        var cvcDigits = new string((AddCvc ?? "").Where(char.IsDigit).ToArray());
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var id = Guid.NewGuid().ToString("N");
            var payload = new CardPayload(
                Title: AddTitle.Trim(),
                Cardholder: AddCardholder.Trim(),
                Number: digits,
                ExpiryMonth: mm,
                ExpiryYear: yy,
                Cvc: cvcDigits,
                Notes: AddNotes.Trim(),
                Issuer: string.IsNullOrWhiteSpace(AddIssuer) ? CardRowVm.DetectIssuer(digits) : AddIssuer.Trim(),
                Bank: AddBank.Trim()
            );

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);
            var header = new VaultItemHeader(
                Id: id,
                Type: ItemType.Card,
                Favorite: AddFavorite,
                CreatedAtUtc: now,
                UpdatedAtUtc: now
            );

            await _repo.InsertAsync(_root.VaultPath, header, enc);

            _all.Insert(0, new CardRowVm(
                id,
                payload.Title,
                payload.Bank ?? "",
                payload.Cardholder,
                payload.Number,
                payload.ExpiryMonth.ToString("00"),
                payload.ExpiryYear.ToString(),
                payload.Cvc,
                payload.Notes,
                payload.Issuer,
                AddFavorite,
                now,
                now,
                isNew: false
            ));

            ClearAddCardForm();
            IsAddCardModalOpen = false;
            SearchText = "";
            SelectedNetworkFilter = AllNetworkFilter;
            SelectedSortOption = SortNewest;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(CardRowVm row)
    {
        Error = "";
        var previous = row.IsFavorite;
        row.IsFavorite = !row.IsFavorite;
        await SaveAsync(row);

        if (!string.IsNullOrWhiteSpace(Error))
        {
            row.IsFavorite = previous;
            NotifyCardSummaryChanged();
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CardRowVm row)
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(row.Title)) { Error = "Title is required."; return; }

        var digits = new string((row.Number ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 12) { Error = "Card number looks too short."; return; }

        if (!int.TryParse(row.ExpiryMonth, out var mm) || mm < 1 || mm > 12)
        {
            Error = "Expiry month must be 1-12.";
            return;
        }

        if (!int.TryParse(row.ExpiryYear, out var yy) || yy < 2000 || yy > 2100)
        {
            Error = "Expiry year must be like 2026.";
            return;
        }

        var cvcDigits = new string((row.Cvc ?? "").Where(char.IsDigit).ToArray());
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");

            var payload = new CardPayload(
                Title: row.Title.Trim(),
                Cardholder: row.Cardholder?.Trim() ?? "",
                Number: digits,
                ExpiryMonth: mm,
                ExpiryYear: yy,
                Cvc: cvcDigits,
                Notes: row.Notes?.Trim() ?? "",
                Issuer: string.IsNullOrWhiteSpace(row.Issuer) ? CardRowVm.DetectIssuer(digits) : row.Issuer.Trim(),
                Bank: row.Bank?.Trim() ?? ""
            );

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);

            var header = new VaultItemHeader(
                Id: row.Id,
                Type: ItemType.Card,
                Favorite: row.IsFavorite,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: now
            );

            if (row.IsNew)
                await _repo.InsertAsync(_root.VaultPath, header, enc);
            else
                await _repo.UpdateAsync(_root.VaultPath, header, enc);

            row.Number = digits;
            row.Cvc = cvcDigits;
            row.Issuer = payload.Issuer;
            row.Bank = payload.Bank ?? "";
            row.ExpiryMonth = mm.ToString("00");
            row.ExpiryYear = yy.ToString();
            row.MarkSaved();

            ApplyFilter(resetPage: false);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CardRowVm row)
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            await _repo.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(CardRowVm row)
    {
        _all.Remove(row);
        ApplyFilter(resetPage: false);
    }

    private void ClearAddCardForm()
    {
        AddTitle = "";
        AddBank = "";
        AddCardholder = "";
        AddIssuer = DefaultIssuer;
        _lastAutoAddIssuer = DefaultIssuer;
        AddNumber = "";
        AddExpiryMonth = "";
        AddExpiryYear = "";
        AddCvc = "";
        AddNotes = "";
        AddFavorite = false;
    }

    private void UpdateAddIssuerFromNumber(string number)
    {
        var detected = CardRowVm.DetectIssuer(number);
        if (string.Equals(detected, DefaultIssuer, StringComparison.Ordinal))
        {
            if (string.Equals(AddIssuer, _lastAutoAddIssuer, StringComparison.Ordinal))
            {
                AddIssuer = DefaultIssuer;
                _lastAutoAddIssuer = DefaultIssuer;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(AddIssuer) ||
            string.Equals(AddIssuer, DefaultIssuer, StringComparison.Ordinal) ||
            string.Equals(AddIssuer, _lastAutoAddIssuer, StringComparison.Ordinal))
        {
            AddIssuer = detected;
            _lastAutoAddIssuer = detected;
        }
    }

    private async Task LoadAsync()
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            _all.Clear();
            Rows.Clear();

            var rows = await _repo.ListAsync(_root.VaultPath);

            foreach (var r in rows.Where(x => x.Header.Type == ItemType.Card))
            {
                var json = AesGcmBlob.Decrypt(_root.VaultKey, r.EncryptedPayload);
                var payload = JsonSerializer.Deserialize<CardPayload>(json, JsonOpts);
                if (payload is null) continue;

                _all.Add(new CardRowVm(
                    r.Header.Id,
                    payload.Title,
                    payload.Bank ?? payload.Cardholder,
                    payload.Cardholder,
                    payload.Number,
                    payload.ExpiryMonth.ToString("00"),
                    payload.ExpiryYear.ToString(),
                    payload.Cvc,
                    payload.Notes,
                    payload.Issuer,
                    r.Header.Favorite,
                    r.Header.CreatedAtUtc,
                    r.Header.UpdatedAtUtc,
                    isNew: false
                ));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void PreviousPage()
    {
        if (!CanGoPreviousPage)
            return;

        CurrentPage--;
        RenderPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage()
    {
        if (!CanGoNextPage)
            return;

        CurrentPage++;
        RenderPage();
    }

    private void ApplyFilter() => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        IEnumerable<CardRowVm> filtered = _all;
        var q = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(SelectedNetworkFilter) &&
            !string.Equals(SelectedNetworkFilter, AllNetworkFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.IssuerDisplay, SelectedNetworkFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Bank ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Cardholder ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.NumberDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.IssuerDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.ExpiryDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.IsFavorite && "favorite".Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = SelectedSortOption switch
        {
            SortExpiry => filtered.OrderBy(GetExpirySortKey),
            SortAlphabetical => filtered.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(r => ParseTimestamp(r.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        RenderPage();
        NotifyCardSummaryChanged();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var row in _filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            Rows.Add(row);

        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCardSummaryChanged()
    {
        OnPropertyChanged(nameof(TotalCardsCount));
        OnPropertyChanged(nameof(ActiveCardsCount));
        OnPropertyChanged(nameof(ExpiringSoonCount));
        OnPropertyChanged(nameof(PrimaryCardTitle));
        OnPropertyChanged(nameof(PrimaryCardNote));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static DateTime GetExpirySortKey(CardRowVm row)
    {
        if (!int.TryParse(row.ExpiryMonth, out var month) || month is < 1 or > 12)
            return DateTime.MaxValue;
        if (!int.TryParse(row.ExpiryYear, out var year))
            return DateTime.MaxValue;
        if (year < 100)
            year += 2000;

        return new DateTime(year, month, DateTime.DaysInMonth(year, month));
    }
}
