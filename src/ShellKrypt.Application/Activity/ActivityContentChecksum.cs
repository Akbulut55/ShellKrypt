using System.Security.Cryptography;
using System.Text.Json;

namespace ShellKrypt.Application.Activity;

public static class ActivityContentChecksum
{
    public static string Compute(ActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var canonical = JsonSerializer.SerializeToUtf8Bytes(new ChecksumContent(
            entry.Id ?? string.Empty,
            entry.TimestampUtc ?? string.Empty,
            entry.Category ?? string.Empty,
            entry.Title ?? string.Empty,
            entry.Detail ?? string.Empty,
            entry.Severity ?? string.Empty,
            entry.AffectedItem ?? string.Empty));

        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    private sealed record ChecksumContent(
        string Id,
        string TimestampUtc,
        string Category,
        string Title,
        string Detail,
        string Severity,
        string AffectedItem);
}
