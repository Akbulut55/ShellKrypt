using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
    private static byte[] ActivityLogAssociatedData(string id)
        => AesGcmBlob.CreateAssociatedData("activity-log", "v1", id);

    private sealed record ActivityLogPayload(
        string Category,
        string Title,
        string Detail,
        string Severity,
        string? AffectedItem);
}
