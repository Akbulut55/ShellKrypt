using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ShellKrypt.Application.Localization;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.UI.Shared.Theming;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    private void LoadFromRootSettings()
    {
        AutoLockEnabled = _root.AutoLockEnabled;
        SelectedAutoLockDuration = ResolveAutoLockDuration(_root.AutoLockMinutes);
        LockOnDeactivate = _root.LockOnDeactivate;
        SelectedFocusLossLockDelay = ResolveFocusLossLockDelay(_root.LockOnDeactivate, _root.LockOnDeactivateSeconds);
        SelectedClipboardClearDuration = ResolveSecondsDuration(ClipboardClearTimeoutOptions, _root.ClipboardClearSeconds);
        SelectedMarkdownAutoSaveDuration = ResolveSecondsDuration(MarkdownAutoSaveDurationOptions, _root.MarkdownAutoSaveSeconds);
        ClipboardCopyEnabled = _root.ClipboardCopyEnabled;
        CloseToTrayEnabled = _root.CloseToTrayEnabled;
        SelectedThemeOption = ResolveThemeOption(_root.ThemeId);
        OnPropertyChanged(nameof(SecurityStatusText));
        OnPropertyChanged(nameof(SelectedAutoLockDurationLabel));
        OnPropertyChanged(nameof(SelectedFocusLossLockDelayLabel));
        OnPropertyChanged(nameof(SelectedClipboardClearDurationLabel));
        OnPropertyChanged(nameof(SelectedMarkdownAutoSaveDurationLabel));
        OnPropertyChanged(nameof(ThemeModeLabel));
        OnPropertyChanged(nameof(FocusLockSummary));
        OnPropertyChanged(nameof(ClipboardClearSummary));
        OnPropertyChanged(nameof(MarkdownAutoSaveSummary));
    }

    private AutoLockDurationOption ResolveAutoLockDuration(int minutes)
    {
        var existing = AutoLockDurations.FirstOrDefault(x => x.Minutes == minutes);
        if (existing is not null)
            return existing;

        var custom = new AutoLockDurationOption(minutes, "Settings.Duration.CustomMinutes", T("Settings.Duration.CustomMinutes", minutes));
        AutoLockDurations.Add(custom);
        return custom;
    }

    private SecondsDurationOption ResolveFocusLossLockDelay(bool enabled, int seconds)
    {
        if (!enabled)
            return FocusLossLockDelayOptions.First(option => option.Seconds == 0);

        return ResolveSecondsDuration(FocusLossLockDelayOptions, seconds);
    }

    private ThemeOption ResolveThemeOption(string? themeId)
    {
        var normalized = ShellKryptThemePalettes.GetById(themeId).Id;
        var option = ThemeOptions.FirstOrDefault(x => string.Equals(x.Id, normalized, StringComparison.OrdinalIgnoreCase))
            ?? ThemeOptions[0];
        MarkSelected(ThemeOptions, option);
        return option;
    }

    private LanguageOption ResolveLanguageOption(string? languageId)
    {
        var normalized = LanguageRegistry.GetById(languageId).Id;
        var option = LanguageOptions.FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
            ?? LanguageOptions[0];
        MarkSelected(LanguageOptions, option);
        return option;
    }

    private SecondsDurationOption ResolveSecondsDuration(ObservableCollection<SecondsDurationOption> options, int seconds)
    {
        var existing = options.FirstOrDefault(x => x.Seconds == seconds);
        if (existing is not null)
            return existing;

        var custom = new SecondsDurationOption(seconds, "Settings.Duration.CustomSeconds", T("Settings.Duration.CustomSeconds", seconds));
        options.Add(custom);
        return custom;
    }

    private async Task LoadCurrentSecurityProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return;

        try
        {
            var kdf = await _vaultService.GetKdfParamsAsync(_root.VaultPath);
            if (kdf is null)
                return;

            var known = VaultSecurityProfiles.Match(kdf);
            if (known is not null)
            {
                SelectedSecurityProfile = SecurityProfiles.FirstOrDefault(profile => profile.Key == known.Key) ?? known;
                ActiveSecurityProfileLabel = known.Label;
                return;
            }

            var custom = SecurityProfiles.FirstOrDefault(profile => profile.Key == "custom");
            var customLabel = $"Custom ({kdf.MemoryKb / 1024} MB Argon2)";
            var customProfile = new VaultSecurityProfile("custom", customLabel, T("Settings.Profile.CustomDescription"), kdf);

            if (custom is null)
                SecurityProfiles.Add(customProfile);
            else
            {
                var index = SecurityProfiles.IndexOf(custom);
                SecurityProfiles[index] = customProfile;
            }

            SelectedSecurityProfile = customProfile;
            ActiveSecurityProfileLabel = customProfile.Label;
        }
        catch
        {
            ActiveSecurityProfileLabel = T("Settings.Profile.Unavailable");
        }
    }
}
