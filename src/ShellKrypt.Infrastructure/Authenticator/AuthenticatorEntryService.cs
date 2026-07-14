using System.Text.Json;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Infrastructure.Authenticator;

public sealed class AuthenticatorEntryService : IAuthenticatorEntryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repository;

    public AuthenticatorEntryService(IItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AuthenticatorEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repository.ListAsync(vaultPath, vaultKey, ct);
        var entries = new List<AuthenticatorEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Authenticator))
        {
            var payload = DecryptPayload(vaultKey, row.Header, row.EncryptedPayload);
            if (payload is not null)
                entries.Add(ToEntry(row.Header, payload));
        }

        return entries;
    }

    public async Task<AuthenticatorEntry> AddAsync(string vaultPath, byte[] vaultKey, AuthenticatorInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(Guid.NewGuid().ToString("N"), ItemType.Authenticator, false, now, now);
        var payload = ToPayload(input, string.Empty);
        await _repository.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public async Task<AuthenticatorEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, AuthenticatorInput input, CancellationToken ct = default)
    {
        var existing = await GetEntryAsync(vaultPath, vaultKey, id, ct)
            ?? throw new InvalidOperationException("Authenticator entry was not found.");
        var header = new VaultItemHeader(id, ItemType.Authenticator, false, createdAtUtc, DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input, existing.LastUsedAtUtc);
        await _repository.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public async Task<AuthenticatorEntry> MarkUsedAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct = default)
    {
        var existing = await GetEntryAsync(vaultPath, vaultKey, id, ct)
            ?? throw new InvalidOperationException("Authenticator entry was not found.");
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(existing.Id, ItemType.Authenticator, false, existing.CreatedAtUtc, now);
        var payload = ToPayload(
            new AuthenticatorInput(
                existing.Name,
                existing.Secret,
                existing.KeyType,
                existing.KeyType == AuthenticatorKeyType.CounterBased ? existing.Counter + 1 : existing.Counter,
                existing.Algorithm,
                existing.Digits,
                existing.PeriodSeconds),
            now);
        await _repository.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repository.DeleteAsync(vaultPath, id, ct);

    private async Task<AuthenticatorEntry?> GetEntryAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct)
        => (await ListAsync(vaultPath, vaultKey, ct)).FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));

    private static AuthenticatorPayload ToPayload(AuthenticatorInput input, string lastUsedAtUtc)
    {
        var secret = AuthenticatorNormalization.Secret(input.Secret);
        _ = Base32Codec.Decode(secret);

        return new AuthenticatorPayload(
            AuthenticatorNormalization.Text(input.Name, "Authenticator"),
            string.Empty,
            string.Empty,
            secret,
            AuthenticatorNormalization.Algorithm(input.Algorithm),
            AuthenticatorNormalization.Digits(input.Digits),
            AuthenticatorNormalization.Period(input.PeriodSeconds),
            string.Empty,
            string.IsNullOrWhiteSpace(lastUsedAtUtc) ? string.Empty : lastUsedAtUtc,
            AuthenticatorNormalization.KeyType(input.KeyType),
            AuthenticatorNormalization.Counter(input.Counter));
    }

    private static AuthenticatorEntry ToEntry(VaultItemHeader header, AuthenticatorPayload payload)
        => new(
            header.Id,
            AuthenticatorNormalization.FirstNonEmpty(payload.ServiceName, payload.Issuer, payload.AccountLabel, "Authenticator"),
            payload.Secret,
            AuthenticatorNormalization.KeyType(payload.KeyType),
            AuthenticatorNormalization.Counter(payload.Counter),
            AuthenticatorNormalization.Algorithm(payload.Algorithm),
            AuthenticatorNormalization.Digits(payload.Digits),
            AuthenticatorNormalization.Period(payload.PeriodSeconds),
            payload.LastUsedAtUtc ?? string.Empty,
            header.CreatedAtUtc,
            header.UpdatedAtUtc);

    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, AuthenticatorPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    private static AuthenticatorPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<AuthenticatorPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOptions);
}
