using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class NoteService : INoteService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public NoteService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<NoteEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var notes = new List<NoteEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Note))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is null)
                continue;

            notes.Add(ToEntry(row.Header, payload));
        }

        return notes;
    }

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

    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, NotePayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static NotePayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<NotePayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);

    private static NotePayload ToPayload(NoteInput input)
        => new(
            Title: input.Title.Trim(),
            Content: input.Content);

    private static NoteEntry ToEntry(VaultItemHeader header, NotePayload payload)
        => new(
            Id: header.Id,
            Title: payload.Title,
            Content: payload.Content,
            Favorite: header.Favorite,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);
}
