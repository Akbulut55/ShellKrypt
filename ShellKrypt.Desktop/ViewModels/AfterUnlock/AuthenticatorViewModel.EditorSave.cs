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
            Error = T(_root, "Common.NoVaultSelected");
            return;
        }

        if (string.IsNullOrWhiteSpace(FormName))
        {
            Error = T(_root, "Authenticator.Validation.NameRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(FormSecret))
        {
            Error = T(_root, "Authenticator.Validation.SecretRequired");
            return;
        }

        if (SelectedFormKeyType is null)
        {
            Error = T(_root, "Authenticator.Validation.KeyTypeRequired");
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
                    Error = T(_root, "Authenticator.Validation.NoSelection");
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
                var vm = new AuthenticatorAccountVm(added, _root.Localization);
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
