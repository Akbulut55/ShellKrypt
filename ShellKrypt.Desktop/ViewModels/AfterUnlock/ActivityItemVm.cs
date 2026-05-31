using System;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class ActivityItemVm : ObservableObject
{
    public ActivityItemVm(ActivityLogEntry entry)
    {
        Entry = entry;
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
        "vault" => "Vault",
        "web" => "Web Logins",
        "cards" => "Credit Cards",
        "notes" => "Markdown Notes",
        "authenticator" => "Authenticator",
        "api_keys" => "API Keys",
        "audit" => "Security Audit",
        "generator" => "Generator",
        "settings" => "Settings",
        "transfer" => "Export",
        "activity" => "Activity Logs",
        _ => "System"
    };
    public string TimestampDisplay => FormatTimestamp(Entry.TimestampUtc);
    public string SeverityChipText => Entry.Severity switch
    {
        "warning" => "Warning",
        "success" => "Success",
        "danger" => "Danger",
        _ => "Info"
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
        "generator" => "GE",
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

    private static string FormatTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        var local = parsed.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        if (delta < TimeSpan.FromMinutes(1))
            return "Just now";
        if (delta < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        if (delta < TimeSpan.FromDays(1))
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        if (delta < TimeSpan.FromDays(7))
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";

        return local.ToString("MMM d, yyyy â€¢ HH:mm", CultureInfo.InvariantCulture);
    }
}
