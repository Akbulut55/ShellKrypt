namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel
{
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
                OnPropertyChanged(nameof(IsAllTypeActive));
                OnPropertyChanged(nameof(IsWebTypeActive));
                OnPropertyChanged(nameof(IsCardTypeActive));
                OnPropertyChanged(nameof(IsNoteTypeActive));
                OnPropertyChanged(nameof(IsAuthenticatorTypeActive));
                OnPropertyChanged(nameof(IsApiKeyTypeActive));
                OnPropertyChanged(nameof(IsProjectSecretTypeActive));
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
            {
                OnPropertyChanged(nameof(TotalItemsDeltaText));
                OnPropertyChanged(nameof(AllFilterLabel));
            }
        }
    }

    public int WebCount
    {
        get => _webCount;
        private set
        {
            if (SetProperty(ref _webCount, value))
                OnPropertyChanged(nameof(WebFilterLabel));
        }
    }

    public int CardCount
    {
        get => _cardCount;
        private set
        {
            if (SetProperty(ref _cardCount, value))
                OnPropertyChanged(nameof(CardFilterLabel));
        }
    }

    public int NoteCount
    {
        get => _noteCount;
        private set
        {
            if (SetProperty(ref _noteCount, value))
                OnPropertyChanged(nameof(NoteFilterLabel));
        }
    }

    public int AuthenticatorCount
    {
        get => _authenticatorCount;
        private set
        {
            if (SetProperty(ref _authenticatorCount, value))
                OnPropertyChanged(nameof(AuthenticatorFilterLabel));
        }
    }

    public int ApiKeyCount
    {
        get => _apiKeyCount;
        private set
        {
            if (SetProperty(ref _apiKeyCount, value))
                OnPropertyChanged(nameof(ApiKeyFilterLabel));
        }
    }

    public int ProjectSecretCount
    {
        get => _projectSecretCount;
        private set
        {
            if (SetProperty(ref _projectSecretCount, value))
                OnPropertyChanged(nameof(ProjectSecretFilterLabel));
        }
    }

    public int FilteredCount
    {
        get => _filteredCount;
        private set
        {
            if (SetProperty(ref _filteredCount, value))
            {
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

}
