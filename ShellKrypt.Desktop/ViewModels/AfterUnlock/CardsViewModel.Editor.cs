using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CardsViewModel
{
    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        _selectedDetailsRow = null;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        IsAddCardMode = true;
        ClearAddCardForm();
        IsAddCardModalOpen = true;
    }

    [RelayCommand]
    private void ShowDetails(CardRowVm row)
    {
        Error = "";
        _selectedDetailsRow = row;
        IsAddCardMode = false;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        PopulateModalFromRow(row);
        IsAddCardModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsCardDeleteConfirming = false;
        IsCardDetailsEditing = true;
    }

    [RelayCommand]
    private void CancelDetailsEdit()
    {
        Error = "";

        if (_selectedDetailsRow is not null)
            PopulateModalFromRow(_selectedDetailsRow);

        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
    }

    [RelayCommand]
    private void CancelAddCard()
    {
        Error = "";
        ClearAddCardForm();
        _selectedDetailsRow = null;
        IsCardDetailsEditing = false;
        IsCardDeleteConfirming = false;
        IsAddCardMode = true;
        IsAddCardModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveAddCardAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var digits = CardRowVm.DigitsOnly(AddNumber, CardRowVm.StandardCardNumberMaxDigits);
        if (digits.Length < 12) { Error = "Card number looks too short."; return; }

        if (!int.TryParse(AddExpiryMonth, out var mm) || mm < 1 || mm > 12)
        {
            Error = "Expiry month must be 1-12.";
            return;
        }

        if (!int.TryParse(AddExpiryYear, out var yy) || yy < 2000 || yy > 2100)
        {
            Error = "Expiry year must be like 2026.";
            return;
        }

        var cvcDigits = CardRowVm.DigitsOnly(AddCvc, CardRowVm.CvcMaxDigits);
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var entry = await _cardService.AddAsync(
                _root.VaultPath,
                _root.VaultKey,
                BuildInput(digits, mm, yy, cvcDigits));

            _all.Insert(0, ToRow(entry));
            await _refreshAllItemsAsync(entry.Id);

            ClearAddCardForm();
            IsAddCardModalOpen = false;
            SearchText = "";
            RefreshCardFilters();
            SelectedBankFilter = AllBankFilter;
            SelectedCardTypeFilter = AllCardTypeFilter;
            SelectedSortOption = SortNewest;
            ApplyFilter();
            _root.LogActivity("cards", "Credit card added", $"Added {entry.Title}.", "success", affectedItem: entry.Title);
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

        if (_selectedDetailsRow is null) { Error = "No card selected."; return; }
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = "Title is required."; return; }

        var digits = CardRowVm.DigitsOnly(AddNumber, CardRowVm.StandardCardNumberMaxDigits);
        if (digits.Length < 12) { Error = "Card number looks too short."; return; }

        if (!int.TryParse(AddExpiryMonth, out var mm) || mm < 1 || mm > 12)
        {
            Error = "Expiry month must be 1-12.";
            return;
        }

        if (!int.TryParse(AddExpiryYear, out var yy) || yy < 2000 || yy > 2100)
        {
            Error = "Expiry year must be like 2026.";
            return;
        }

        var cvcDigits = CardRowVm.DigitsOnly(AddCvc, CardRowVm.CvcMaxDigits);
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var row = _selectedDetailsRow;
            var entry = await _cardService.UpdateAsync(
                _root.VaultPath,
                _root.VaultKey,
                row.Id,
                row.CreatedAtUtc,
                BuildInput(digits, mm, yy, cvcDigits));

            ApplyEntry(row, entry);
            await _refreshAllItemsAsync(entry.Id);

            IsCardDetailsEditing = false;
            IsCardDeleteConfirming = false;
            PopulateModalFromRow(row);
            RefreshCardFilters();
            ApplyFilter(resetPage: false);
            _root.LogActivity("cards", "Credit card updated", $"Updated {entry.Title}.", "info", affectedItem: entry.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

}
