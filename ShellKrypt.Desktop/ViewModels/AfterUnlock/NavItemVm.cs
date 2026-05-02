using CommunityToolkit.Mvvm.ComponentModel;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NavItemVm : ObservableObject
{
    [ObservableProperty] private bool isSelected;

    public string Key { get; }
    public string Title { get; }
    public string ShortCode { get; }

    public NavItemVm(string key, string title)
    {
        Key = key;
        Title = title;
        ShortCode = key.ToUpperInvariant() switch
        {
            "ALL" => "AI",
            "VAULT" => "AI",
            "WEB" => "WB",
            "CARDS" => "CC",
            "NOTES" => "SN",
            "AUTH" => "AU",
            "TOOLS" => "TL",
            "HEALTH" => "HL",
            "SETTINGS" => "ST",
            _ => title.Length >= 2 ? title[..2].ToUpperInvariant() : title.ToUpperInvariant()
        };
    }

    public override string ToString() => Title;
}
