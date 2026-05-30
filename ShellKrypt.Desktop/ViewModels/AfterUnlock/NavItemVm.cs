using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NavItemVm : ObservableObject
{
    [ObservableProperty] private bool isSelected;

    public string Key { get; }
    public string Title { get; }
    public string ShortCode { get; }

    public NavItemVm(ShellKryptSectionDescriptor section)
        : this(section.Key, section.Title, section.Glyph)
    {
    }

    private NavItemVm(string key, string title, string? shortCode = null)
    {
        Key = key;
        Title = title;
        ShortCode = string.IsNullOrWhiteSpace(shortCode)
            ? key.ToUpperInvariant() switch
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
        }
            : shortCode;
    }

    public override string ToString() => Title;
}
