using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    [RelayCommand]
    private void BeginDetailsDelete()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = true;
    }

    [RelayCommand]
    private void CancelDetailsDelete()
    {
        Error = "";
        IsCardDeleteConfirming = false;
    }

    [RelayCommand]
    private async Task ConfirmDetailsDeleteAsync()
    {
        Error = "";

        if (_selectedDetailsRow is null) { Error = "No card selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            var row = _selectedDetailsRow;
            await _cardService.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
            await _refreshAllItemsAsync(null);
            _selectedDetailsRow = null;
            IsCardDeleteConfirming = false;
            IsCardDetailsEditing = false;
            IsAddCardMode = true;
            ClearAddCardForm();
            IsAddCardModalOpen = false;
            _root.LogActivity("cards", "Credit card deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(CardRowVm row)
    {
        _all.Remove(row);
        RefreshCardFilters();
        ApplyFilter(resetPage: false);
    }
}
