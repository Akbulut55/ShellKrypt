using System;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ActivityItemVm : ObservableObject
{
    private readonly LocalizationService _localization;

    public ActivityItemVm(ActivityLogEntry entry, LocalizationService localization)
    {
        Entry = entry;
        _localization = localization;
    }

    public ActivityLogEntry Entry { get; }
    public string Id => Entry.Id;
    public string Category => Entry.Category;
    public string Title => Entry.Title;
    public string Detail => Entry.Detail;
    public string Severity => Entry.Severity;
    public string VaultDisplay => string.IsNullOrWhiteSpace(Entry.VaultPath) ? "ShellKrypt" : Path.GetFileNameWithoutExtension(Entry.VaultPath);
    public string SessionIdDisplay => $"SES-{Id[..4].ToUpperInvariant()}";
    public string TimestampColumnDisplay => FormatColumnTimestamp(Entry.TimestampUtc);
    public string AffectedItemDisplay => !string.IsNullOrWhiteSpace(Entry.AffectedItem)
        ? Entry.AffectedItem
        : string.IsNullOrWhiteSpace(Entry.VaultPath) ? Detail : VaultDisplay;
    public string CategoryLabel => Entry.Category switch
    {
        "vault" => T("Activity.Category.Vault"),
        "web" => T("Activity.Category.Web"),
        "cards" => T("Activity.Category.Cards"),
        "notes" => T("Activity.Category.Notes"),
        "authenticator" => T("Activity.Category.Authenticator"),
        "api_keys" => T("Activity.Category.ApiKeys"),
        "audit" => T("Activity.Category.Audit"),
        "crypto-tools" => T("Activity.Category.CryptoTools"),
        "settings" => T("Activity.Category.Settings"),
        "transfer" => T("Activity.Category.Export"),
        "activity" => T("Activity.Category.Activity"),
        _ => T("Activity.Category.System")
    };
    public string TimestampDisplay => FormatTimestamp(Entry.TimestampUtc);
    public string SeverityChipText => Entry.Severity switch
    {
        "warning" => T("Activity.Severity.Warning"),
        "success" => T("Activity.Severity.Success"),
        "danger" => T("Activity.Severity.Danger"),
        _ => T("Activity.Severity.Info")
    };
    public string SeverityForeground => Entry.Severity switch
    {
        "warning" => "WarningForegroundBrush",
        "success" => "SuccessForegroundBrush",
        "danger" => "DangerBrush",
        _ => "InfoBrush"
    };
    public string SeverityBackground => Entry.Severity switch
    {
        "warning" => "WarningMutedBrush",
        "success" => "SuccessMutedBrush",
        "danger" => "DangerMutedBrush",
        _ => "InfoMutedBrush"
    };
    public string IconGlyph => Entry.Category switch
    {
        "vault" => "VA",
        "web" => "WB",
        "cards" => "CC",
        "notes" => "MD",
        "authenticator" => "AU",
        "api_keys" => "AK",
        "audit" => "SE",
        "crypto-tools" => "CT",
        "settings" => "ST",
        "transfer" => "IO",
        "activity" => "AC",
        _ => "SY"
    };

    private static string FormatColumnTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "--:--:--";

        return parsed.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(CategoryLabel));
        OnPropertyChanged(nameof(SeverityChipText));
        OnPropertyChanged(nameof(TimestampDisplay));
    }

    private string T(string key, params object[] args) => _localization.Get(key, args);

    private string FormatTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return T("Activity.Time.Unknown");

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.FromMinutes(1))
            return T("Activity.Time.JustNow");
        if (delta < TimeSpan.FromHours(1))
            return T("Activity.Time.MinutesAgo", Math.Max(1, (int)delta.TotalMinutes));
        if (delta < TimeSpan.FromDays(1))
            return T("Activity.Time.HoursAgo", Math.Max(1, (int)delta.TotalHours));
        if (delta < TimeSpan.FromDays(7))
            return T("Activity.Time.DaysAgo", Math.Max(1, (int)delta.TotalDays));

        return local.ToString("MMM d, yyyy â€¢ HH:mm", CultureInfo.InvariantCulture);
    }
}
