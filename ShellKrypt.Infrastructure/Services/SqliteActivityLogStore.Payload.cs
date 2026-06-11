using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
    private static byte[] ActivityLogAssociatedData(string id, string timestampUtc)
        => AesGcmBlob.CreateAssociatedData("activity-log", "v2", id, timestampUtc);

    private sealed record ActivityLogPayload(
        string Category,
        string Title,
        string Detail,
        string Severity,
        string? AffectedItem);
}
