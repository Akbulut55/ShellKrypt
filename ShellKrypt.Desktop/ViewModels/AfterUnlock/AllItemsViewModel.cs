using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Desktop.ViewModels;

public sealed class PageChipVm
{
    public PageChipVm(int number, bool isCurrent)
    {
        Number = number;
        IsCurrent = isCurrent;
    }

    public int Number { get; }
    public bool IsCurrent { get; }
    public string Label => Number.ToString(CultureInfo.InvariantCulture);
}

public sealed class AllItemEntry
{
    public AllItemEntry(
        string id,
        ItemType type,
        string title,
        string nameSubtitle,
        string identifierText,
        IReadOnlyList<string> labels,
        string searchText,
        bool favorite,
        string createdAtUtc,
        string updatedAtUtc,
        string copyValue,
        int expiryMonth = 0,
        int expiryYear = 0)
    {
        Id = id;
        Type = type;
        Title = title;
        NameSubtitle = nameSubtitle;
        IdentifierText = identifierText;
        Labels = labels;
        SearchText = searchText;
        Favorite = favorite;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        CopyValue = copyValue;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
    }

    public string Id { get; }
    public ItemType Type { get; }
    public string Title { get; }
    public string NameSubtitle { get; }
    public string IdentifierText { get; }
    public IReadOnlyList<string> Labels { get; }
    public string SearchText { get; }
    public bool Favorite { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; }
    public string CopyValue { get; }
    public int ExpiryMonth { get; }
    public int ExpiryYear { get; }

    public string TypeLabel => Type.ToString();

    public string DisplayTypeLabel => Type switch
    {
        ItemType.Web => "LOGIN",
        ItemType.Card => "CARD",
        ItemType.Note => "MARKDOWN NOTE",
        ItemType.Authenticator => "AUTHENTICATOR",
        ItemType.ApiKey => "API KEY",
        _ => TypeLabel.ToUpperInvariant()
    };

    public string IconGlyph => Type switch
    {
        ItemType.Web => "WB",
        ItemType.Card => "CC",
        ItemType.Note => "MD",
        ItemType.Authenticator => "AU",
        ItemType.ApiKey => "AK",
        _ => "IT"
    };

    public string IconBackground => Type switch
    {
        ItemType.Web => "TypeWebBackgroundBrush",
        ItemType.Card => "TypeCardBackgroundBrush",
        ItemType.Note => "TypeNoteBackgroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorBackgroundBrush",
        ItemType.ApiKey => "TypeApiKeyBackgroundBrush",
        _ => "InfoMutedBrush"
    };

    public string IconForeground => Type switch
    {
        ItemType.Web => "TypeWebForegroundBrush",
        ItemType.Card => "TypeCardForegroundBrush",
        ItemType.Note => "TypeNoteForegroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorForegroundBrush",
        ItemType.ApiKey => "TypeApiKeyForegroundBrush",
        _ => "TextPrimaryBrush"
    };

    public string TypeBadgeBackground => Type switch
    {
        ItemType.Web => "TypeWebBackgroundBrush",
        ItemType.Card => "TypeCardBackgroundBrush",
        ItemType.Note => "TypeNoteBackgroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorBackgroundBrush",
        ItemType.ApiKey => "TypeApiKeyBackgroundBrush",
        _ => "InfoMutedBrush"
    };

    public string TypeBadgeForeground => Type switch
    {
        ItemType.Web => "TypeWebForegroundBrush",
        ItemType.Card => "TypeCardForegroundBrush",
        ItemType.Note => "TypeNoteForegroundBrush",
        ItemType.Authenticator => "TypeAuthenticatorForegroundBrush",
        ItemType.ApiKey => "TypeApiKeyForegroundBrush",
        _ => "TextPrimaryBrush"
    };

    public string FavoriteGlyph => Favorite ? "★" : string.Empty;
    public string LabelsDisplay => Labels.Count == 0 ? "No labels" : string.Join(", ", Labels);
    public string IdentifierDisplay => string.IsNullOrWhiteSpace(IdentifierText) ? "N/A" : IdentifierText.Trim();
    public string NameSubtitleDisplay => string.IsNullOrWhiteSpace(NameSubtitle) ? "Encrypted vault item" : NameSubtitle.Trim();
    public string UpdatedDisplay => FormatRelativeDate(UpdatedAtUtc);
    public string UpdatedAbsoluteDisplay => FormatAbsoluteDate(UpdatedAtUtc);
    public string CreatedDisplay => FormatAbsoluteDate(CreatedAtUtc);
    public bool IsCardExpiryUrgent => TryGetExpiryDate(out var expiry) &&
                                      expiry >= DateTime.Today &&
                                      expiry <= DateTime.Today.AddMonths(3);

