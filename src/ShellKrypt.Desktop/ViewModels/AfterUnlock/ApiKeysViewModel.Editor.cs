using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
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
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        try
        {
            var entry = await _apiKeyService.AddAsync(_root.VaultPath, _root.VaultKey, BuildInput());
            var row = new ApiKeyRowVm(entry, _root.Localization);

            _all.Insert(0, row);
            await _refreshAllItemsAsync(entry.Id);
            RefreshProviderFilters();

            ClearForm();
            IsApiKeyModalOpen = false;
            SearchText = "";
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
            Error = T(_root, "ApiKeys.Error.NoSelection");
            return;
        }

        if (_root.VaultPath is null)
        {
            Error = T(_root, "Common.NoVaultSelected");
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

    private void ClearForm()
    {
        AddName = "";
        AddProvider = "";
        AddUser = "";
        AddEnvironment = "Production";
        AddNotes = "";
        IsApiKeyValueVisible = false;
        FormFields.Clear();
        AddDefaultFields();
        OnPropertyChanged(nameof(ApiKeyValue));
        NotifyFormFieldsChanged();
    }

    private void PopulateFormFromRow(ApiKeyRowVm row)
    {
        AddName = row.Name;
        AddProvider = row.Provider;
        AddUser = row.User;
        AddEnvironment = "Production";
        AddNotes = row.Notes;
        IsApiKeyValueVisible = false;
        FormFields.Clear();

        if (row.PrimaryField is { } primaryField)
        {
            var clone = primaryField.Clone();
            clone.Label = "API Key";
            clone.FieldType = DefaultFieldType;
            clone.IsSensitive = true;
            clone.IsCopyable = true;
            clone.SortOrder = 0;
            clone.IsValueVisible = false;
            FormFields.Add(clone);
        }

        if (FormFields.Count == 0)
            AddDefaultFields();

        OnPropertyChanged(nameof(ApiKeyValue));
        NotifyFormFieldsChanged();
    }

    private ApiKeyInput BuildInput()
    {
        var field = EnsurePrimaryFormField();
        field.Label = "API Key";
        field.FieldType = DefaultFieldType;
        field.IsSensitive = true;
        field.IsCopyable = true;
        field.SortOrder = 0;

        return new ApiKeyInput(
            AddName,
            AddProvider,
            AddEnvironment,
            AddNotes,
            new[]
            {
                new ApiKeyFieldInput(
                field.Id,
                field.Label,
                field.FieldType,
                field.Value,
                field.IsSensitive,
                field.IsCopyable,
                field.SortOrder)
            },
            AddUser);
    }
}
