using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WebLoginsViewModel
{
    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        _selectedDetailsRow = null;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddWebLoginMode = true;
        ClearAddForm();
        IsAddWebLoginModalOpen = true;
    }

    [RelayCommand]
    private void ShowDetails(WebLoginRowVm row)
    {
        Error = "";
        _selectedDetailsRow = row;
        IsAddWebLoginMode = false;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        PopulateModalFromRow(row);
        IsAddPasswordVisible = false;
        IsAddWebLoginModalOpen = true;
    }

    [RelayCommand]
    private void BeginDetailsEdit()
    {
        if (_selectedDetailsRow is null)
            return;

        Error = "";
        IsLoginDeleteConfirming = false;
        IsLoginDetailsEditing = true;
    }

    [RelayCommand]
    private void CancelDetailsEdit()
    {
        Error = "";

        if (_selectedDetailsRow is not null)
            PopulateModalFromRow(_selectedDetailsRow);

        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddPasswordVisible = false;
    }

    [RelayCommand]
    private void CancelAdd()
    {
        Error = "";
        ClearAddForm();
        _selectedDetailsRow = null;
        IsLoginDetailsEditing = false;
        IsLoginDeleteConfirming = false;
        IsAddWebLoginModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveAddAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = T(_root, "Validation.TitleRequired"); return; }

        try
        {
            var entry = await _webLoginService.AddAsync(_root.VaultPath, _root.VaultKey, BuildInput());

            _all.Insert(0, ToRow(entry));
            await _refreshAllItemsAsync(entry.Id);
            RefreshLoginFilters();
            ClearAddForm();
            IsAddWebLoginModalOpen = false;
            ApplyFilter();
            _root.LogActivity("web", "Web login added", $"Added {entry.Title}.", "success", affectedItem: entry.Title);
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

        if (_selectedDetailsRow is null) { Error = T(_root, "WebLogins.Error.NoSelection"); return; }
        if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; }
        if (string.IsNullOrWhiteSpace(AddTitle)) { Error = T(_root, "Validation.TitleRequired"); return; }

        try
        {
            var row = _selectedDetailsRow;
            var entry = await _webLoginService.UpdateAsync(
                _root.VaultPath,
                _root.VaultKey,
                row.Id,
                row.CreatedAtUtc,
                BuildInput());

            ApplyEntry(row, entry);
            await _refreshAllItemsAsync(entry.Id);

            IsLoginDetailsEditing = false;
            IsLoginDeleteConfirming = false;
            RefreshLoginFilters();
            ApplyFilter(resetPage: false);
            _root.LogActivity("web", "Web login updated", $"Updated {entry.Title}.", "info", affectedItem: entry.Title);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ClearAddForm()
    {
        AddTitle = "";
        AddUrl = "";
        AddUsername = "";
        AddEmail = "";
        AddPassword = "";
        AddNotes = "";
        IsAddPasswordVisible = false;
    }

    private void PopulateModalFromRow(WebLoginRowVm row)
    {
        AddTitle = row.Title;
        AddUrl = row.Url;
        AddUsername = row.Username;
        AddEmail = row.Email;
        AddPassword = row.Password;
        AddNotes = row.Notes;
    }

    private WebLoginInput BuildInput()
        => new(AddTitle, AddUrl, AddUsername, AddEmail, AddPassword, AddNotes);

    private WebLoginRowVm ToRow(WebLoginEntry entry)
        => new(
            _root.Localization,
            entry.Id,
            entry.Title,
            entry.Username,
            entry.Password,
            entry.Url,
            entry.Notes,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            isNew: false,
            email: entry.Email);

    private static void ApplyEntry(WebLoginRowVm row, WebLoginEntry entry)
    {
        row.Title = entry.Title;
        row.Url = entry.Url;
        row.Username = entry.Username;
        row.Email = entry.Email;
        row.Password = entry.Password;
        row.Notes = entry.Notes;
        row.MarkSaved(entry.UpdatedAtUtc);
    }
}