    public bool IsRecent(int recentWindowDays = 30)
    {
        if (!TryParseDate(UpdatedAtUtc, out var updated))
            return false;

        return updated >= DateTimeOffset.UtcNow.AddDays(-recentWindowDays);
    }

    private static string FormatRelativeDate(string? value)
    {
        if (!TryParseDate(value, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var now = DateTimeOffset.Now;
        var delta = now - local;

        if (delta < TimeSpan.Zero)
            return local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)} minute{Pluralize(delta.TotalMinutes)} ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)} hour{Pluralize(delta.TotalHours)} ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)} day{Pluralize(delta.TotalDays)} ago";
        if (local.Year == now.Year)
            return local.ToString("MMM d", CultureInfo.InvariantCulture);

        return local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatAbsoluteDate(string? value)
    {
        if (!TryParseDate(value, out var parsed))
            return "Unknown";

        return parsed.ToLocalTime().ToString("MMM d, yyyy '|' HH:mm", CultureInfo.InvariantCulture);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset parsed)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed);

    private bool TryGetExpiryDate(out DateTime expiry)
    {
        expiry = DateTime.MaxValue;

        if (Type != ItemType.Card || ExpiryMonth is < 1 or > 12 || ExpiryYear < 2000)
            return false;

        expiry = new DateTime(ExpiryYear, ExpiryMonth, DateTime.DaysInMonth(ExpiryYear, ExpiryMonth));
        return true;
    }

    private static string Pluralize(double value)
        => Math.Abs(value) >= 2 ? "s" : string.Empty;
}

internal enum AllItemsSortMode
{
    UpdatedDescending,
    Alphabetical,
    TypeThenTitle
}

