using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.Authenticator;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private void BeginDelete()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        Editor.Close();
        IsDeleteConfirmOpen = true;
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
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        if (SelectedEntry is null)
        {
            Error = T(_root, "Authenticator.Validation.NoSelection");
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = SelectedEntry;
            await _entryService.DeleteAsync(_root.VaultPath, deleted.Id);
            _allEntries.Remove(deleted);
            ApplyFilter();
            await _refreshAllItemsAsync(null);
            _root.LogActivity("authenticator", "Authenticator deleted", $"Deleted {deleted.Name}.", "warning", affectedItem: deleted.Name);
            IsDeleteConfirmOpen = false;
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
