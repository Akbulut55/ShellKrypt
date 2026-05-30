using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private void CreateVault()
    {
        Error = "";

        if (RequestSecurityAcknowledgement(SecurityAcknowledgementAction.CreateVault))
            return;

        _root.GoCreateVault();
    }

    [RelayCommand]
    private void Refresh() => ReloadVaults(SelectedVault?.VaultPath);

    [RelayCommand]
    private void SortByRecent() => ActiveSort = "recent";

    [RelayCommand]
    private void SortByName() => ActiveSort = "name";

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPreviousPage)
            CurrentPage--;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNextPage)
            CurrentPage++;
    }

    [RelayCommand]
    private void OpenSelectedVault()
    {
        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        OpenVault(SelectedVault);
    }

    [RelayCommand]
    private void OpenDefaultVault()
    {
        var defaultVault = _vaultRegistry.GetDefaultVault();
        if (defaultVault is null)
        {
            Error = "No default vault has been set yet.";
            return;
        }

        OpenVault(new VaultRecordVm(defaultVault));
    }

    [RelayCommand]
    private void OpenVault(VaultRecordVm? vault)
    {
        if (vault is null)
            return;

        Error = "";

        if (!vault.Exists)
        {
            Status = $"Vault file is missing:\n{vault.VaultPath}";
            Error = "The selected vault file could not be found.";
            ReloadVaults(vault.VaultPath);
            return;
        }

        if (RequestSecurityAcknowledgement(SecurityAcknowledgementAction.OpenVault, vault))
            return;

        _root.SetVaultPath(vault.VaultPath);
        _root.GoUnlock();
    }
}
