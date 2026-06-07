using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        Error = string.Empty;

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormName))
        {
            Error = "Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormSecret))
        {
            Error = "Secret key is required.";
            return;
        }

        if (SelectedFormKeyType is null)
        {
            Error = "Select a key type.";
            return;
        }

        IsBusy = true;
        try
        {
            var input = BuildEditorInput();

            if (IsEditingExisting)
            {
                if (SelectedEntry is null)
                {
                    Error = "No authenticator code selected.";
                    return;
                }

                var updated = await _authenticatorService.UpdateAsync(
                    _root.VaultPath,
                    _root.VaultKey,
                    SelectedEntry.Id,
                    SelectedEntry.CreatedAtUtc,
                    input);

                SelectedEntry.Apply(updated);
                RefreshSnapshots();
                await _refreshAllItemsAsync(updated.Id);
                _root.LogActivity("authenticator", "Authenticator updated", $"Updated {updated.Name}.", "info", affectedItem: updated.Name);
            }
            else
            {
                var added = await _authenticatorService.AddAsync(_root.VaultPath, _root.VaultKey, input);
                var vm = new AuthenticatorAccountVm(added);
                _allEntries.Insert(0, vm);
                RefreshSnapshots();
                ApplyFilter(selectEntryId: added.Id);
                await _refreshAllItemsAsync(added.Id);
                _root.LogActivity("authenticator", "Authenticator added", $"Added {added.Name}.", "success", affectedItem: added.Name);
            }

            IsEditorModalOpen = false;
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

    private AuthenticatorInput BuildEditorInput()
    {
        return new AuthenticatorInput(
            Name: FormName,
            Secret: FormSecret,
            KeyType: SelectedFormKeyType!.KeyType,
            Counter: SelectedFormKeyType.KeyType == AuthenticatorKeyType.CounterBased ? _formCounter : 0,
            Algorithm: SelectedFormAlgorithm?.Value ?? "HMAC-SHA1",
            Digits: SelectedFormDigits?.Digits ?? 6,
            PeriodSeconds: ResolveFormPeriodSeconds());
    }
}
