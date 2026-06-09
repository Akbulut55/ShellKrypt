using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    public sealed partial class AutoLockDurationOption : ObservableObject
    {
        public AutoLockDurationOption(int minutes, string labelKey, string label)
        {
            Minutes = minutes;
            LabelKey = labelKey;
            Label = label;
        }

        public int Minutes { get; }
        public string LabelKey { get; }

        [ObservableProperty] private string label;

        [ObservableProperty] private bool isSelected;
    }

    public sealed partial class SecondsDurationOption : ObservableObject
    {
        public SecondsDurationOption(int seconds, string labelKey, string label)
        {
            Seconds = seconds;
            LabelKey = labelKey;
            Label = label;
        }

        public int Seconds { get; }
        public string LabelKey { get; }

        [ObservableProperty] private string label;

        [ObservableProperty] private bool isSelected;
    }

    public sealed partial class LanguageOption : ObservableObject
    {
        public LanguageOption(string code, string label, string displayName)
        {
            Code = code;
            Label = label;
            DisplayName = displayName;
        }

        public string Code { get; }
        public string Label { get; }
        public string DisplayName { get; }

        [ObservableProperty] private bool isSelected;

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
