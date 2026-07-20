namespace ShellKrypt.Application.Activity;

public enum ActivityLogFailureKind
{
    None = 0,
    Unavailable,
    ReadFailed,
    WriteFailed,
    ClearFailed
}

public sealed record ActivityLogLoadResult(
    IReadOnlyList<ActivityLogEntry> Entries,
    int SkippedCorruptEntries,
    ActivityLogFailureKind FailureKind)
{
    public bool Success => FailureKind == ActivityLogFailureKind.None;

    public static ActivityLogLoadResult Empty { get; } = new([], 0, ActivityLogFailureKind.None);
}

public readonly record struct ActivityLogOperationResult(ActivityLogFailureKind FailureKind)
{
    public bool Success => FailureKind == ActivityLogFailureKind.None;

    public static ActivityLogOperationResult Succeeded { get; } = new(ActivityLogFailureKind.None);
}
