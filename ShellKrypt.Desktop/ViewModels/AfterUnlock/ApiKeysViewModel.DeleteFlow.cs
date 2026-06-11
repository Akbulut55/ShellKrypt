using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
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
    private async Task ConfirmDetailsDeleteAsync()
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
}
