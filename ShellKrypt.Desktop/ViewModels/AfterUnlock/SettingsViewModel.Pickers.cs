using System.Collections.ObjectModel;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    partial void OnSelectedThemeOptionChanged(ThemeOption? value)
    {
        if (value is null)
            return;

        _root.ThemeId = value.Id;
        Status = T("Settings.Status.ThemeChanged", value.Label);
        MarkSelected(ThemeOptions, value);
        OnPropertyChanged(nameof(ThemeModeLabel));
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (value is not null)
        {
            _root.LanguageId = value.Code;
            Status = T("Settings.Status.LanguageChanged", value.Label);
            MarkSelected(LanguageOptions, value);
        }

        OnPropertyChanged(nameof(SelectedLanguageLabel));
    }

    private static void MarkSelected(ObservableCollection<AutoLockDurationOption> options, AutoLockDurationOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    private static void MarkSelected(ObservableCollection<SecondsDurationOption> options, SecondsDurationOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    private static void MarkSelected(ObservableCollection<ThemeOption> options, ThemeOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    private static void MarkSelected(ObservableCollection<LanguageOption> options, LanguageOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }
}
