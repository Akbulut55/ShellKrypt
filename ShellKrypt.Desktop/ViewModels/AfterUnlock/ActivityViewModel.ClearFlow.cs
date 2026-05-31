using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel
{
    [RelayCommand]
    private async Task ClearAsync()
    {
        Error = string.Empty;

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Clear Activity Log?",
            "Clear this vault's activity history?",
            "This only deletes activity entries for the current vault. Vault items remain untouched.",
            "Clear Activity");

        if (!confirmed)
            return;

        _store.Clear(_root.VaultPath, _root.IsUnlocked ? _root.VaultKey : null);
        ReloadFromStore();
        _root.LogActivity("activity", "Activity logs cleared", "The current vault activity feed was cleared.", "warning", affectedItem: CurrentVaultDisplayName);
    }
}
