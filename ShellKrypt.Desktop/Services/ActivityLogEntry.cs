namespace ShellKrypt.Desktop.Services;

public sealed record ActivityLogEntry(
    string Id,
    string TimestampUtc,
    string Category,
    string Title,
    string Detail,
    string Severity,
    string? VaultPath)
{
    public string? AffectedItem { get; init; }
}
