using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.Settings;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private void ViewAudit()
    {
        _shell.ShowSecurityAudit();
    }

    [RelayCommand]
    private void OpenBackupCenter()
    {
        _shell.ShowBackupCenter();
    }
}
