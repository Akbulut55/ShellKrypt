using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private void BeginDelete()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsDetailsModalOpen = false;
        IsEditorModalOpen = false;
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsDelete()
    {
        BeginDelete();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        Error = string.Empty;
        IsDeleteConfirmOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (SelectedEntry is null)
        {
            Error = "No authenticator code selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = SelectedEntry;
            await _authenticatorService.DeleteAsync(_root.VaultPath, deleted.Id);
            _allEntries.Remove(deleted);
            ApplyFilter();
            await _refreshAllItemsAsync(null);
            _root.LogActivity("authenticator", "Authenticator deleted", $"Deleted {deleted.Name}.", "warning", affectedItem: deleted.Name);
            IsDeleteConfirmOpen = false;
            IsDetailsModalOpen = false;
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
}
