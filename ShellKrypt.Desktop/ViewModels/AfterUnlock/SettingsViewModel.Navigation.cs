using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private void ViewAudit()
    {
        _shell.ShowSecurityAudit();
    }
}
