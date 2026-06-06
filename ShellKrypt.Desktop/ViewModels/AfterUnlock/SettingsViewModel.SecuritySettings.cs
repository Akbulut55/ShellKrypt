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
        Status = "Settings saved.";
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
        Status = "Settings saved.";
        MarkSelected(AutoLockDurations, value);
        OnPropertyChanged(nameof(SelectedAutoLockDurationLabel));
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnSelectedClipboardClearDurationChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        _root.ClipboardClearSeconds = value.Seconds;
        Status = "Settings saved.";
        MarkSelected(ClipboardClearTimeoutOptions, value);
        OnPropertyChanged(nameof(SelectedClipboardClearDurationLabel));
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    partial void OnClipboardCopyEnabledChanged(bool value)
    {
        _root.ClipboardCopyEnabled = value;
        Status = value ? "Clipboard copy enabled." : "Clipboard copy disabled.";
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    partial void OnSelectedSecurityProfileChanged(VaultSecurityProfile? value) => OnPropertyChanged(nameof(SelectedSecurityProfileDescription));

    [RelayCommand]
    private void SaveChanges()
    {
        Status = "Changes saved locally.";
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        LoadFromRootSettings();
        Status = "Local settings reloaded.";
    }
}
