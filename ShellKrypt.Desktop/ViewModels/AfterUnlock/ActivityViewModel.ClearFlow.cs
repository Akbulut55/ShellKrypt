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
            T(_root, "Activity.Clear.Title"),
            T(_root, "Activity.Clear.Subtitle"),
            T(_root, "Activity.Clear.Detail"),
            T(_root, "Activity.Clear.Confirm"));

        if (!confirmed)
            return;

        _store.Clear(_root.VaultPath, _root.IsUnlocked ? _root.VaultKey : null);
        ReloadFromStore();
        _root.LogActivity("activity", "Activity logs cleared", "The current vault activity feed was cleared.", "warning", affectedItem: CurrentVaultDisplayName);
    }
}
