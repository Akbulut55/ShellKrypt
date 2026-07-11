using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    partial void OnAutoLockEnabledChanged(bool value)
    {
        _root.AutoLockEnabled = value;
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnLockOnDeactivateChanged(bool value)
    {
        _root.LockOnDeactivate = value;
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnSelectedFocusLossLockDelayChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        LockOnDeactivate = value.Seconds > 0;
        _root.LockOnDeactivate = LockOnDeactivate;
        if (value.Seconds > 0)
            _root.LockOnDeactivateSeconds = value.Seconds;
        Status = T("Settings.Status.SettingsSaved");
        MarkSelected(FocusLossLockDelayOptions, value);
        OnPropertyChanged(nameof(SelectedFocusLossLockDelayLabel));
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnSelectedAutoLockDurationChanged(AutoLockDurationOption? value)
    {
        if (value is null)
            return;

        _root.AutoLockMinutes = value.Minutes;
        _root.AutoLockEnabled = value.Minutes > 0;
        Status = T("Settings.Status.SettingsSaved");
        MarkSelected(AutoLockDurations, value);
        OnPropertyChanged(nameof(SelectedAutoLockDurationLabel));
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnSelectedClipboardClearDurationChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        _root.ClipboardClearSeconds = value.Seconds;
        Status = T("Settings.Status.SettingsSaved");
        MarkSelected(ClipboardClearTimeoutOptions, value);
        OnPropertyChanged(nameof(SelectedClipboardClearDurationLabel));
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    partial void OnSelectedMarkdownAutoSaveDurationChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        _root.MarkdownAutoSaveSeconds = value.Seconds;
        Status = T("Settings.Status.SettingsSaved");
        MarkSelected(MarkdownAutoSaveDurationOptions, value);
        OnPropertyChanged(nameof(SelectedMarkdownAutoSaveDurationLabel));
        OnPropertyChanged(nameof(MarkdownAutoSaveSummary));
    }

    partial void OnClipboardCopyEnabledChanged(bool value)
    {
        _root.ClipboardCopyEnabled = value;
        Status = value ? T("Settings.Status.ClipboardCopyEnabled") : T("Settings.Status.ClipboardCopyDisabled");
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    partial void OnCloseToTrayEnabledChanged(bool value)
    {
        _root.CloseToTrayEnabled = value;
        Status = value ? T("Settings.Status.CloseToTrayEnabled") : T("Settings.Status.CloseToTrayDisabled");
    }

    partial void OnSelectedSecurityProfileChanged(VaultSecurityProfile? value)
    {
        OnPropertyChanged(nameof(SelectedSecurityProfileLabel));
        OnPropertyChanged(nameof(SelectedSecurityProfileDescription));
    }

    [RelayCommand]
    private void SaveChanges()
    {
        Status = T("Settings.Status.ChangesSavedLocally");
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        LoadFromRootSettings();
        Status = T("Settings.Status.LocalSettingsReloaded");
    }
}
