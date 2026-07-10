namespace ShellKrypt.Core.Items;

public sealed record NoteInput(
    string Title,
    string Content,
    bool Favorite);

public sealed record NoteEntry(
    string Id,
    string Title,
    string Content,
    bool Favorite,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public interface INoteService
{
    Task<IReadOnlyList<NoteEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<NoteEntry> AddAsync(string vaultPath, byte[] vaultKey, NoteInput input, CancellationToken ct = default);
    Task<NoteEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, NoteInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}
