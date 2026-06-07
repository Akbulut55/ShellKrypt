using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class CardService
{
    public async Task<CardEntry> AddAsync(string vaultPath, byte[] vaultKey, CardInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.Card,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<CardEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        CardInput input,
        CancellationToken ct = default)
    {
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.Card,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);
}
