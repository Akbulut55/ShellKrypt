using System;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedOptionLabels();
        RefreshLocalizedProperties();
    }

    private void RefreshLocalizedOptionLabels()
    {
        foreach (var option in AutoLockDurations)
            option.Label = option.LabelKey.Contains("Custom", StringComparison.Ordinal)
                ? T(option.LabelKey, option.Minutes)
                : T(option.LabelKey);

        foreach (var option in FocusLossLockDelayOptions)
            option.Label = option.LabelKey.Contains("Custom", StringComparison.Ordinal)
                ? T(option.LabelKey, option.Seconds)
                : T(option.LabelKey);

        foreach (var option in ClipboardClearTimeoutOptions)
            option.Label = option.LabelKey.Contains("Custom", StringComparison.Ordinal)
                ? T(option.LabelKey, option.Seconds)
                : T(option.LabelKey);
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(SelectedAutoLockDurationLabel));
        OnPropertyChanged(nameof(SelectedFocusLossLockDelayLabel));
        OnPropertyChanged(nameof(SelectedClipboardClearDurationLabel));
        OnPropertyChanged(nameof(SelectedLanguageLabel));
        OnPropertyChanged(nameof(FocusLockSummary));
        OnPropertyChanged(nameof(ClipboardClearSummary));
        OnPropertyChanged(nameof(RecoveryGuidanceText));
        OnPropertyChanged(nameof(BackupRecommendationText));
        OnPropertyChanged(nameof(SecurityStatusText));
        OnPropertyChanged(nameof(VaultStorageDisplay));
    }
}
