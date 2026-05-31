using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel : ViewModelBase
{
    internal const string DefaultFieldType = "API Key";

    private const int PageSize = 5;
    private const string AllEnvironmentFilter = "Env: All";
    private const string AllProviderFilter = "Provider: All";
    private const string SortNewest = "Sort: Newest";
    private const string SortProvider = "Provider";
    private const string SortAlphabetical = "Alphabetical";

    private readonly MainWindowViewModel _root;
    private readonly IApiKeyService _apiKeyService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly List<ApiKeyRowVm> _all = new();
    private readonly List<ApiKeyRowVm> _filtered = new();
    private ApiKeyRowVm? _selectedDetailsRow;

    public ObservableCollection<ApiKeyRowVm> Rows { get; } = new();
    public ObservableCollection<string> EnvironmentFilters { get; } = new() { AllEnvironmentFilter };
    public ObservableCollection<string> ProviderFilters { get; } = new() { AllProviderFilter };
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        SortNewest,
        SortProvider,
        SortAlphabetical
    };
    public ObservableCollection<string> FieldTypeOptions { get; } = new()
    {
        "API Key",
        "Secret Key",
        "Client ID",
        "Client Secret",
        "Project ID",
        "Project Number",
        "Key Name",
        "Prefix",
        "Endpoint",
        "Scope",
        "Custom"
    };
    public ObservableCollection<string> EnvironmentOptions { get; } = new()
    {
        "Production",
        "Staging",
        "Development",
        "Local",
        "Shared",
        "Custom"
    };
    public ObservableCollection<ApiKeyFieldRowVm> FormFields { get; } = new();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedEnvironmentFilter = AllEnvironmentFilter;
    [ObservableProperty] private string selectedProviderFilter = AllProviderFilter;
    [ObservableProperty] private string selectedSortOption = SortNewest;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private bool isApiKeyModalOpen;
    [ObservableProperty] private bool isAddApiKeyMode = true;
    [ObservableProperty] private bool isApiKeyDetailsEditing;
    [ObservableProperty] private bool isApiKeyDeleteConfirming;
    [ObservableProperty] private string addName = "";
    [ObservableProperty] private string addProvider = "";
    [ObservableProperty] private string addEnvironment = "Production";
    [ObservableProperty] private string addNotes = "";
    [ObservableProperty] private string error = "";

    public ApiKeysViewModel(MainWindowViewModel root, IApiKeyService apiKeyService, Func<string?, Task> refreshAllItemsAsync)
    {
        _root = root;
        _apiKeyService = apiKeyService;
        _refreshAllItemsAsync = refreshAllItemsAsync;
        _ = LoadAsync();
    }

    public int TotalCount => _all.Count;
    public int SensitiveFieldCount => _all.Sum(row => row.Fields.Count(apiField => apiField.IsSensitive));
    public int ProviderCount => _all
        .Select(row => row.ProviderDisplay)
        .Where(provider => !string.IsNullOrWhiteSpace(provider))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public string ItemsSummary => $"Showing {Rows.Count} of {_filtered.Count} API keys";
    public int TotalPages => DesktopPagination.GetTotalPages(_filtered.Count, PageSize);
    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string ApiKeyModalTitle => IsAddApiKeyMode
        ? "Add API Key"
        : IsApiKeyDeleteConfirming
            ? "Delete API Key?"
            : IsApiKeyDetailsEditing
                ? "Edit API Key"
                : "API Key Details";
    public string ApiKeyModalSubtitle => IsAddApiKeyMode
        ? "Store provider tokens, client IDs, project identifiers, and custom secret fields."
        : IsApiKeyDeleteConfirming
            ? "Are you sure you want to delete this API key? This action cannot be undone."
            : IsApiKeyDetailsEditing
                ? "Update the saved API key fields in this encrypted vault."
                : "Review the saved API key fields stored in this encrypted vault.";
    public bool IsApiKeyDetailsViewMode => !IsAddApiKeyMode && !IsApiKeyDetailsEditing && !IsApiKeyDeleteConfirming;
    public bool IsApiKeyDetailsEditMode => !IsAddApiKeyMode && IsApiKeyDetailsEditing && !IsApiKeyDeleteConfirming;
    public bool IsApiKeyDetailsDeleteConfirmMode => !IsAddApiKeyMode && IsApiKeyDeleteConfirming;
    public bool IsApiKeyFormReadOnly => !IsAddApiKeyMode && !IsApiKeyDetailsEditing;
    public bool IsApiKeyFormEditable => IsAddApiKeyMode || IsApiKeyDetailsEditing;
    public string ModalFooterText => IsApiKeyDetailsDeleteConfirmMode
        ? $"Are you sure you want to delete \"{(string.IsNullOrWhiteSpace(AddName) ? "this API key" : AddName)}\"?"
        : "All API key fields are encrypted locally before being stored.";
    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No API keys stored yet"
        : "No API keys match this search";
    public string EmptySubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? "Add an API key with only the fields you need for that provider."
        : "Try a different provider, name, environment, or field label.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedEnvironmentFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedProviderFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsAddApiKeyModeChanged(bool value) => NotifyModalStateChanged();
    partial void OnIsApiKeyDetailsEditingChanged(bool value) => NotifyModalStateChanged();
    partial void OnIsApiKeyDeleteConfirmingChanged(bool value) => NotifyModalStateChanged();
    partial void OnAddNameChanged(string value) => OnPropertyChanged(nameof(ModalFooterText));

    private void NotifyModalStateChanged()
    {
        OnPropertyChanged(nameof(ApiKeyModalTitle));
        OnPropertyChanged(nameof(ApiKeyModalSubtitle));
        OnPropertyChanged(nameof(IsApiKeyDetailsViewMode));
        OnPropertyChanged(nameof(IsApiKeyDetailsEditMode));
        OnPropertyChanged(nameof(IsApiKeyDetailsDeleteConfirmMode));
        OnPropertyChanged(nameof(IsApiKeyFormReadOnly));
        OnPropertyChanged(nameof(IsApiKeyFormEditable));
        OnPropertyChanged(nameof(ModalFooterText));
    }
}
