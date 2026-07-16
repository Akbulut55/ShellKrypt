using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public partial class ActivityViewModel
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true
    };

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        Error = string.Empty;

        if (_allItems.Count == 0)
        {
            Error = T(_root, "Activity.Export.NoLogs");
            return;
        }

        var suggestedName = $"ShellKrypt-{SanitizeFileName(CurrentVaultDisplayName)}-activity-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json";
        var exportPath = await _root.PickSaveFileAsync(
            T(_root, "Activity.Export.DialogTitle"),
            suggestedName,
            ".json",
            [".json"],
            T(_root, "Activity.Export.FileType"));

        if (string.IsNullOrWhiteSpace(exportPath))
            return;

        await File.WriteAllTextAsync(exportPath, BuildActivityReportJson());
        Error = T(_root, "Activity.Export.PlaintextWarning");
        _root.LogActivity("activity", "Activity report exported", $"Saved {_allItems.Count} activity log entries to {Path.GetFileName(exportPath)}.", "info", affectedItem: Path.GetFileName(exportPath));
    }

    private string BuildActivityReportJson()
    {
        var report = new ActivityLogReport(
            ReportType: "ShellKrypt Plaintext Activity Logs Report",
            Vault: CurrentVaultDisplayName,
            GeneratedAt: DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            TotalEvents: _allItems.Count,
            Events: _allItems
                .OrderByDescending(item => item.Entry.TimestampUtc, StringComparer.Ordinal)
                .Select(item => new ActivityLogReportEvent(
                    Id: item.Id,
                    TimestampUtc: item.Entry.TimestampUtc,
                    TimestampLocal: FormatMetadataTimestamp(item.Entry.TimestampUtc),
                    Category: item.CategoryLabel,
                    Status: item.SeverityChipText,
                    Event: item.Title,
                    AffectedItem: item.AffectedItemDisplay,
                    Detail: item.Detail,
                    IntegrityHash: ComputeIntegrityHash(item.Entry)))
                .ToArray());

        return JsonSerializer.Serialize(report, ReportJsonOptions);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "vault" : sanitized;
    }

    private sealed record ActivityLogReport(
        string ReportType,
        string Vault,
        string GeneratedAt,
        int TotalEvents,
        IReadOnlyList<ActivityLogReportEvent> Events);

    private sealed record ActivityLogReportEvent(
        string Id,
        string TimestampUtc,
        string TimestampLocal,
        string Category,
        string Status,
        string Event,
        string AffectedItem,
        string Detail,
        string IntegrityHash);
}
