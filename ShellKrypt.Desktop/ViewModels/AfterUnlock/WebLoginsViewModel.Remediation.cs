using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel
{
    public async Task<bool> OpenForRemediationAsync(string itemId, bool generateReplacementPassword)
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(itemId) || _root.VaultPath is null)
            return false;

        if (_all.Count == 0)
            await LoadAsync();

        var row = _all.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
        if (row is null)
        {
            await LoadAsync();
            row = _all.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
            if (row is null)
                return false;
        }

        SearchText = "";
        SelectedUsernameFilter = "";
        SelectedUsernameFilterChoice = AllUsernameFilter;
        SelectedEmailFilter = "";
        SelectedEmailFilterChoice = AllEmailFilter;

        var index = _all.FindIndex(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
        CurrentPage = index < 0 ? 1 : (index / PageSize) + 1;
        RenderPage();

        _selectedDetailsRow = row;
        IsAddWebLoginMode = false;
        IsLoginDeleteConfirming = false;
        IsLoginDetailsEditing = true;
        PopulateModalFromRow(row);

        if (generateReplacementPassword)
        {
            AddPassword = GenerateStrongPassword();
            IsAddPasswordVisible = true;
        }
        else
        {
            IsAddPasswordVisible = false;
        }

        IsAddWebLoginModalOpen = true;
        return true;
    }
}
