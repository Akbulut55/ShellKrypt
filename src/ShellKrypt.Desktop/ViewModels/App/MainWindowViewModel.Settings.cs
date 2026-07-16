namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AcceptSecurityAcknowledgement() => _settings.AcceptSecurityAcknowledgement();

    public void SaveBackupCenterHistory() => _automaticBackups.SaveHistory();

    public void SaveEmergencyKitState() => _settings.SaveEmergencyKitState();

    public void SaveBackupScheduleState() => _automaticBackups.SaveSchedule();
}