public sealed class AllItemsViewModel : ViewModelBase
{
    private const int PageSize = 5;
    private const int RecentWindowDays = 30;

    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IItemRepository _repo;
    private readonly List<AllItemEntry> _allItems = new();
    private readonly List<AllItemEntry> _filteredItems = new();
    private readonly List<string> _webPasswords = new();
    private AllItemsSortMode _sortMode = AllItemsSortMode.UpdatedDescending;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    public AllItemsViewModel(MainWindowViewModel root, ShellViewModel shell, IItemRepository repo)
    {
        _root = root;
        _shell = shell;
        _repo = repo;

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
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Math.Max(FilteredCount, 1) / (double)PageSize));
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

    private void ShowAll()
    {
        ActiveScope = "all";
        ActiveType = "all";
    }
    private void ShowFavorites() => ActiveScope = "favorites";
    private void ShowRecent() => ActiveScope = "recent";
    private void ShowWebTypes() => ActiveType = "web";
    private void ShowCardTypes() => ActiveType = "card";
    private void ShowNoteTypes() => ActiveType = "note";
    private void ShowAuthenticatorTypes() => ActiveType = "authenticator";
    private void ShowApiKeyTypes() => ActiveType = "api";

    private async Task RefreshAsync()
    {
        await LoadAsync(SelectedRow?.Id);
    }

    public Task RefreshAfterMutationAsync(string? selectItemId = null)
        => LoadAsync(selectItemId);

    private void ResetFilters()
    {
        Error = string.Empty;
        SearchText = string.Empty;
        ActiveScope = "all";
        ActiveType = "all";
        CurrentPage = 1;
        ApplyFilter();
    }

    private void CycleSort()
    {
        Error = string.Empty;
        _sortMode = _sortMode switch
        {
            AllItemsSortMode.UpdatedDescending => AllItemsSortMode.Alphabetical,
            AllItemsSortMode.Alphabetical => AllItemsSortMode.TypeThenTitle,
            _ => AllItemsSortMode.UpdatedDescending
        };

        CurrentPage = 1;
        ApplyFilter();
    }

    private void GoPreviousPage()
    {
        if (CanGoPrevious)
            CurrentPage--;
    }

    private void GoNextPage()
    {
        if (CanGoNext)
            CurrentPage++;
    }

    private void GoToPage(PageChipVm? page)
    {
        if (page is not null)
            CurrentPage = page.Number;
    }

    private void AddItem()
    {
        Error = string.Empty;

        var targetType = ActiveType switch
        {
            "web" => ItemType.Web,
            "card" => ItemType.Card,
            "note" => ItemType.Note,
            "authenticator" => ItemType.Authenticator,
            "api" => ItemType.ApiKey,
            _ => SelectedRow?.Type ?? ItemType.Web
        };

        switch (targetType)
        {
            case ItemType.Web:
                _shell.ShowWebLogins();
                ExecuteCommand(_shell.WebLogins.AddNewCommand);
                break;

            case ItemType.Card:
                _shell.ShowCards();
                ExecuteCommand(_shell.Cards.AddNewCommand);
                break;

            case ItemType.Note:
                _shell.ShowMarkdownNotes();
                ExecuteCommand(_shell.MarkdownNotes.NewNoteCommand);
                break;

            case ItemType.Authenticator:
                _shell.ShowAuthenticator();
                ExecuteCommand(_shell.Authenticator.AddNewCommand);
                break;

            case ItemType.ApiKey:
                _shell.ShowApiKeys();
                ExecuteCommand(_shell.ApiKeys.AddNewCommand);
                break;
        }
    }

    private void OpenRow(AllItemEntry? row)
    {
        if (row is null)
            return;

        Error = string.Empty;

        switch (row.Type)
        {
            case ItemType.Web:
                _shell.ShowWebLogins();
                break;
            case ItemType.Card:
                _shell.ShowCards();
                break;
            case ItemType.Note:
                _shell.ShowMarkdownNotes();
                break;
            case ItemType.Authenticator:
                _shell.ShowAuthenticator();
                _ = _shell.ShowAuthenticatorByIdAsync(row.Id);
                break;
            case ItemType.ApiKey:
                _shell.ShowApiKeys();
                _ = _shell.ShowApiKeyByIdAsync(row.Id);
                break;
        }
    }

    private async Task LoadAsync(string? selectItemId = null)
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        IsBusy = true;
        try
        {
            _allItems.Clear();
            _filteredItems.Clear();
            _webPasswords.Clear();
            Rows.Clear();
            PageChips.Clear();

            var rows = await _repo.ListAsync(_root.VaultPath, _root.VaultKey);

            foreach (var row in rows)
                _allItems.Add(BuildEntry(row));

            TotalCount = _allItems.Count;
            WebCount = _allItems.Count(x => x.Type == ItemType.Web);
            CardCount = _allItems.Count(x => x.Type == ItemType.Card);
            NoteCount = _allItems.Count(x => x.Type == ItemType.Note);
            AuthenticatorCount = _allItems.Count(x => x.Type == ItemType.Authenticator);
            ApiKeyCount = _allItems.Count(x => x.Type == ItemType.ApiKey);
            WeakPasswordCount = _webPasswords.Count(IsWeakPassword);
            ReusedPasswordCount = CountReusedPasswords(_webPasswords);
            ExpiringSoonCardCount = _allItems.Count(x => x.IsCardExpiryUrgent);
            CreatedThisMonthCount = CountCreatedThisMonth(_allItems);

            ApplyFilter();

            if (!string.IsNullOrWhiteSpace(selectItemId))
            {
                SelectedRow = Rows.FirstOrDefault(x => x.Id == selectItemId)
                              ?? _filteredItems.FirstOrDefault(x => x.Id == selectItemId)
                              ?? Rows.FirstOrDefault();
            }
            else if (SelectedRow is not null)
            {
                SelectedRow = Rows.FirstOrDefault(x => x.Id == SelectedRow.Id)
                              ?? _filteredItems.FirstOrDefault(x => x.Id == SelectedRow.Id)
                              ?? Rows.FirstOrDefault();
            }
            else
            {
                SelectedRow = Rows.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        _filteredItems.Clear();

        var query = SearchText?.Trim();
        IEnumerable<AllItemEntry> filtered = _allItems;

        filtered = ActiveScope switch
        {
            "favorites" => filtered.Where(x => x.Favorite),
            "recent" => filtered.Where(x => x.IsRecent(RecentWindowDays)),
            _ => filtered
        };

        filtered = ActiveType switch
        {
            "web" => filtered.Where(x => x.Type == ItemType.Web),
            "card" => filtered.Where(x => x.Type == ItemType.Card),
            "note" => filtered.Where(x => x.Type == ItemType.Note),
            "authenticator" => filtered.Where(x => x.Type == ItemType.Authenticator),
            "api" => filtered.Where(x => x.Type == ItemType.ApiKey),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(x => x.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = _sortMode switch
        {
            AllItemsSortMode.Alphabetical => filtered.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
            AllItemsSortMode.TypeThenTitle => filtered
                .OrderBy(x => x.Type switch
                {
                    ItemType.Web => 0,
                    ItemType.Card => 1,
                    ItemType.Note => 2,
                    ItemType.Authenticator => 3,
                    ItemType.ApiKey => 4,
                    _ => 99
                })
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(GetUpdatedSortValue).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in filtered)
            _filteredItems.Add(item);

        FilteredCount = _filteredItems.Count;

        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;
        else
            RefreshVisibleRows();

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private void RefreshVisibleRows()
    {
        Rows.Clear();

        var safePage = Math.Clamp(CurrentPage, 1, TotalPages);
        if (safePage != CurrentPage)
        {
            _currentPage = safePage;
            OnPropertyChanged(nameof(CurrentPage));
        }

        foreach (var row in _filteredItems.Skip((safePage - 1) * PageSize).Take(PageSize))
            Rows.Add(row);

        if (SelectedRow is not null && !Rows.Any(x => x.Id == SelectedRow.Id))
            SelectedRow = Rows.FirstOrDefault();
        else if (SelectedRow is null)
            SelectedRow = Rows.FirstOrDefault();

        RefreshPageChips();
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(FooterSummary));
    }

    private void RefreshPageChips()
    {
        PageChips.Clear();

        var totalPages = TotalPages;
        if (totalPages <= 0)
            return;

        var start = Math.Max(1, CurrentPage - 1);
        var end = Math.Min(totalPages, start + 2);

        if (end - start < 2)
            start = Math.Max(1, end - 2);

        for (var page = start; page <= end; page++)
            PageChips.Add(new PageChipVm(page, page == CurrentPage));
    }

    private AllItemEntry BuildEntry(VaultItemRow row)
    {
        var labels = row.Labels.Select(x => x.Name).ToArray();

        return row.Header.Type switch
        {
            ItemType.Web => BuildWebEntry(row, labels),
            ItemType.Card => BuildCardEntry(row, labels),
            ItemType.Note => BuildNoteEntry(row, labels),
            ItemType.Authenticator => BuildAuthenticatorEntry(row, labels),
            ItemType.ApiKey => BuildApiKeyEntry(row, labels),
            _ => new AllItemEntry(
                row.Header.Id,
                row.Header.Type,
                "Unknown item",
                "Encrypted vault item",
                "N/A",
                labels,
                string.Join(" ", labels),
                row.Header.Favorite,
                row.Header.CreatedAtUtc,
                row.Header.UpdatedAtUtc,
                string.Empty)
        };
    }

    private AllItemEntry BuildWebEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(_root.VaultKey, row.Header, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<WebPayload>(json, JsonOpts)
            ?? new WebPayload("", "", "", "", "");

        var title = FirstNonEmpty(payload.Title, payload.Url, payload.Username, "Untitled login");
        var subtitle = FirstNonEmpty(payload.Url, "Encrypted login");
        var identifier = FirstNonEmpty(payload.Username, "N/A");
        var copyValue = FirstNonEmpty(payload.Username, payload.Password, payload.Url, title);
        var searchText = BuildSearchText(title, subtitle, identifier, payload.Notes, payload.Password, string.Join(" ", labels), row.Header.Favorite ? "favorite" : string.Empty);

        _webPasswords.Add(payload.Password ?? string.Empty);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue);
    }

    private AllItemEntry BuildAuthenticatorEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(_root.VaultKey, row.Header, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<AuthenticatorPayload>(json, JsonOpts)
            ?? new AuthenticatorPayload("", "", "", "", "", 6, 30, "", "", "", 0);

        var keyType = payload.KeyType?.Trim().ToLowerInvariant() switch
        {
            "counter-based" or "counter" or "hotp" => "Counter based code",
            _ => "Time based code"
        };
        var title = FirstNonEmpty(payload.ServiceName, payload.Issuer, "Authenticator");
        var subtitle = keyType;
        var identifier = payload.KeyType?.Trim().ToLowerInvariant() switch
        {
            "counter-based" or "counter" or "hotp" => $"Counter {Math.Max(0, payload.Counter)}",
            _ => "Rotates every 30 seconds"
        };
        var searchText = BuildSearchText(
            title,
            subtitle,
            identifier,
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            string.Empty);
    }

    private AllItemEntry BuildCardEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(_root.VaultKey, row.Header, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<CardPayload>(json, JsonOpts)
            ?? new CardPayload("", "", "", 0, 0, "", "");

        var title = FirstNonEmpty(payload.Title, "Untitled card");
        var maskedNumber = MaskCardNumber(payload.Number);
        var subtitle = FirstNonEmpty(maskedNumber, "Encrypted card");
        var identifier = FirstNonEmpty(payload.Cardholder, maskedNumber, "N/A");
        var digits = new string((payload.Number ?? string.Empty).Where(char.IsDigit).ToArray());
        var copyValue = FirstNonEmpty(digits, payload.Cardholder, title);
        var searchText = BuildSearchText(title, subtitle, identifier, payload.Notes, payload.Number, payload.Cvc, string.Join(" ", labels), row.Header.Favorite ? "favorite" : string.Empty);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue,
            payload.ExpiryMonth,
            payload.ExpiryYear);
    }

    private AllItemEntry BuildNoteEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(_root.VaultKey, row.Header, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<NotePayload>(json, JsonOpts)
            ?? new NotePayload("", "");

        var title = FirstNonEmpty(payload.Title, "Untitled markdown note");
        var snippet = TrimSnippet(payload.Content, 72);
        var subtitle = FirstNonEmpty(snippet, "Encrypted markdown note");
        var copyValue = FirstNonEmpty(payload.Content, title);
        var searchText = BuildSearchText(title, snippet, payload.Content, string.Join(" ", labels), row.Header.Favorite ? "favorite" : string.Empty);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            "N/A",
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue);
    }

    private AllItemEntry BuildApiKeyEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = VaultPayloadProtector.DecryptItemPayload(_root.VaultKey, row.Header, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<ApiKeyPayload>(json, JsonOpts)
            ?? new ApiKeyPayload("", "", "", "", Array.Empty<ApiKeyFieldPayload>());

        var title = FirstNonEmpty(payload.Name, "Untitled API key");
        var provider = FirstNonEmpty(payload.Provider, "Unknown provider");
        var environment = FirstNonEmpty(payload.Environment, "Production");
        var primaryField = payload.Fields
            .OrderBy(field => field.SortOrder)
            .FirstOrDefault(field => field.IsSensitive && field.IsCopyable)
            ?? payload.Fields.OrderBy(field => field.SortOrder).FirstOrDefault(field => field.IsCopyable)
            ?? payload.Fields.OrderBy(field => field.SortOrder).FirstOrDefault();
        var fieldSummary = primaryField is null
            ? "No fields"
            : $"{primaryField.Label}: {MaskApiKeyValue(primaryField.Value)}";
        var identifier = FirstNonEmpty(provider, environment, primaryField?.Label, "N/A");
        var copyValue = FirstNonEmpty(primaryField?.Value, title);
        var searchText = BuildSearchText(
            title,
            provider,
            environment,
            payload.Notes,
            string.Join(" ", payload.Fields.Select(field => $"{field.Label} {field.FieldType} {field.Value}")),
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            title,
            fieldSummary,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            copyValue);
    }

    private static void ExecuteCommand(ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
            command.Execute(null);
    }

    private static string BuildSearchText(params string?[] parts)
        => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);

    private static string FirstNonEmpty(params string?[] parts)
        => parts.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part))?.Trim() ?? string.Empty;

    private static string TrimSnippet(string? text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var value = text.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static string MaskCardNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return string.Empty;

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        return $"**** **** **** {digits[^4..]}";
    }

    private static string MaskApiKeyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Encrypted API field";

        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? "****" : $"**** **** {trimmed[^4..]}";
    }

    private static bool IsWeakPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return true;

        var value = password.Trim();
        if (value.Length < 12)
            return true;

        var hasLetter = value.Any(char.IsLetter);
        var hasDigit = value.Any(char.IsDigit);
        var hasSymbol = value.Any(ch => !char.IsLetterOrDigit(ch));
        return !(hasLetter && hasDigit && hasSymbol);
    }

    private static int CountReusedPasswords(IEnumerable<string> passwords)
    {
        return passwords
            .Where(password => !string.IsNullOrWhiteSpace(password))
            .GroupBy(password => password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count());
    }

    private static int CountCreatedThisMonth(IEnumerable<AllItemEntry> items)
    {
        var now = DateTimeOffset.UtcNow;
        return items.Count(item => DateTimeOffset.TryParse(item.CreatedAtUtc, out var created)
                                   && created.Year == now.Year
                                   && created.Month == now.Month);
    }

    private static DateTimeOffset GetUpdatedSortValue(AllItemEntry item)
        => DateTimeOffset.TryParse(item.UpdatedAtUtc, out var updated) ? updated : DateTimeOffset.MinValue;
}
