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

    private const string AllProviderFilter = "Provider: All";
    private const string SortNewest = "Sort: Newest";
    private const string SortOldest = "Sort: Oldest";
    private const string SortNameAscending = "Sort: A to Z";
    private const string SortNameDescending = "Sort: Z to A";
    private const string SortProviderAscending = "Sort: Provider A to Z";
    private const string SortProviderDescending = "Sort: Provider Z to A";

    private readonly MainWindowViewModel _root;
    private readonly IApiKeyService _apiKeyService;
    private readonly Func<string?, Task> _refreshAllItemsAsync;
    private readonly List<ApiKeyRowVm> _all = new();
    private readonly List<ApiKeyRowVm> _filtered = new();
    private ApiKeyRowVm? _selectedDetailsRow;

    public ObservableCollection<ApiKeyRowVm> Rows { get; } = new();
    public ObservableCollection<string> ProviderFilters { get; } = new() { AllProviderFilter };
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        SortNewest,
        SortOldest,
        SortNameAscending,
        SortNameDescending,
        SortProviderAscending,
        SortProviderDescending
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
    public ObservableCollection<ApiKeyFieldRowVm> FormFields { get; } = new();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedProviderFilter = AllProviderFilter;
    [ObservableProperty] private string selectedSortOption = SortNewest;
    [ObservableProperty] private bool isApiKeyModalOpen;
    [ObservableProperty] private bool isAddApiKeyMode = true;
    [ObservableProperty] private bool isApiKeyDetailsEditing;
    [ObservableProperty] private bool isApiKeyDeleteConfirming;
    [ObservableProperty] private string addName = "";
    [ObservableProperty] private string addProvider = "";
    [ObservableProperty] private string addUser = "";
    [ObservableProperty] private string addEnvironment = "Production";
    [ObservableProperty] private string addNotes = "";
    [ObservableProperty] private bool isApiKeyValueVisible;
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
    public string ItemsSummary => T(_root, "ApiKeys.ItemsSummary", _filtered.Count);
    public bool HasRows => Rows.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string ApiKeyModalTitle => IsAddApiKeyMode
        ? T(_root, "ApiKeys.Modal.AddTitle")
        : IsApiKeyDeleteConfirming
            ? T(_root, "ApiKeys.Modal.DeleteTitle")
            : IsApiKeyDetailsEditing
                ? T(_root, "ApiKeys.Modal.EditTitle")
                : T(_root, "ApiKeys.Modal.DetailsTitle");
    public string ApiKeyModalSubtitle => IsAddApiKeyMode
        ? T(_root, "ApiKeys.Modal.AddSubtitle")
        : IsApiKeyDeleteConfirming
            ? T(_root, "ApiKeys.Modal.DeleteSubtitle")
            : IsApiKeyDetailsEditing
                ? T(_root, "ApiKeys.Modal.EditSubtitle")
                : T(_root, "ApiKeys.Modal.DetailsSubtitle");
    public bool IsApiKeyDetailsViewMode => !IsAddApiKeyMode && !IsApiKeyDetailsEditing && !IsApiKeyDeleteConfirming;
    public bool IsApiKeyDetailsEditMode => !IsAddApiKeyMode && IsApiKeyDetailsEditing && !IsApiKeyDeleteConfirming;
    public bool IsApiKeyDetailsDeleteConfirmMode => !IsAddApiKeyMode && IsApiKeyDeleteConfirming;
    public bool IsApiKeyFormReadOnly => !IsAddApiKeyMode && !IsApiKeyDetailsEditing;
    public bool IsApiKeyFormEditable => IsAddApiKeyMode || IsApiKeyDetailsEditing;
    public string ModalFooterText => IsApiKeyDetailsDeleteConfirmMode
        ? T(_root, "ApiKeys.Modal.DeleteFooter", string.IsNullOrWhiteSpace(AddName) ? T(_root, "ApiKeys.ThisApiKey") : AddName)
        : T(_root, "ApiKeys.Modal.Footer");
    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "ApiKeys.Empty.NoneTitle")
        : T(_root, "ApiKeys.Empty.NoMatchTitle");
    public string EmptySubtitle => string.IsNullOrWhiteSpace(SearchText)
        ? T(_root, "ApiKeys.Empty.NoneSubtitle")
        : T(_root, "ApiKeys.Empty.NoMatchSubtitle");
    public string ApiKeyValue
    {
        get => PrimaryFormField?.Value ?? "";
        set
        {
            EnsurePrimaryFormField().Value = value ?? "";
            OnPropertyChanged();
        }
    }
    public bool UseMaskedApiKeyInput => !IsApiKeyValueVisible;
    public bool UsePlainApiKeyInput => IsApiKeyValueVisible;
    public string ApiKeyValueVisibilityLabel => IsApiKeyValueVisible ? T(_root, "ApiKeys.Field.Hide") : T(_root, "ApiKeys.Field.Show");
    private ApiKeyFieldRowVm? PrimaryFormField => FormFields.FirstOrDefault();

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedProviderFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsAddApiKeyModeChanged(bool value) => NotifyModalStateChanged();
    partial void OnIsApiKeyDetailsEditingChanged(bool value) => NotifyModalStateChanged();
    partial void OnIsApiKeyDeleteConfirmingChanged(bool value) => NotifyModalStateChanged();
    partial void OnAddNameChanged(string value) => OnPropertyChanged(nameof(ModalFooterText));
    partial void OnIsApiKeyValueVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(UseMaskedApiKeyInput));
        OnPropertyChanged(nameof(UsePlainApiKeyInput));
        OnPropertyChanged(nameof(ApiKeyValueVisibilityLabel));
    }

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

    public override void RefreshLocalization()
    {
        foreach (var row in _all)
            row.RefreshLocalization();

        foreach (var field in FormFields)
            field.RefreshLocalization();

        NotifyLocalized(
            nameof(ItemsSummary),
            nameof(ApiKeyModalTitle),
            nameof(ApiKeyModalSubtitle),
            nameof(ModalFooterText),
            nameof(ApiKeyValueVisibilityLabel),
            nameof(EmptyTitle),
            nameof(EmptySubtitle));
    }

    private ApiKeyFieldRowVm EnsurePrimaryFormField()
    {
        if (FormFields.FirstOrDefault() is { } existing)
            return existing;

        AddDefaultFields();
        NotifyFormFieldsChanged();
        return FormFields[0];
    }
}
