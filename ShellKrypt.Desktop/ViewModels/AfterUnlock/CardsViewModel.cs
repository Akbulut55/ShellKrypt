using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel : ViewModelBase
{
    private const string AllBankFilter = "Bank: All";
    private const string AllCardTypeFilter = "Type: All";
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
    public ObservableCollection<string> BankFilters { get; } = new() { AllBankFilter };
    public ObservableCollection<string> CardTypeFilters { get; } = new() { AllCardTypeFilter };
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
    [ObservableProperty] private string selectedBankFilter = AllBankFilter;
    [ObservableProperty] private string selectedCardTypeFilter = AllCardTypeFilter;
    [ObservableProperty] private string selectedSortOption = SortNewest;
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
        ? T(_root, "Cards.Summary.ActiveOne")
        : T(_root, "Cards.Summary.ActiveMany", ActiveCardsCount);
    public string ExpiringSoonSummary => ExpiringSoonCount == 1
        ? T(_root, "Cards.Summary.ExpiringOne")
        : T(_root, "Cards.Summary.ExpiringMany", ExpiringSoonCount);
    public string ExpiredCardsSummary => ExpiredCardsCount == 1
        ? T(_root, "Cards.Summary.ExpiredOne")
        : T(_root, "Cards.Summary.ExpiredMany", ExpiredCardsCount);
    public string ItemsSummary => T(_root, "Cards.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public string EmptyTableTitle => _all.Count == 0
        ? T(_root, "Cards.Empty.NoneTitle")
        : T(_root, "Cards.Empty.NoMatchTitle");
    public string EmptyTableSubtitle => _all.Count == 0
        ? T(_root, "Cards.Empty.NoneSubtitle")
        : T(_root, "Cards.Empty.NoMatchSubtitle");
    public string CardModalTitle => IsAddCardMode
        ? T(_root, "Cards.Modal.AddTitle")
        : IsCardDeleteConfirming
            ? T(_root, "Cards.Modal.DeleteTitle")
            : IsCardDetailsEditing
                ? T(_root, "Cards.Modal.EditTitle")
                : T(_root, "Cards.Modal.DetailsTitle");
    public string CardModalSubtitle => IsAddCardMode
        ? T(_root, "Cards.Modal.AddSubtitle")
        : IsCardDeleteConfirming
            ? T(_root, "Cards.Modal.DeleteSubtitle")
            : IsCardDetailsEditing
                ? T(_root, "Cards.Modal.EditSubtitle")
                : T(_root, "Cards.Modal.DetailsSubtitle");
    public bool IsCardDetailsViewMode => !IsAddCardMode && !IsCardDetailsEditing && !IsCardDeleteConfirming;
    public bool IsCardDetailsEditMode => !IsAddCardMode && IsCardDetailsEditing && !IsCardDeleteConfirming;
    public bool IsCardDetailsDeleteConfirmMode => !IsAddCardMode && IsCardDeleteConfirming;
    public bool IsCardFormReadOnly => !IsAddCardMode && !IsCardDetailsEditing;
    public bool IsCardFormEditable => IsAddCardMode || IsCardDetailsEditing;
    public string AddCvcVisibilityLabel => IsAddCvcVisible ? T(_root, "Common.Hide") : T(_root, "Common.Reveal");
    public string CardModalFooterText => IsCardDetailsDeleteConfirmMode
        ? T(_root, "Cards.Modal.DeleteFooter", string.IsNullOrWhiteSpace(AddTitle) ? T(_root, "Cards.ThisCard") : AddTitle)
        : T(_root, "Cards.Modal.Footer");

    public CardsViewModel(MainWindowViewModel root, ICardService cardService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _cardService = cardService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedBankFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedCardTypeFilterChanged(string value) => ApplyFilter();
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

    public override void RefreshLocalization()
    {
        foreach (var row in _all)
            row.RefreshLocalization();

        NotifyLocalized(
            nameof(ActiveCardsSummary),
            nameof(ExpiringSoonSummary),
            nameof(ExpiredCardsSummary),
            nameof(ItemsSummary),
            nameof(EmptyTableTitle),
            nameof(EmptyTableSubtitle),
            nameof(CardModalTitle),
            nameof(CardModalSubtitle),
            nameof(AddCvcVisibilityLabel),
            nameof(CardModalFooterText));
    }
}
