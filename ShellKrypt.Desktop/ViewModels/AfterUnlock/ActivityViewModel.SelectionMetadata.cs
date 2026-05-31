using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ActivityViewModel
{
    private static string FormatMetadataTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return "Unknown";

        return parsed.ToLocalTime().ToString("MMM d, yyyy | HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string ComputeIntegrityHash(ActivityLogEntry entry)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{entry.Id}|{entry.TimestampUtc}|{entry.Category}|{entry.Title}|{entry.Detail}|{entry.Severity}|{entry.VaultPath}|{entry.AffectedItem}");
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
