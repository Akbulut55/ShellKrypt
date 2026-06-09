using System;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    private bool TryEnsureUnlockedVault(out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = T("Settings.Status.TransferUnlockVault");
            return false;
        }

        vaultPath = _root.VaultPath;
        vaultKey = _root.VaultKey;
        return true;
    }

    private async Task RunTransferAsync(Func<Task> action)
    {
        IsTransferBusy = true;
        try
        {
            TransferStatus = "";
            await action();
        }
        catch (Exception ex)
        {
            TransferStatus = ex.Message;
        }
        finally
        {
            IsTransferBusy = false;
        }
    }
}
