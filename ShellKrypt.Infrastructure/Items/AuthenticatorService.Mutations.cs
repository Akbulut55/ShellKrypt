using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService
{
    public async Task<AuthenticatorEntry> AddAsync(string vaultPath, byte[] vaultKey, AuthenticatorInput input, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: Guid.NewGuid().ToString("N"),
            Type: ItemType.Authenticator,
            Favorite: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var payload = ToPayload(input, lastUsedAtUtc: string.Empty);

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public async Task<AuthenticatorEntry> UpdateAsync(
        string vaultPath,
        byte[] vaultKey,
        string id,
        string createdAtUtc,
        AuthenticatorInput input,
        CancellationToken ct = default)
    {
        var existing = await GetEntryAsync(vaultPath, vaultKey, id, ct)
            ?? throw new InvalidOperationException("Authenticator entry was not found.");

        var header = new VaultItemHeader(
            Id: id,
            Type: ItemType.Authenticator,
            Favorite: false,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));
        var payload = ToPayload(input, existing.LastUsedAtUtc);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public async Task<AuthenticatorEntry> MarkUsedAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct = default)
    {
        var existing = await GetEntryAsync(vaultPath, vaultKey, id, ct)
            ?? throw new InvalidOperationException("Authenticator entry was not found.");

        var now = DateTimeOffset.UtcNow.ToString("O");
        var header = new VaultItemHeader(
            Id: existing.Id,
            Type: ItemType.Authenticator,
            Favorite: false,
            CreatedAtUtc: existing.CreatedAtUtc,
            UpdatedAtUtc: now);
        var payload = new AuthenticatorPayload(
            ServiceName: existing.Name,
            Issuer: string.Empty,
            AccountLabel: string.Empty,
            Secret: existing.Secret,
            Algorithm: existing.Algorithm,
            Digits: existing.Digits,
            PeriodSeconds: existing.PeriodSeconds,
            RecoveryNotes: string.Empty,
            LastUsedAtUtc: now,
            KeyType: SerializeKeyType(existing.KeyType),
            Counter: existing.KeyType == AuthenticatorKeyType.CounterBased ? existing.Counter + 1 : existing.Counter);

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, header, payload), ct);
        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);
}
