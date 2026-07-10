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
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        OpenVault(SelectedVault);
    }

    [RelayCommand]
    private void OpenVault(VaultRecordVm? vault)
    {
        if (vault is null)
            return;

        Error = "";

        if (!vault.Exists)
        {
            Status = T(_root, "Welcome.Status.MissingVaultFile", vault.VaultPath);
            Error = T(_root, "Welcome.Error.SelectedVaultMissing");
            ReloadVaults(vault.VaultPath);
            return;
        }

        if (RequestSecurityAcknowledgement(SecurityAcknowledgementAction.OpenVault, vault))
            return;

        _root.SetVaultPath(vault.VaultPath);
        _root.GoUnlock();
    }
}
