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
            Error = T(_root, "Authenticator.Validation.NoValidCode");
            return;
        }

        await _root.CopyToClipboardAsync(SelectedEntry.CurrentCodeRaw);

        if (_root.VaultPath is null)
            return;

        var updated = await _entryService.MarkUsedAsync(_root.VaultPath, _root.VaultKey, SelectedEntry.Id);
        SelectedEntry.Apply(updated);
        RefreshSnapshots();
        await _refreshAllItemsAsync(updated.Id);
        _root.LogActivity("authenticator", "Authenticator code copied", $"Copied code for {updated.Name}.", "info", affectedItem: updated.Name);
    }

    private void RefreshSnapshots()
    {
        foreach (var entry in _allEntries)
            entry.ApplySnapshot(_codeGenerator.GetCurrentCode(entry.ToEntry()));

        OnPropertyChanged(nameof(CanCopyCode));
    }
}
