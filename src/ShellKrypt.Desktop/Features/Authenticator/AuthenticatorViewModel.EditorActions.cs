using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Desktop.Features.Authenticator;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private void AddNew()
    {
        Error = string.Empty;
        IsDeleteConfirmOpen = false;
        Editor.OpenAdd();
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedEntry is null)
            return;

        Error = string.Empty;
        IsDeleteConfirmOpen = false;
        Editor.OpenEdit(SelectedEntry);
    }

    [RelayCommand]
    private void ToggleSecretVisibility(AuthenticatorAccountVm? entry)
    {
        if (entry is not null)
            entry.IsSecretVisible = !entry.IsSecretVisible;
    }

    private async Task SaveEditorAsync(AuthenticatorInput input, AuthenticatorAccountVm? existingEntry)
    {
        if (_desktop.Session.VaultPath is null)
            throw new InvalidOperationException(T(_desktop.Localization, "Common.NoVaultSelected"));

        if (existingEntry is not null)
        {
            var updated = await _entryService.UpdateAsync(
                _desktop.Session.VaultPath,
                _desktop.Session.VaultKey,
                existingEntry.Id,
                existingEntry.CreatedAtUtc,
                input);
            existingEntry.Apply(updated);
            RefreshSnapshots();
            await _refreshAllItemsAsync(updated.Id);
            _desktop.Activity.Log("authenticator", "Authenticator updated", $"Updated {updated.Name}.", "info", affectedItem: updated.Name);
            return;
        }

        var added = await _entryService.AddAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey, input);
        _allEntries.Insert(0, new AuthenticatorAccountVm(added, _desktop.Localization));
        RefreshSnapshots();
        ApplyFilter(added.Id);
        await _refreshAllItemsAsync(added.Id);
        _desktop.Activity.Log("authenticator", "Authenticator added", $"Added {added.Name}.", "success", affectedItem: added.Name);
    }
}
