using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class NoteService
{
    public async Task<NoteEntry> AddAsync(string vaultPath, byte[] vaultKey, NoteInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.Note,
            Favorite: input.Favorite,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<NoteEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        NoteInput input,
        CancellationToken ct = default)
    {
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.Note,
            Favorite: input.Favorite,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);
}
