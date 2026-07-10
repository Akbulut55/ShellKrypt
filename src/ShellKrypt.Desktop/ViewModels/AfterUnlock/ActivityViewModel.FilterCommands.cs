using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel
{
    [RelayCommand]
    private void ShowAll() => ActiveCategory = "all";

    [RelayCommand]
    private void ShowVault() => ActiveCategory = "vault";

    [RelayCommand]
    private void ShowItems() => ActiveCategory = "items";

    [RelayCommand]
    private void ShowAudit() => ActiveCategory = "audit";

    [RelayCommand]
    private void ShowSettings() => ActiveCategory = "settings";

    [RelayCommand]
    private void ShowTransfer() => ActiveCategory = "transfer";

    [RelayCommand]
    private void Refresh() => ReloadFromStore();
}
