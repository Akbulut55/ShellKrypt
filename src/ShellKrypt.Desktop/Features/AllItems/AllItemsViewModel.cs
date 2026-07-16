using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Items;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.AllItems;

public sealed partial class AllItemsViewModel : ViewModelBase
{
    private const int AllRowsQuerySize = int.MaxValue;

    private readonly AllItemsRuntime _root;
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
    private int _projectSecretCount;
    private int _filteredCount;
    private int _weakPasswordCount;
    private int _reusedPasswordCount;
    private int _expiringSoonCardCount;
    private int _createdThisMonthCount;

    public AllItemsViewModel(AllItemsRuntime root, ShellViewModel shell, IVaultItemSummaryService summaryService)
    {
        _root = root;
        _shell = shell;
        _summaryService = summaryService;

        Rows = new ObservableCollection<AllItemEntry>();

        ShowAllCommand = new RelayCommand(ShowAll);
        ShowFavoritesCommand = new RelayCommand(ShowFavorites);
        ShowRecentCommand = new RelayCommand(ShowRecent);
        ShowWebTypesCommand = new RelayCommand(ShowWebTypes);
        ShowCardTypesCommand = new RelayCommand(ShowCardTypes);
        ShowNoteTypesCommand = new RelayCommand(ShowNoteTypes);
        ShowAuthenticatorTypesCommand = new RelayCommand(ShowAuthenticatorTypes);
        ShowApiKeyTypesCommand = new RelayCommand(ShowApiKeyTypes);
        ShowProjectSecretTypesCommand = new RelayCommand(ShowProjectSecretTypes);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        CycleSortCommand = new RelayCommand(CycleSort);
        AddItemCommand = new RelayCommand(AddItem);
        OpenRowCommand = new RelayCommand<AllItemEntry?>(OpenRow);

        _ = LoadAsync();
    }

    public ObservableCollection<AllItemEntry> Rows { get; }

    public ICommand ShowAllCommand { get; }
    public ICommand ShowFavoritesCommand { get; }
    public ICommand ShowRecentCommand { get; }
    public ICommand ShowWebTypesCommand { get; }
    public ICommand ShowCardTypesCommand { get; }
    public ICommand ShowNoteTypesCommand { get; }
    public ICommand ShowAuthenticatorTypesCommand { get; }
    public ICommand ShowApiKeyTypesCommand { get; }
    public ICommand ShowProjectSecretTypesCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand CycleSortCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand OpenRowCommand { get; }

    public override void RefreshLocalization()
    {
        foreach (var row in Rows)
            row.RefreshLocalization();

        NotifyLocalized(
            nameof(TotalItemsDeltaText),
            nameof(WeakPasswordSubtitle),
            nameof(ReusedPasswordSubtitle),
            nameof(ExpiringSoonCardSubtitle),
            nameof(FooterSummary),
            nameof(AllFilterLabel),
            nameof(WebFilterLabel),
            nameof(CardFilterLabel),
            nameof(AuthenticatorFilterLabel),
            nameof(ApiKeyFilterLabel),
            nameof(ProjectSecretFilterLabel),
            nameof(NoteFilterLabel),
            nameof(AddItemButtonText),
            nameof(EmptyStateTitle),
            nameof(EmptyStateSubtitle));
    }
}
