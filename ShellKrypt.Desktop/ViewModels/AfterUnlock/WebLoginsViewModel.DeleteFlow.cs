using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel
{
    [RelayCommand]
    private void BeginDetailsDelete()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = true;
        IsAddPasswordVisible = false;
    }

    [RelayCommand]
    private void CancelDetailsDelete()
    {
        Error = "";
        IsLoginDeleteConfirming = false;
    }

    [RelayCommand]
    private async Task ConfirmDetailsDeleteAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = T(_root, "WebLogins.Error.NoSelection"); return; }
        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }

        try
        {
            var row = _selectedDetailsRow;
            await _webLoginService.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
            await _refreshAllItemsAsync(null);
            _selectedDetailsRow = null;
            IsLoginDeleteConfirming = false;
            IsLoginDetailsEditing = false;
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
            _root.LogActivity("web", "Web login deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(WebLoginRowVm row)
    {
        _all.Remove(row);
        RefreshLoginFilters();
        ApplyFilter(resetPage: false);
    }
}
