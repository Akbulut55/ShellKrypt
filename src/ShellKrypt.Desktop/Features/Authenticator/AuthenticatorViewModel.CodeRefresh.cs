using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.Authenticator;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        Error = string.Empty;

        if (SelectedEntry is null || !SelectedEntry.IsCodeValid)
        {
            Error = T(_desktop.Localization, "Authenticator.Validation.NoValidCode");
            return;
        }

        await _desktop.Clipboard.CopyAsync(SelectedEntry.CurrentCodeRaw);

        if (_desktop.Session.VaultPath is null)
            return;

        var updated = await _entryService.MarkUsedAsync(_desktop.Session.VaultPath, _desktop.Session.VaultKey, SelectedEntry.Id);
        SelectedEntry.Apply(updated);
        RefreshSnapshots();
        await _refreshAllItemsAsync(updated.Id);
        _desktop.Activity.Log("authenticator", "Authenticator code copied", $"Copied code for {updated.Name}.", "info", affectedItem: updated.Name);
    }

    private void RefreshSnapshots()
    {
        foreach (var entry in _allEntries)
            entry.ApplySnapshot(_codeGenerator.GetCurrentCode(entry.ToEntry()));

        OnPropertyChanged(nameof(CanCopyCode));
    }
}
