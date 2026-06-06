using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    partial void OnSelectedThemeOptionChanged(ThemeOption? value)
    {
        if (value is null)
            return;

        _root.ThemeId = value.Id;
        Status = $"Theme switched to {value.Label}.";
        MarkSelected(ThemeOptions, value);
        OnPropertyChanged(nameof(ThemeModeLabel));
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (value is not null)
            Status = $"Language set to {value.Label}.";

        OnPropertyChanged(nameof(SelectedLanguageLabel));
        OnPropertyChanged(nameof(IsEnglishLanguageSelected));
    }

    partial void OnIsThemePickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsThemePickerOpen));
    }

    partial void OnIsLanguagePickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsLanguagePickerOpen));
    }

    partial void OnIsAutoLockPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsAutoLockPickerOpen));
    }

    partial void OnIsFocusLockPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsFocusLockPickerOpen));
    }

    partial void OnIsClipboardClearPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsClipboardClearPickerOpen));
    }

    [RelayCommand]
    private void ToggleAutoLockPicker() => IsAutoLockPickerOpen = !IsAutoLockPickerOpen;

    [RelayCommand]
    private void SelectAutoLockDuration(AutoLockDurationOption? option)
    {
        if (option is null)
            return;

        SelectedAutoLockDuration = option;
        IsAutoLockPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleFocusLockPicker() => IsFocusLockPickerOpen = !IsFocusLockPickerOpen;

    [RelayCommand]
    private void SelectFocusLossLockDelay(SecondsDurationOption? option)
    {
        if (option is null)
            return;

        SelectedFocusLossLockDelay = option;
        IsFocusLockPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleClipboardClearPicker() => IsClipboardClearPickerOpen = !IsClipboardClearPickerOpen;

    [RelayCommand]
    private void SelectClipboardClearDuration(SecondsDurationOption? option)
    {
        if (option is null)
            return;

        SelectedClipboardClearDuration = option;
        IsClipboardClearPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleThemePicker() => IsThemePickerOpen = !IsThemePickerOpen;

    [RelayCommand]
    private void SelectTheme(ThemeOption? option)
    {
        if (option is null)
            return;

        SelectedThemeOption = option;
        IsThemePickerOpen = false;
    }

    [RelayCommand]
    private void ToggleLanguagePicker() => IsLanguagePickerOpen = !IsLanguagePickerOpen;

    [RelayCommand]
    private void SelectEnglishLanguage()
    {
        SelectedLanguageOption = LanguageOptions.FirstOrDefault(option => option.Code == "en") ?? LanguageOptions[0];
        IsLanguagePickerOpen = false;
    }

    private void ClosePickersExcept(string openPickerName)
    {
        if (openPickerName != nameof(IsAutoLockPickerOpen))
            IsAutoLockPickerOpen = false;
        if (openPickerName != nameof(IsFocusLockPickerOpen))
            IsFocusLockPickerOpen = false;
        if (openPickerName != nameof(IsClipboardClearPickerOpen))
            IsClipboardClearPickerOpen = false;
        if (openPickerName != nameof(IsThemePickerOpen))
            IsThemePickerOpen = false;
        if (openPickerName != nameof(IsLanguagePickerOpen))
            IsLanguagePickerOpen = false;
    }

    public void ClosePickers()
    {
        IsAutoLockPickerOpen = false;
        IsFocusLockPickerOpen = false;
        IsClipboardClearPickerOpen = false;
        IsThemePickerOpen = false;
        IsLanguagePickerOpen = false;
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
}
