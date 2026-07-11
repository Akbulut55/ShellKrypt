namespace ShellKrypt.Application.Activity;

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
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
