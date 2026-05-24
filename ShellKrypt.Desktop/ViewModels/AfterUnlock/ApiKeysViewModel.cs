using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ApiKeyFieldRowVm : ObservableObject
{
    public string Id { get; }
    public int SortOrder { get; set; }

    [ObservableProperty] private string label;
    [ObservableProperty] private string fieldType;
    [ObservableProperty] private string value;
    [ObservableProperty] private bool isSensitive;
    [ObservableProperty] private bool isCopyable;
    [ObservableProperty] private bool isValueVisible;

    public ApiKeyFieldRowVm(
        string id,
        string label,
        string fieldType,
        string value,
        bool isSensitive,
        bool isCopyable,
        int sortOrder)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Label = label ?? "";
        FieldType = string.IsNullOrWhiteSpace(fieldType) ? ApiKeysViewModel.DefaultFieldType : fieldType;
        Value = value ?? "";
        IsSensitive = isSensitive;
        IsCopyable = isCopyable;
        SortOrder = sortOrder;
    }

    public string DisplayValue => IsSensitive && !IsValueVisible ? MaskValue(Value) : Value;
    public string VisibilityLabel => IsValueVisible ? "Hide" : "Show";
    public string CopyLabel => IsCopyable ? "Copy" : "Locked";
    public bool UseMaskedValueInput => IsSensitive && !IsValueVisible;
    public bool UsePlainValueInput => !UseMaskedValueInput;
    public bool CanReveal => IsSensitive;

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    partial void OnIsSensitiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(UseMaskedValueInput));
        OnPropertyChanged(nameof(UsePlainValueInput));
        OnPropertyChanged(nameof(CanReveal));
    }

    partial void OnIsCopyableChanged(bool value) => OnPropertyChanged(nameof(CopyLabel));
    partial void OnIsValueVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(VisibilityLabel));
        OnPropertyChanged(nameof(UseMaskedValueInput));
        OnPropertyChanged(nameof(UsePlainValueInput));
    }

    public ApiKeyFieldRowVm Clone()
        => new(Id, Label, FieldType, Value, IsSensitive, IsCopyable, SortOrder)
        {
            IsValueVisible = IsValueVisible
        };

    private static string MaskValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var visibleTail = value.Length <= 4 ? "" : value[^4..];
        return string.IsNullOrWhiteSpace(visibleTail)
            ? "****"
            : $"**** **** {visibleTail}";
    }
}

