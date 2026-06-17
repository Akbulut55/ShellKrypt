using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NavItemVm : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _titleKey;
    private readonly string _fallbackTitle;

    [ObservableProperty] private bool isSelected;

    public string Key { get; }
    public string Title
    {
        get
        {
            var value = _localization.Get(_titleKey);
            return string.Equals(value, _titleKey, StringComparison.Ordinal) ? _fallbackTitle : value;
        }
    }
    public string ShortCode { get; }
    public string IconResourceKey { get; }
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconResourceKey);

    public NavItemVm(ShellKryptSectionDescriptor section, LocalizationService localization)
        : this(section.Key, section.Title, section.Glyph, localization)
    {
    }

    private NavItemVm(string key, string title, string? shortCode, LocalizationService localization)
    {
        _localization = localization;
        _titleKey = $"Sidebar.{key}.Title";
        _fallbackTitle = title;
        Key = key;
        IconResourceKey = GetIconResourceKey(key);
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

    private static string GetIconResourceKey(string key) => key switch
    {
        ShellKryptSectionKeys.Vault => "IconViewList",
        ShellKryptSectionKeys.WebLogins => "IconWeb",
        ShellKryptSectionKeys.Cards => "IconCreditCard",
        ShellKryptSectionKeys.ApiKeys => "IconKey",
        ShellKryptSectionKeys.Authenticator => "IconChronic",
        ShellKryptSectionKeys.Notes => "IconMarkdown",
        ShellKryptSectionKeys.Generator => "IconPassword",
        ShellKryptSectionKeys.QuickFill => "IconTrendingFlat",
        ShellKryptSectionKeys.Audit => "IconVitalSigns",
        ShellKryptSectionKeys.Backup => "IconBackupTable",
        ShellKryptSectionKeys.Activity => "IconManageHistory",
        ShellKryptSectionKeys.Settings => "IconSettings",
        _ => string.Empty
    };

    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));

    public override string ToString() => Title;
}
