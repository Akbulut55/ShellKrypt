using System;
using ShellKrypt.Application.Settings;

using ShellKrypt.Application.Backups;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AcceptSecurityAcknowledgement()
    {
        if (HasAcceptedSecurityAcknowledgement)
            return;

        _securityAcknowledgementAcceptedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        _securityAcknowledgementVersionAccepted = AppSettings.CurrentSecurityAcknowledgementVersion;
        SaveSettingsAndSyncSessionSecurity();
    }

    public void SaveBackupCenterHistory()
    {
        _backupCenterHistory.Normalize();
        SaveSettingsAndSyncSessionSecurity();
    }

    public void SaveEmergencyKitState()
    {
        _emergencyKit.Normalize();
        SaveSettingsAndSyncSessionSecurity();
    }

    public void SaveBackupScheduleState()
    {
        _backupSchedule.Normalize();
        _automaticBackupState.Normalize();
        SaveSettingsAndSyncSessionSecurity();
        AutomaticBackupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAutoLockEnabledChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnAutoLockMinutesChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnLockOnDeactivateChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnLockOnDeactivateSecondsChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnClipboardClearSecondsChanged(int value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnClipboardCopyEnabledChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnCloseToTrayEnabledChanged(bool value) => SaveSettingsAndSyncSessionSecurity();
    partial void OnMarkdownAutoSaveSecondsChanged(int value) => SaveSettingsAndSyncSessionSecurity();

    partial void OnThemeIdChanged(string value)
    {
        var normalized = AppSettings.NormalizeThemeId(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            ThemeId = normalized;
            return;
        }

        ApplyTheme(normalized);
        SaveSettingsAndSyncSessionSecurity();
    }

    partial void OnLanguageIdChanged(string value)
    {
        var normalized = AppSettings.NormalizeLanguageId(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            LanguageId = normalized;
            return;
        }

        _localization.SetLanguage(normalized);
        ApplyLocalization();
        SaveSettingsAndSyncSessionSecurity();
    }

    private void SaveSettingsAndSyncSessionSecurity()
    {
        try
        {
            var appSettings = new AppSettings
            {
                ThemeId = ThemeId,
                LanguageId = LanguageId,
                BackupCenterHistory = _backupCenterHistory,
                EmergencyKit = _emergencyKit,
                BackupSchedule = _backupSchedule,
                AutomaticBackupState = _automaticBackupState,
                QuickFill = _quickFill,
                CloseToTrayEnabled = CloseToTrayEnabled,
                MarkdownAutoSaveSeconds = Math.Max(1, MarkdownAutoSaveSeconds),
                SecurityAcknowledgementAcceptedAtUtc = _securityAcknowledgementAcceptedAtUtc,
                SecurityAcknowledgementVersionAccepted = _securityAcknowledgementVersionAccepted
            };
            appSettings.ApplySessionSecuritySettings(BuildSessionSecuritySettings());
            _settingsService.Save(appSettings);
        }
        catch
        {
        }

        _sessionSecurity.ApplySettings(BuildSessionSecuritySettings());
    }

    private static void ApplyTheme(string themeId)
    {
        if (Avalonia.Application.Current is ShellKrypt.Desktop.App app)
            app.ApplyTheme(themeId);
    }

    private void ApplyLocalization()
    {
        if (Avalonia.Application.Current is ShellKrypt.Desktop.App app)
            app.ApplyLocalization(_localization);
    }

    private SessionSecuritySettings BuildSessionSecuritySettings()
    {
        return new SessionSecuritySettings
        {
            AutoLockEnabled = AutoLockEnabled,
            AutoLockMinutes = AutoLockMinutes,
            LockOnDeactivate = LockOnDeactivate,
            LockOnDeactivateSeconds = LockOnDeactivateSeconds,
            ClipboardClearSeconds = ClipboardClearSeconds,
            ClipboardCopyEnabled = ClipboardCopyEnabled
        }.Normalize();
    }
}