public sealed partial class ApiKeyRowVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string provider;
    [ObservableProperty] private string environment;
    [ObservableProperty] private string notes;

    public ObservableCollection<ApiKeyFieldRowVm> Fields { get; } = new();

    public ApiKeyRowVm(ApiKeyEntry entry)
    {
        Id = entry.Id;
        CreatedAtUtc = entry.CreatedAtUtc;
        UpdatedAtUtc = entry.UpdatedAtUtc;
        Name = entry.Name;
        Provider = entry.Provider;
        Environment = entry.Environment;
        Notes = entry.Notes;

        foreach (var field in entry.Fields.OrderBy(field => field.SortOrder))
        {
            Fields.Add(new ApiKeyFieldRowVm(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder));
        }
    }

    public string Monogram
    {
        get
        {
            var letters = Name.Where(char.IsLetterOrDigit).Take(2).ToArray();
            return letters.Length == 0 ? "AK" : new string(letters).ToUpperInvariant();
        }
    }

    public string ProviderDisplay => string.IsNullOrWhiteSpace(Provider) ? "Unknown provider" : Provider.Trim();
    public string EnvironmentDisplay => string.IsNullOrWhiteSpace(Environment) ? "Production" : Environment.Trim();
    public string FieldCountDisplay => Fields.Count == 1 ? "1 field" : $"{Fields.Count} fields";
    public ApiKeyFieldRowVm? PrimaryField => Fields.FirstOrDefault(apiField => apiField.IsSensitive && apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault(apiField => apiField.IsCopyable)
                                             ?? Fields.FirstOrDefault();
    public string PrimaryFieldLabel => PrimaryField?.Label ?? "No field";
    public string PrimaryFieldDisplay => PrimaryField?.DisplayValue ?? "No key stored";
    public string PrimaryCopyValue => PrimaryField?.Value ?? "";
    public string UpdatedDisplay => FormatRelativeDate(UpdatedAtUtc);
    public string SearchText => string.Join(" ", new[]
    {
        Name,
        Provider,
        Environment,
        Notes,
        string.Join(" ", Fields.Select(apiField => $"{apiField.Label} {apiField.FieldType} {apiField.Value}"))
    }.Where(part => !string.IsNullOrWhiteSpace(part)));

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Monogram));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ProviderDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnEnvironmentChanged(string value)
    {
        OnPropertyChanged(nameof(EnvironmentDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(SearchText));

    public void Apply(ApiKeyEntry entry)
    {
        Name = entry.Name;
        Provider = entry.Provider;
        Environment = entry.Environment;
        Notes = entry.Notes;
        UpdatedAtUtc = entry.UpdatedAtUtc;

        Fields.Clear();
        foreach (var field in entry.Fields.OrderBy(field => field.SortOrder))
        {
            Fields.Add(new ApiKeyFieldRowVm(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder));
        }

        NotifyFieldsChanged();
        OnPropertyChanged(nameof(UpdatedDisplay));
    }

    public void NotifyFieldsChanged()
    {
        OnPropertyChanged(nameof(FieldCountDisplay));
        OnPropertyChanged(nameof(PrimaryField));
        OnPropertyChanged(nameof(PrimaryFieldLabel));
        OnPropertyChanged(nameof(PrimaryFieldDisplay));
        OnPropertyChanged(nameof(PrimaryCopyValue));
        OnPropertyChanged(nameof(SearchText));
    }

    private static string FormatRelativeDate(string? value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.Zero)
            return local.ToString("MMM d", CultureInfo.InvariantCulture);
        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return local.ToString("MMM d", CultureInfo.InvariantCulture);
    }
}

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
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
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

    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        _selectedDetailsRow = null;
        IsApiKeyDetailsEditing = false;
        IsApiKeyDeleteConfirming = false;
        IsAddApiKeyMode = true;
        ClearForm();
        IsApiKeyModalOpen = true;
    }

    [RelayCommand]
    private void ShowDetails(ApiKeyRowVm? row)
    {
        if (row is null)
            return;

        Error = "";
        _selectedDetailsRow = row;
        IsAddApiKeyMode = false;
        IsApiKeyDetailsEditing = false;
        IsApiKeyDeleteConfirming = false;
        PopulateFormFromRow(row);
        IsApiKeyModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsApiKeyDeleteConfirming = false;
        IsApiKeyDetailsEditing = true;
    }

    [RelayCommand]
    private void CancelDetailsEdit()
    {
        Error = "";

        if (_selectedDetailsRow is not null)
            PopulateFormFromRow(_selectedDetailsRow);

        IsApiKeyDetailsEditing = false;
        IsApiKeyDeleteConfirming = false;
    }

    [RelayCommand]
    private void BeginDetailsDelete()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsApiKeyDetailsEditing = false;
        IsApiKeyDeleteConfirming = true;
    }

    [RelayCommand]
    private void CancelDetailsDelete()
    {
        Error = "";
        IsApiKeyDeleteConfirming = false;
    }

    [RelayCommand]
    private void AddField()
    {
        FormFields.Add(new ApiKeyFieldRowVm(
            Guid.NewGuid().ToString("N"),
            "",
            DefaultFieldType,
            "",
            isSensitive: true,
            isCopyable: true,
            sortOrder: FormFields.Count));
        NotifyFormFieldsChanged();
    }

    [RelayCommand]
    private void RemoveField(ApiKeyFieldRowVm? field)
    {
        if (field is null)
            return;

        FormFields.Remove(field);
        ResequenceFormFields();
        NotifyFormFieldsChanged();
    }

    [RelayCommand]
    private void ToggleFieldVisibility(ApiKeyFieldRowVm? field)
    {
        if (field is not null)
            field.IsValueVisible = !field.IsValueVisible;
    }

    [RelayCommand]
    private async Task CopyFieldAsync(ApiKeyFieldRowVm? field)
    {
        Error = "";

        if (field is null || !field.IsCopyable || string.IsNullOrWhiteSpace(field.Value))
        {
            Error = "No copyable value is available.";
            return;
        }

        await _root.CopyToClipboardAsync(field.Value);
        _root.LogActivity("api_keys", "API key field copied", $"Copied {field.Label}.", "info", affectedItem: field.Label);
    }

    [RelayCommand]
    private async Task CopyPrimarySecretAsync(ApiKeyRowVm? row)
    {
        Error = "";

        if (row is null || string.IsNullOrWhiteSpace(row.PrimaryCopyValue))
        {
            Error = "No API key value is available to copy.";
            return;
        }

        await _root.CopyToClipboardAsync(row.PrimaryCopyValue);
        _root.LogActivity("api_keys", "API key copied", $"Copied {row.PrimaryFieldLabel} for {row.Name}.", "info", affectedItem: row.Name);
    }

    [RelayCommand]
    private void CancelApiKeyModal()
    {
        Error = "";
        ClearForm();
        _selectedDetailsRow = null;
        IsApiKeyDetailsEditing = false;
        IsApiKeyDeleteConfirming = false;
        IsAddApiKeyMode = true;
        IsApiKeyModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveAddApiKeyAsync()
    {
        Error = "";

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        try
        {
            var entry = await _apiKeyService.AddAsync(_root.VaultPath, _root.VaultKey, BuildInput());
            var row = new ApiKeyRowVm(entry);

            _all.Insert(0, row);
            await _refreshAllItemsAsync(entry.Id);
            RefreshProviderFilters();

            ClearForm();
            IsApiKeyModalOpen = false;
            SearchText = "";
            SelectedEnvironmentFilter = AllEnvironmentFilter;
            SelectedProviderFilter = AllProviderFilter;
            SelectedSortOption = SortNewest;
            ApplyFilter();
            _root.LogActivity("api_keys", "API key added", $"Added {entry.Name}.", "success", affectedItem: entry.Name);
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

        if (_selectedDetailsRow is null)
        {
            Error = "No API key selected.";
            return;
        }

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        try
        {
            var entry = await _apiKeyService.UpdateAsync(
                _root.VaultPath,
                _root.VaultKey,
                _selectedDetailsRow.Id,
                _selectedDetailsRow.CreatedAtUtc,
                BuildInput());

            _selectedDetailsRow.Apply(entry);
            await _refreshAllItemsAsync(entry.Id);
            RefreshProviderFilters();
            IsApiKeyDetailsEditing = false;
            IsApiKeyDeleteConfirming = false;
            PopulateFormFromRow(_selectedDetailsRow);
            ApplyFilter(resetPage: false);
            _root.LogActivity("api_keys", "API key updated", $"Updated {entry.Name}.", "info", affectedItem: entry.Name);
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

        if (_selectedDetailsRow is null)
        {
            Error = "No API key selected.";
            return;
        }

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        try
        {
            var deleted = _selectedDetailsRow;
            await _apiKeyService.DeleteAsync(_root.VaultPath, deleted.Id);

            _all.Remove(deleted);
            _selectedDetailsRow = null;
            await _refreshAllItemsAsync(null);
            RefreshProviderFilters();
            IsApiKeyModalOpen = false;
            IsApiKeyDeleteConfirming = false;
            IsApiKeyDetailsEditing = false;
            IsAddApiKeyMode = true;
            ClearForm();
            ApplyFilter(resetPage: false);
            _root.LogActivity("api_keys", "API key deleted", $"Deleted {deleted.Name}.", "warning", affectedItem: deleted.Name);
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

    public async Task<bool> OpenEntryByIdAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_all.Count == 0)
            await LoadAsync();

        var row = _all.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (row is null)
            return false;

        SearchText = "";
        SelectedEnvironmentFilter = AllEnvironmentFilter;
        SelectedProviderFilter = AllProviderFilter;
        SelectedSortOption = SortNewest;
        ApplyFilter();

        var index = _filtered.FindIndex(item => string.Equals(item.Id, row.Id, StringComparison.Ordinal));
        CurrentPage = index < 0 ? 1 : (index / PageSize) + 1;
        RenderPage();
        ShowDetails(row);
        return true;
    }

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        try
        {
            _all.Clear();
            Rows.Clear();

            var entries = await _apiKeyService.ListAsync(_root.VaultPath, _root.VaultKey);
            _all.AddRange(entries.Select(entry => new ApiKeyRowVm(entry)));

            RefreshProviderFilters();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ApplyFilter() => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        IEnumerable<ApiKeyRowVm> filtered = _all;
        var query = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(SelectedEnvironmentFilter) &&
            !string.Equals(SelectedEnvironmentFilter, AllEnvironmentFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row => string.Equals(row.EnvironmentDisplay, SelectedEnvironmentFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedProviderFilter) &&
            !string.Equals(SelectedProviderFilter, AllProviderFilter, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row => string.Equals(row.ProviderDisplay, SelectedProviderFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(row => row.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = SelectedSortOption switch
        {
            SortProvider => filtered
                .OrderBy(row => row.ProviderDisplay, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SortAlphabetical => filtered.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(row => ParseTimestamp(row.UpdatedAtUtc))
        };

        _filtered.Clear();
        _filtered.AddRange(filtered);

        if (resetPage)
            CurrentPage = 1;
        else
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        RenderPage();
        NotifySummaryChanged();
    }

    private void RenderPage()
    {
        Rows.Clear();

        foreach (var row in _filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            Rows.Add(row);

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void ClearForm()
    {
        AddName = "";
        AddProvider = "";
        AddEnvironment = "Production";
        AddNotes = "";
        FormFields.Clear();
        AddDefaultFields();
        NotifyFormFieldsChanged();
    }

    private void AddDefaultFields()
    {
        FormFields.Add(new ApiKeyFieldRowVm(
            Guid.NewGuid().ToString("N"),
            "API Key",
            DefaultFieldType,
            "",
            isSensitive: true,
            isCopyable: true,
            sortOrder: 0));
    }

    private void PopulateFormFromRow(ApiKeyRowVm row)
    {
        AddName = row.Name;
        AddProvider = row.Provider;
        AddEnvironment = row.EnvironmentDisplay;
        AddNotes = row.Notes;
        FormFields.Clear();

        foreach (var field in row.Fields.OrderBy(field => field.SortOrder))
        {
            var clone = field.Clone();
            clone.IsValueVisible = false;
            FormFields.Add(clone);
        }

        if (FormFields.Count == 0)
            AddDefaultFields();

        NotifyFormFieldsChanged();
    }

    private ApiKeyInput BuildInput()
    {
        ResequenceFormFields();
        return new ApiKeyInput(
            AddName,
            AddProvider,
            AddEnvironment,
            AddNotes,
            FormFields.Select(field => new ApiKeyFieldInput(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder)).ToArray());
    }

    private void RefreshProviderFilters()
    {
        var previousEnvironment = SelectedEnvironmentFilter;
        var previous = SelectedProviderFilter;
        EnvironmentFilters.Clear();
        EnvironmentFilters.Add(AllEnvironmentFilter);
        ProviderFilters.Clear();
        ProviderFilters.Add(AllProviderFilter);

        foreach (var environment in _all
                     .Select(row => row.EnvironmentDisplay)
                     .Where(environment => !string.IsNullOrWhiteSpace(environment))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(environment => environment, StringComparer.OrdinalIgnoreCase))
        {
            EnvironmentFilters.Add(environment);
        }

        foreach (var provider in _all
                     .Select(row => row.ProviderDisplay)
                     .Where(provider => !string.IsNullOrWhiteSpace(provider))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase))
        {
            ProviderFilters.Add(provider);
        }

        SelectedEnvironmentFilter = EnvironmentFilters.Any(environment => string.Equals(environment, previousEnvironment, StringComparison.OrdinalIgnoreCase))
            ? previousEnvironment
            : AllEnvironmentFilter;
        SelectedProviderFilter = ProviderFilters.Any(provider => string.Equals(provider, previous, StringComparison.OrdinalIgnoreCase))
            ? previous
            : AllProviderFilter;
    }

    private void ResequenceFormFields()
    {
        for (var i = 0; i < FormFields.Count; i++)
            FormFields[i].SortOrder = i;
    }

    private void NotifyFormFieldsChanged()
    {
        OnPropertyChanged(nameof(FormFields));
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

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SensitiveFieldCount));
        OnPropertyChanged(nameof(ProviderCount));
        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
}
