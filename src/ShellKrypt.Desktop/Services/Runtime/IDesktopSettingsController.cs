using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Services.Runtime;

public interface IDesktopSettingsController
{
    event EventHandler? Changed;
    LocalizationService Localization { get; }
    bool AutoLockEnabled { get; set; }
    int AutoLockMinutes { get; set; }
    bool LockOnDeactivate { get; set; }
    int LockOnDeactivateSeconds { get; set; }
    int ClipboardClearSeconds { get; set; }
    bool ClipboardCopyEnabled { get; set; }
    bool CloseToTrayEnabled { get; set; }
    int MarkdownAutoSaveSeconds { get; set; }
    string ThemeId { get; set; }
    string LanguageId { get; set; }
    bool HasAcceptedSecurityAcknowledgement { get; }
    BackupCenterHistory BackupCenterHistory { get; }
    EmergencyKitState EmergencyKit { get; }
    BackupScheduleSettings BackupSchedule { get; }
    AutomaticBackupState AutomaticBackupState { get; }
    QuickFillSettings QuickFill { get; }
    void AcceptSecurityAcknowledgement();
    void SaveBackupCenterHistory();
    void SaveEmergencyKitState();
    void SaveBackupScheduleState();
    void SaveQuickFillSettings();
}
