using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm : ObservableObject
{
    internal const string DefaultCardType = "Credit Card";
    internal const int StandardCardNumberMaxDigits = 16;
    internal const int ExpiryMonthMaxDigits = 2;
    internal const int ExpiryYearMaxDigits = 4;
    internal const int CvcMaxDigits = 4;

    public string Id { get; }
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
    [ObservableProperty] private string cardType;

    [ObservableProperty] private bool isSecretsVisible;

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
        string cardType,
        string createdAtUtc,
        string updatedAtUtc)
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
        CardType = string.IsNullOrWhiteSpace(cardType) ? DefaultCardType : cardType;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string IconLetter => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();

    public string NumberDisplay
        => IsSecretsVisible ? FormatCardNumber(Number) : MaskCardNumber(Number);

    public string ExpiryDisplay
        => $"{(string.IsNullOrWhiteSpace(ExpiryMonth) ? "MM" : ExpiryMonth)} / {FormatExpiryYear(ExpiryYear)}";

    public string SubtitleDisplay => string.IsNullOrWhiteSpace(Notes)
        ? (string.IsNullOrWhiteSpace(Cardholder) ? "Encrypted card" : Cardholder.Trim())
        : Notes.Trim();
    public string BankDisplay => string.IsNullOrWhiteSpace(Bank) ? "Unassigned" : Bank.Trim();
    public string IssuerDisplay => string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
    public bool IsExpired => TryGetExpiryDate(out var expiry) && expiry < DateTime.Today;
    public bool IsExpiryUrgent => TryGetExpiryDate(out var expiry) &&
                                  expiry >= DateTime.Today &&
                                  expiry <= DateTime.Today.AddMonths(3);
    public string SecretsActionLabel => IsSecretsVisible ? "Hide" : "View";

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnBankChanged(string value) => OnPropertyChanged(nameof(BankDisplay));
    partial void OnNumberChanged(string value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        if (string.IsNullOrWhiteSpace(Issuer))
            OnPropertyChanged(nameof(IssuerDisplay));
    }
    partial void OnCvcChanged(string value)
    {
        var normalized = DigitsOnly(value, CvcMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            Cvc = normalized;
        }
    }
    partial void OnExpiryMonthChanged(string value)
    {
        var normalized = DigitsOnly(value, ExpiryMonthMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            ExpiryMonth = normalized;
            return;
        }

        NotifyExpiryChanged();
    }
    partial void OnExpiryYearChanged(string value)
    {
        var normalized = DigitsOnly(value, ExpiryYearMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            ExpiryYear = normalized;
            return;
        }

        NotifyExpiryChanged();
    }
    partial void OnCardholderChanged(string value) => OnPropertyChanged(nameof(SubtitleDisplay));
    partial void OnNotesChanged(string value)
    {
        OnPropertyChanged(nameof(SubtitleDisplay));
    }
    partial void OnIssuerChanged(string value) => OnPropertyChanged(nameof(IssuerDisplay));
    partial void OnIsSecretsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        OnPropertyChanged(nameof(SecretsActionLabel));
    }

    public void MarkSaved(string updatedAtUtc)
    {
        Issuer = string.IsNullOrWhiteSpace(Issuer) ? DetectIssuer(Number) : Issuer.Trim();
        UpdatedAtUtc = string.IsNullOrWhiteSpace(updatedAtUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : updatedAtUtc;
    }

    internal static string FormatCardNumber(string? number, int maxDigits = 19, bool includeTrailingSeparator = false)
    {
        var digits = DigitsOnly(number, maxDigits);
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

    internal static string DigitsOnly(string? value, int maxDigits)
        => new((value ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());

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
        OnPropertyChanged(nameof(IsExpired));
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

}

public partial class CardsViewModel : ViewModelBase
{
    private const int PageSize = 5;
    private const string AllNetworkFilter = "Network: All";
    private const string SortNewest = "Sort: Newest";
    private const string SortExpiry = "Exp. Date";
    private const string SortAlphabetical = "Alphabetical";
    private const string DefaultIssuer = "Card";

    private readonly MainWindowViewModel _root;
    private readonly ICardService _cardService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;

    private readonly List<CardRowVm> _all = new();
    private readonly List<CardRowVm> _filtered = new();
    private CardRowVm? _selectedDetailsRow;
    private bool _formattingAddNumber;
    private string _lastAutoAddIssuer = DefaultIssuer;

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
    public ObservableCollection<string> CardTypeOptions { get; } = new()
    {
        CardRowVm.DefaultCardType,
        "Debit Card",
        "Bank Card",
        "Prepaid Card",
        "Virtual Card",
        "Charge Card"
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
    [ObservableProperty] private bool isAddCardMode = true;
    [ObservableProperty] private bool isCardDetailsEditing;
    [ObservableProperty] private bool isCardDeleteConfirming;
    [ObservableProperty] private string addTitle = "";
    [ObservableProperty] private string addBank = "";
    [ObservableProperty] private string addCardholder = "";
    [ObservableProperty] private string addIssuer = DefaultIssuer;
    [ObservableProperty] private string addCardType = CardRowVm.DefaultCardType;
    [ObservableProperty] private string addNumber = "";
    [ObservableProperty] private string addExpiryMonth = "";
    [ObservableProperty] private string addExpiryYear = "";
    [ObservableProperty] private string addCvc = "";
    [ObservableProperty] private string addNotes = "";
    [ObservableProperty] private bool isAddCvcVisible;
    [ObservableProperty] private string error = "";

    public int ActiveCardsCount => _all.Count(row => !row.IsExpired);
    public int ExpiringSoonCount => _all.Count(row => row.IsExpiryUrgent);
    public int ExpiredCardsCount => _all.Count(row => row.IsExpired);
    public string ActiveCardsSummary => ActiveCardsCount == 1
        ? "1 usable card in your vault"
        : $"{ActiveCardsCount} usable cards in your vault";
    public string ExpiringSoonSummary => ExpiringSoonCount == 1
        ? "1 card expires within 3 months"
        : $"{ExpiringSoonCount} cards expire within 3 months";
    public string ExpiredCardsSummary => ExpiredCardsCount == 1
        ? "1 card is already expired"
        : $"{ExpiredCardsCount} cards are already expired";
    public string ItemsSummary => $"Showing {Rows.Count} of {_filtered.Count} cards";
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
    public string PageSummary => $"{CurrentPage} / {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public bool HasRows => Rows.Count > 0;
    public string EmptyTableTitle => _all.Count == 0
        ? "No credit cards saved yet"
        : "No credit cards match this view";
    public string EmptyTableSubtitle => _all.Count == 0
        ? "Add a credit card to keep encrypted payment details in this vault."
        : "Adjust the search, network filter, or sort option to show more cards.";
    public string CardModalTitle => IsAddCardMode
        ? "Add Credit Card"
        : IsCardDeleteConfirming
            ? "Delete Card?"
            : IsCardDetailsEditing
                ? "Edit Credit Card"
                : "Card Details";
    public string CardModalSubtitle => IsAddCardMode
        ? "Store a new payment card in your encrypted vault."
        : IsCardDeleteConfirming
            ? "Are you sure you want to delete this card? This action cannot be undone."
            : IsCardDetailsEditing
                ? "Update the saved payment card stored in this encrypted vault."
                : "Review the saved payment card stored in this encrypted vault.";
    public bool IsCardDetailsViewMode => !IsAddCardMode && !IsCardDetailsEditing && !IsCardDeleteConfirming;
    public bool IsCardDetailsEditMode => !IsAddCardMode && IsCardDetailsEditing && !IsCardDeleteConfirming;
    public bool IsCardDetailsDeleteConfirmMode => !IsAddCardMode && IsCardDeleteConfirming;
    public bool IsCardFormReadOnly => !IsAddCardMode && !IsCardDetailsEditing;
    public bool IsCardFormEditable => IsAddCardMode || IsCardDetailsEditing;
    public string AddCvcVisibilityLabel => IsAddCvcVisible ? "Hide" : "Reveal";
    public string CardModalFooterText => IsCardDetailsDeleteConfirmMode
        ? $"Are you sure you want to delete \"{(string.IsNullOrWhiteSpace(AddTitle) ? "this card" : AddTitle)}\"?"
        : "Fields are encrypted locally before being stored.";

    public CardsViewModel(MainWindowViewModel root, ICardService cardService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _cardService = cardService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedNetworkFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();
    partial void OnIsAddCardModeChanged(bool value) => NotifyCardModalModeChanged();
    partial void OnIsCardDetailsEditingChanged(bool value) => NotifyCardModalModeChanged();
    partial void OnIsCardDeleteConfirmingChanged(bool value) => NotifyCardModalModeChanged();
    partial void OnAddTitleChanged(string value) => OnPropertyChanged(nameof(CardModalFooterText));
    partial void OnIsAddCvcVisibleChanged(bool value) => OnPropertyChanged(nameof(AddCvcVisibilityLabel));
    partial void OnAddNumberChanged(string value)
    {
        if (_formattingAddNumber)
            return;

        var formatted = CardRowVm.FormatCardNumber(
            value,
            maxDigits: CardRowVm.StandardCardNumberMaxDigits,
            includeTrailingSeparator: true);
        if (!string.Equals(value, formatted, StringComparison.Ordinal))
        {
            _formattingAddNumber = true;
            AddNumber = formatted;
            _formattingAddNumber = false;
        }

        UpdateAddIssuerFromNumber(formatted);
    }
    partial void OnAddExpiryMonthChanged(string value)
    {
        var normalized = CardRowVm.DigitsOnly(value, CardRowVm.ExpiryMonthMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
            AddExpiryMonth = normalized;
    }
    partial void OnAddExpiryYearChanged(string value)
    {
        var normalized = CardRowVm.DigitsOnly(value, CardRowVm.ExpiryYearMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
            AddExpiryYear = normalized;
    }
    partial void OnAddCvcChanged(string value)
    {
        var normalized = CardRowVm.DigitsOnly(value, CardRowVm.CvcMaxDigits);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
            AddCvc = normalized;
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(EmptyTableTitle));
        OnPropertyChanged(nameof(EmptyTableSubtitle));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCardModalModeChanged()
    {
        OnPropertyChanged(nameof(CardModalTitle));
        OnPropertyChanged(nameof(CardModalSubtitle));
        OnPropertyChanged(nameof(IsCardDetailsViewMode));
        OnPropertyChanged(nameof(IsCardDetailsEditMode));
        OnPropertyChanged(nameof(IsCardDetailsDeleteConfirmMode));
        OnPropertyChanged(nameof(IsCardFormReadOnly));
        OnPropertyChanged(nameof(IsCardFormEditable));
        OnPropertyChanged(nameof(CardModalFooterText));
    }

    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        _selectedDetailsRow = null;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        IsAddCardMode = true;
        ClearAddCardForm();
        IsAddCardModalOpen = true;
    }

    [RelayCommand]
    private void ShowDetails(CardRowVm row)
    {
        Error = "";
        _selectedDetailsRow = row;
        IsAddCardMode = false;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        PopulateModalFromRow(row);
        IsAddCardModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsCardDeleteConfirming = false;
        IsCardDetailsEditing = true;
    }

    [RelayCommand]
    private void CancelDetailsEdit()
    {
        Error = "";

        if (_selectedDetailsRow is not null)
            PopulateModalFromRow(_selectedDetailsRow);

        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
    }

    [RelayCommand]
    private void BeginDetailsDelete()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = true;
    }

    [RelayCommand]
    private void CancelDetailsDelete()
    {
        Error = "";
        IsCardDeleteConfirming = false;
    }

    [RelayCommand]
    private void ToggleSecrets(CardRowVm row)
        => row.IsSecretsVisible = !row.IsSecretsVisible;

    [RelayCommand]
    private void ToggleAddCvcVisibility()
        => IsAddCvcVisible = !IsAddCvcVisible;

    [RelayCommand]
    private async Task CopyCardNumberAsync(CardRowVm row)
    {
        Error = "";

        var digits = CardRowVm.DigitsOnly(row.Number, CardRowVm.StandardCardNumberMaxDigits);
        if (string.IsNullOrWhiteSpace(digits))
        {
            Error = "No card number to copy.";
            return;
        }

        await _root.CopyToClipboardAsync(digits);
        _root.LogActivity("cards", "Card number copied", $"Copied card number for {row.Title}.", "info", affectedItem: row.Title);
    }

    [RelayCommand]
    private void CancelAddCard()
    {
        Error = "";
        ClearAddCardForm();
        _selectedDetailsRow = null;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        IsAddCardMode = true;
        IsAddCardModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveAddCardAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var digits = CardRowVm.DigitsOnly(AddNumber, CardRowVm.StandardCardNumberMaxDigits);
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

        var cvcDigits = CardRowVm.DigitsOnly(AddCvc, CardRowVm.CvcMaxDigits);
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var entry = await _cardService.AddAsync(
                _root.VaultPath,
                _root.VaultKey,
                BuildInput(digits, mm, yy, cvcDigits));

            _all.Insert(0, ToRow(entry));
            await _refreshAllItemsAsync(entry.Id);

            ClearAddCardForm();
            IsAddCardModalOpen = false;
            SearchText = "";
            SelectedNetworkFilter = AllNetworkFilter;
            SelectedSortOption = SortNewest;
            ApplyFilter();
            _root.LogActivity("cards", "Credit card added", $"Added {entry.Title}.", "success", affectedItem: entry.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveDetailsAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = "No card selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var digits = CardRowVm.DigitsOnly(AddNumber, CardRowVm.StandardCardNumberMaxDigits);
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

        var cvcDigits = CardRowVm.DigitsOnly(AddCvc, CardRowVm.CvcMaxDigits);
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var row = _selectedDetailsRow;
            var entry = await _cardService.UpdateAsync(
                _root.VaultPath,
                _root.VaultKey,
                row.Id,
                row.CreatedAtUtc,
                BuildInput(digits, mm, yy, cvcDigits));

            ApplyEntry(row, entry);
            await _refreshAllItemsAsync(entry.Id);

            IsCardDetailsEditing = false;
            IsCardDeleteConfirming = false;
            PopulateModalFromRow(row);
            ApplyFilter(resetPage: false);
            _root.LogActivity("cards", "Credit card updated", $"Updated {entry.Title}.", "info", affectedItem: entry.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConfirmDetailsDeleteAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = "No card selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            var row = _selectedDetailsRow;
            await _cardService.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
            await _refreshAllItemsAsync(null);
            _selectedDetailsRow = null;
            IsCardDeleteConfirming = false;
            IsCardDetailsEditing = false;
            IsAddCardMode = true;
            ClearAddCardForm();
            IsAddCardModalOpen = false;
            _root.LogActivity("cards", "Credit card deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title);
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
        AddCardType = CardRowVm.DefaultCardType;
        _lastAutoAddIssuer = DefaultIssuer;
        AddNumber = "";
        AddExpiryMonth = "";
        AddExpiryYear = "";
        AddCvc = "";
        IsAddCvcVisible = false;
        AddNotes = "";
    }

    private void PopulateModalFromRow(CardRowVm row)
    {
        AddTitle = row.Title;
        AddBank = row.Bank;
        AddCardholder = row.Cardholder;
        AddNumber = row.Number;
        AddExpiryMonth = row.ExpiryMonth;
        AddExpiryYear = row.ExpiryYear;
        AddCvc = row.Cvc;
        IsAddCvcVisible = false;
        AddNotes = row.Notes;
        AddCardType = string.IsNullOrWhiteSpace(row.CardType) ? CardRowVm.DefaultCardType : row.CardType;
        AddIssuer = string.IsNullOrWhiteSpace(row.Issuer) ? row.IssuerDisplay : row.Issuer;
        _lastAutoAddIssuer = AddIssuer;
    }

    private CardInput BuildInput(string digits, int expiryMonth, int expiryYear, string cvcDigits)
        => new(
            Title: AddTitle,
            Bank: AddBank,
            Cardholder: AddCardholder,
            Number: digits,
            ExpiryMonth: expiryMonth,
            ExpiryYear: expiryYear,
            Cvc: cvcDigits,
            Notes: AddNotes,
            Issuer: string.IsNullOrWhiteSpace(AddIssuer) ? CardRowVm.DetectIssuer(digits) : AddIssuer,
            CardType: string.IsNullOrWhiteSpace(AddCardType) ? CardRowVm.DefaultCardType : AddCardType);

    private static CardRowVm ToRow(CardEntry entry)
        => new(
            entry.Id,
            entry.Title,
            entry.Bank,
            entry.Cardholder,
            entry.Number,
            entry.ExpiryMonth.ToString("00"),
            entry.ExpiryYear.ToString(),
            entry.Cvc,
            entry.Notes,
            entry.Issuer,
            string.IsNullOrWhiteSpace(entry.CardType) ? CardRowVm.DefaultCardType : entry.CardType,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc);

    private static void ApplyEntry(CardRowVm row, CardEntry entry)
    {
        row.Title = entry.Title;
        row.Bank = entry.Bank;
        row.Cardholder = entry.Cardholder;
        row.Number = entry.Number;
        row.ExpiryMonth = entry.ExpiryMonth.ToString("00");
        row.ExpiryYear = entry.ExpiryYear.ToString();
        row.Cvc = entry.Cvc;
        row.Notes = entry.Notes;
        row.Issuer = entry.Issuer;
        row.CardType = string.IsNullOrWhiteSpace(entry.CardType) ? CardRowVm.DefaultCardType : entry.CardType;
        row.MarkSaved(entry.UpdatedAtUtc);
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

            var entries = await _cardService.ListAsync(_root.VaultPath, _root.VaultKey);
            _all.AddRange(entries.Select(ToRow));

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
                r.CardType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.ExpiryDisplay.Contains(q, StringComparison.OrdinalIgnoreCase));
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
        OnPropertyChanged(nameof(ActiveCardsCount));
        OnPropertyChanged(nameof(ExpiringSoonCount));
        OnPropertyChanged(nameof(ExpiredCardsCount));
        OnPropertyChanged(nameof(ActiveCardsSummary));
        OnPropertyChanged(nameof(ExpiringSoonSummary));
        OnPropertyChanged(nameof(ExpiredCardsSummary));
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
