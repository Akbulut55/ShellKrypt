using System.Globalization;
using System.Text.Json;
using ShellKrypt.Application.Activity;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed class ActivityReportService(TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DateTimeOffset Now => timeProvider.GetLocalNow();

    public Task WriteAsync(string path, string json, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, json, cancellationToken);

    public string BuildJson(
        string exportScope,
        string vaultDisplayName,
        IReadOnlyList<ActivityItemVm> events,
        int sourceTotalEvents,
        ActivityAppliedFilters filters)
    {
        var report = new ActivityLogReport(
            ReportType: "ShellKrypt Plaintext Activity Logs Report",
            ExportScope: exportScope,
            Vault: vaultDisplayName,
            GeneratedAt: timeProvider.GetLocalNow().ToString("O", CultureInfo.InvariantCulture),
            SourceTotalEvents: sourceTotalEvents,
            TotalEvents: events.Count,
            AppliedFilters: filters,
            ChecksumNotice: "ContentChecksum is an unkeyed SHA-256 content checksum. It can identify content differences but does not prove origin, authenticity, or tamper resistance.",
            Events: events.Select(ToReportEvent).ToArray());

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private ActivityLogReportEvent ToReportEvent(ActivityItemVm item)
        => new(
            Id: item.Id,
            TimestampUtc: item.Entry.TimestampUtc,
            TimestampLocal: FormatLocalTimestamp(item.Entry.TimestampUtc),
            Category: item.CategoryLabel,
            Status: item.SeverityChipText,
            Event: item.Title,
            AffectedItem: item.AffectedItemDisplay,
            Detail: item.Detail,
            ContentChecksum: ActivityContentChecksum.Compute(item.Entry));

    private string FormatLocalTimestamp(string timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? TimeZoneInfo.ConvertTime(parsed, timeProvider.LocalTimeZone).ToString("MMM d, yyyy | HH:mm:ss", CultureInfo.InvariantCulture)
            : "Unknown";

    private sealed record ActivityLogReport(
        string ReportType,
        string ExportScope,
        string Vault,
        string GeneratedAt,
        int SourceTotalEvents,
        int TotalEvents,
        ActivityAppliedFilters AppliedFilters,
        string ChecksumNotice,
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
        string ContentChecksum);
}
