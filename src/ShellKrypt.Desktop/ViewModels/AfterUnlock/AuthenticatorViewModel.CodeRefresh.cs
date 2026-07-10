using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

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

        var updated = await _authenticatorService.MarkUsedAsync(_root.VaultPath, _root.VaultKey, SelectedEntry.Id);
        SelectedEntry.Apply(updated);
        RefreshSnapshots();
        await _refreshAllItemsAsync(updated.Id);
        _root.LogActivity("authenticator", "Authenticator code copied", $"Copied code for {updated.Name}.", "info", affectedItem: updated.Name);
    }

    private void RefreshSnapshots()
    {
        foreach (var entry in _allEntries)
            entry.ApplySnapshot(_authenticatorService.GetCurrentCode(entry.ToEntry()));

        OnPropertyChanged(nameof(RefreshingSoonCount));
        OnPropertyChanged(nameof(RecentlyUsedCount));
        OnPropertyChanged(nameof(CanCopyCode));
    }
}
