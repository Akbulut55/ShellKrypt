namespace ShellKrypt.Application.Notes;

public sealed record NoteInput(string Title, string? Content, bool Favorite);

public sealed record NoteEntry(
    string Id,
    string Title,
    string Content,
    bool Favorite,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public enum NoteFailureKind
{
    None = 0,
    Unavailable,
    ValidationFailed,
    ReadFailed,
    WriteFailed,
    DeleteFailed
}

public sealed record NoteLoadResult(
    IReadOnlyList<NoteEntry> Entries,
    int SkippedCorruptEntries,
    NoteFailureKind FailureKind)
{
    public bool Success => FailureKind == NoteFailureKind.None;
    public static NoteLoadResult Empty { get; } = new([], 0, NoteFailureKind.None);
}

public sealed record NoteMutationResult(NoteEntry? Entry, NoteFailureKind FailureKind)
{
    public bool Success => FailureKind == NoteFailureKind.None && Entry is not null;
    public static NoteMutationResult Failed(NoteFailureKind kind) => new(null, kind);
    public static NoteMutationResult Succeeded(NoteEntry entry) => new(entry, NoteFailureKind.None);
}

public readonly record struct NoteOperationResult(NoteFailureKind FailureKind)
{
    public bool Success => FailureKind == NoteFailureKind.None;
    public static NoteOperationResult Succeeded { get; } = new(NoteFailureKind.None);
}

public interface INoteService
{
    Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<NoteMutationResult> AddAsync(string vaultPath, byte[] vaultKey, NoteInput input, CancellationToken ct = default);
    Task<NoteMutationResult> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, NoteInput input, CancellationToken ct = default);
    Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}

public interface INoteStore
{
    Task<NoteLoadResult> LoadAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<NoteOperationResult> InsertAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default);
    Task<NoteOperationResult> UpdateAsync(string vaultPath, byte[] vaultKey, NoteEntry entry, CancellationToken ct = default);
    Task<NoteOperationResult> DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}
