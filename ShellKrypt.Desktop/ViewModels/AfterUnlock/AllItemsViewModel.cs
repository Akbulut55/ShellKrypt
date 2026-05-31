using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel : ViewModelBase
{
    private const int PageSize = 5;

    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IVaultItemSummaryService _summaryService;
    private AllItemsSortMode _sortMode = AllItemsSortMode.UpdatedDescending;

    private AllItemEntry? _selectedRow;
    private string _searchText = string.Empty;
    private string _activeScope = "all";
    private string _activeType = "all";
    private string _error = string.Empty;
    private bool _isBusy;
    private int _totalCount;
    private int _webCount;
    private int _cardCount;
    private int _noteCount;
    private int _authenticatorCount;
    private int _apiKeyCount;
    private int _filteredCount;
    private int _weakPasswordCount;
    private int _reusedPasswordCount;
    private int _expiringSoonCardCount;
    private int _createdThisMonthCount;
    private int _currentPage = 1;

    public AllItemsViewModel(MainWindowViewModel root, ShellViewModel shell, IVaultItemSummaryService summaryService)
    {
        _root = root;
        _shell = shell;
        _summaryService = summaryService;

        Rows = new ObservableCollection<AllItemEntry>();
        PageChips = new ObservableCollection<PageChipVm>();

        ShowAllCommand = new RelayCommand(ShowAll);
        ShowFavoritesCommand = new RelayCommand(ShowFavorites);
        ShowRecentCommand = new RelayCommand(ShowRecent);
        ShowWebTypesCommand = new RelayCommand(ShowWebTypes);
        ShowCardTypesCommand = new RelayCommand(ShowCardTypes);
        ShowNoteTypesCommand = new RelayCommand(ShowNoteTypes);
        ShowAuthenticatorTypesCommand = new RelayCommand(ShowAuthenticatorTypes);
        ShowApiKeyTypesCommand = new RelayCommand(ShowApiKeyTypes);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        CycleSortCommand = new RelayCommand(CycleSort);
        GoPreviousPageCommand = new RelayCommand(GoPreviousPage);
        GoNextPageCommand = new RelayCommand(GoNextPage);
        GoToPageCommand = new RelayCommand<PageChipVm?>(GoToPage);
        AddItemCommand = new RelayCommand(AddItem);
        OpenRowCommand = new RelayCommand<AllItemEntry?>(OpenRow);

        _ = LoadAsync();
    }

    public ObservableCollection<AllItemEntry> Rows { get; }
    public ObservableCollection<PageChipVm> PageChips { get; }

    public ICommand ShowAllCommand { get; }
    public ICommand ShowFavoritesCommand { get; }
    public ICommand ShowRecentCommand { get; }
    public ICommand ShowWebTypesCommand { get; }
    public ICommand ShowCardTypesCommand { get; }
    public ICommand ShowNoteTypesCommand { get; }
    public ICommand ShowAuthenticatorTypesCommand { get; }
    public ICommand ShowApiKeyTypesCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand CycleSortCommand { get; }
    public ICommand GoPreviousPageCommand { get; }
    public ICommand GoNextPageCommand { get; }
    public ICommand GoToPageCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand OpenRowCommand { get; }

    public AllItemEntry? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
                Error = string.Empty;
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                CurrentPage = 1;
                ApplyFilter();
            }
        }
    }

    public string ActiveScope
    {
        get => _activeScope;
        set
        {
            if (SetProperty(ref _activeScope, value))
            {
                CurrentPage = 1;
                OnPropertyChanged(nameof(IsAllScopeActive));
                OnPropertyChanged(nameof(IsFavoritesScopeActive));
                OnPropertyChanged(nameof(IsRecentScopeActive));
                OnPropertyChanged(nameof(EmptyStateTitle));
                OnPropertyChanged(nameof(EmptyStateSubtitle));
                ApplyFilter();
            }
        }
    }

    public string ActiveType
    {
        get => _activeType;
        set
        {
            if (SetProperty(ref _activeType, value))
            {
                CurrentPage = 1;
                OnPropertyChanged(nameof(IsAllTypeActive));
                OnPropertyChanged(nameof(IsWebTypeActive));
                OnPropertyChanged(nameof(IsCardTypeActive));
                OnPropertyChanged(nameof(IsNoteTypeActive));
                OnPropertyChanged(nameof(IsAuthenticatorTypeActive));
                OnPropertyChanged(nameof(IsApiKeyTypeActive));
                OnPropertyChanged(nameof(AddItemButtonText));
                ApplyFilter();
            }
        }
    }

    public string Error
    {
        get => _error;
        set
        {
            if (SetProperty(ref _error, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
                OnPropertyChanged(nameof(TotalItemsDeltaText));
        }
    }

    public int WebCount
    {
        get => _webCount;
        private set => SetProperty(ref _webCount, value);
    }

    public int CardCount
    {
        get => _cardCount;
        private set => SetProperty(ref _cardCount, value);
    }

    public int NoteCount
    {
        get => _noteCount;
        private set => SetProperty(ref _noteCount, value);
    }

    public int AuthenticatorCount
    {
        get => _authenticatorCount;
        private set => SetProperty(ref _authenticatorCount, value);
    }

    public int ApiKeyCount
    {
        get => _apiKeyCount;
        private set => SetProperty(ref _apiKeyCount, value);
    }

    public int FilteredCount
    {
        get => _filteredCount;
        private set
        {
            if (SetProperty(ref _filteredCount, value))
            {
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageSummary));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(FooterSummary));
            }
        }
    }

    public int WeakPasswordCount
    {
        get => _weakPasswordCount;
        private set
        {
            if (SetProperty(ref _weakPasswordCount, value))
                OnPropertyChanged(nameof(WeakPasswordSubtitle));
        }
    }

    public int ReusedPasswordCount
    {
        get => _reusedPasswordCount;
        private set
        {
            if (SetProperty(ref _reusedPasswordCount, value))
                OnPropertyChanged(nameof(ReusedPasswordSubtitle));
        }
    }

    public int ExpiringSoonCardCount
    {
        get => _expiringSoonCardCount;
        private set
        {
            if (SetProperty(ref _expiringSoonCardCount, value))
                OnPropertyChanged(nameof(ExpiringSoonCardSubtitle));
        }
    }

    public int CreatedThisMonthCount
    {
        get => _createdThisMonthCount;
        private set
        {
            if (SetProperty(ref _createdThisMonthCount, value))
                OnPropertyChanged(nameof(TotalItemsDeltaText));
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            var safeValue = value < 1 ? 1 : value;
            if (SetProperty(ref _currentPage, safeValue))
                RefreshVisibleRows();
        }
    }

    public bool IsAllScopeActive => string.Equals(ActiveScope, "all", StringComparison.Ordinal);
    public bool IsFavoritesScopeActive => string.Equals(ActiveScope, "favorites", StringComparison.Ordinal);
    public bool IsRecentScopeActive => string.Equals(ActiveScope, "recent", StringComparison.Ordinal);

    public bool IsAllTypeActive => string.Equals(ActiveType, "all", StringComparison.Ordinal);
    public bool IsWebTypeActive => string.Equals(ActiveType, "web", StringComparison.Ordinal);
    public bool IsCardTypeActive => string.Equals(ActiveType, "card", StringComparison.Ordinal);
    public bool IsNoteTypeActive => string.Equals(ActiveType, "note", StringComparison.Ordinal);
    public bool IsAuthenticatorTypeActive => string.Equals(ActiveType, "authenticator", StringComparison.Ordinal);
    public bool IsApiKeyTypeActive => string.Equals(ActiveType, "api", StringComparison.Ordinal);

    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public int TotalPages => DesktopPagination.GetTotalPages(FilteredCount, PageSize);
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public string TotalItemsDeltaText => CreatedThisMonthCount <= 0 ? "0 items this month" : $"+{CreatedThisMonthCount} items this month";
    public string WeakPasswordSubtitle => WeakPasswordCount <= 0 ? "0 passwords needs attention" : $"{WeakPasswordCount} passwords needs attention";
    public string ReusedPasswordSubtitle => ReusedPasswordCount <= 0 ? "No overlap found" : "Security risk";
    public string ExpiringSoonCardSubtitle => ExpiringSoonCardCount switch
    {
        0 => "No urgent renewals",
        1 => "1 card expires within 3 months",
        _ => $"{ExpiringSoonCardCount} cards expire within 3 months"
    };
    public string FooterSummary => $"Showing {Rows.Count} of {FilteredCount} items";

    public string AddItemButtonText => ActiveType switch
    {
        "web" => "+ Add Login",
        "card" => "+ Add Card",
        "note" => "+ Add Note",
        "authenticator" => "+ Add Authenticator",
        "api" => "+ Add API Key",
        _ => "+ Add Item"
    };

    public string EmptyStateTitle => ActiveScope switch
    {
        "favorites" => "No favorites match this view",
        "recent" => "No recent items found",
        _ => "No vault items match this view"
    };

    public string EmptyStateSubtitle => ActiveScope switch
    {
        "favorites" => "Mark items as favorites from their dedicated sections, then they will surface here.",
        "recent" => "Try a wider search, or switch back to all items to inspect the full vault.",
        _ => "Adjust the search query or category filter to surface another item set."
    };
}
