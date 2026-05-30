using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    public sealed partial class AutoLockDurationOption : ObservableObject
    {
        public AutoLockDurationOption(int minutes, string label)
        {
            Minutes = minutes;
            Label = label;
        }

        public int Minutes { get; }
        public string Label { get; }

        [ObservableProperty] private bool isSelected;
    }

    public sealed partial class SecondsDurationOption : ObservableObject
    {
        public SecondsDurationOption(int seconds, string label)
        {
            Seconds = seconds;
            Label = label;
        }

        public int Seconds { get; }
        public string Label { get; }

        [ObservableProperty] private bool isSelected;
    }

    public sealed class LanguageOption
    {
        public LanguageOption(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }

    public sealed partial class ThemeOption : ObservableObject
    {
        public ThemeOption(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }

        [ObservableProperty] private bool isSelected;

        public override string ToString() => Label;
    }
}
