using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class WebLoginService : IWebLoginService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public WebLoginService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<WebLoginEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, ct);
        var logins = new List<WebLoginEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Web))
        {
            var payload = DecryptPayload(vaultKey, row.EncryptedPayload);
            if (payload is null)
                continue;

            logins.Add(ToEntry(row.Header, payload));
        }

        return logins;
    }

    public async Task<WebLoginEntry> AddAsync(string vaultPath, byte[] vaultKey, WebLoginInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.Web,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public async Task<WebLoginEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        WebLoginInput input,
        CancellationToken ct = default)
    {
        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.Web,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);

        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);

    private static byte[] EncryptPayload(byte[] vaultKey, WebPayload payload)
        => AesGcmBlob.Encrypt(vaultKey, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static WebPayload? DecryptPayload(byte[] vaultKey, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<WebPayload>(AesGcmBlob.Decrypt(vaultKey, encryptedPayload), JsonOpts);

    private static WebPayload ToPayload(WebLoginInput input)
        => new(
            Title: input.Title.Trim(),
            Url: input.Url.Trim(),
            Username: input.Username.Trim(),
            Password: input.Password,
            Notes: input.Notes.Trim())
        {
            Email = input.Email.Trim()
        };

    private static WebLoginEntry ToEntry(VaultItemHeader header, WebPayload payload)
        => new(
            Id: header.Id,
            Title: payload.Title,
            Url: payload.Url,
            Username: payload.Username,
            Email: payload.Email,
            Password: payload.Password,
            Notes: payload.Notes,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);
}
