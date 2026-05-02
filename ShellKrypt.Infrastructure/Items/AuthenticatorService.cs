using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class AuthenticatorService : IAuthenticatorService
{
    private const string DefaultAlgorithm = "HMAC-SHA1";
    private const int DefaultDigits = 6;
    private const int DefaultPeriod = 30;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public AuthenticatorService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<AuthenticatorEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var entries = new List<AuthenticatorEntry>();

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Authenticator))
        {
            var payload = DecryptPayload(vaultKey, row.EncryptedPayload);
            if (payload is null)
                continue;

            entries.Add(ToEntry(row.Header, payload));
        }

        return entries;
    }

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

        await _repo.InsertAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);
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

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);
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

        await _repo.UpdateAsync(vaultPath, header, EncryptPayload(vaultKey, payload), ct);
        return ToEntry(header, payload);
    }

    public Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
        => _repo.DeleteAsync(vaultPath, id, ct);

    public AuthenticatorCodeSnapshot GetCurrentCode(AuthenticatorEntry entry, DateTimeOffset? now = null)
    {
        try
        {
            var secretBytes = DecodeBase32(NormalizeSecret(entry.Secret));
            var digits = NormalizeDigits(entry.Digits);
            var counter = entry.KeyType == AuthenticatorKeyType.CounterBased
                ? (ulong)NormalizeCounter(entry.Counter)
                : CalculateTimeCounter(now ?? DateTimeOffset.UtcNow, NormalizePeriod(entry.PeriodSeconds));

            var code = GenerateCode(secretBytes, counter, NormalizeAlgorithm(entry.Algorithm), digits);
            if (entry.KeyType == AuthenticatorKeyType.CounterBased)
                return new AuthenticatorCodeSnapshot(code, 0, 0, true);

            var snapshotTime = now ?? DateTimeOffset.UtcNow;
            var period = NormalizePeriod(entry.PeriodSeconds);
            var unixSeconds = snapshotTime.ToUnixTimeSeconds();
            var remaining = period - (int)(unixSeconds % period);
            if (remaining <= 0)
                remaining = period;

            var progressPercent = ((period - remaining) / (double)period) * 100d;
            return new AuthenticatorCodeSnapshot(code, remaining, progressPercent, true);
        }
        catch
        {
            return new AuthenticatorCodeSnapshot("------", 0, 0, false);
        }
    }

    private async Task<AuthenticatorEntry?> GetEntryAsync(string vaultPath, byte[] vaultKey, string id, CancellationToken ct)
    {
        var entries = await ListAsync(vaultPath, vaultKey, ct);
        return entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }

    private static byte[] EncryptPayload(byte[] vaultKey, AuthenticatorPayload payload)
        => AesGcmBlob.Encrypt(vaultKey, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static AuthenticatorPayload? DecryptPayload(byte[] vaultKey, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<AuthenticatorPayload>(AesGcmBlob.Decrypt(vaultKey, encryptedPayload), JsonOpts);

    private static AuthenticatorPayload ToPayload(AuthenticatorInput input, string lastUsedAtUtc)
    {
        var normalizedSecret = NormalizeSecret(input.Secret);
        _ = DecodeBase32(normalizedSecret);

        return new AuthenticatorPayload(
            ServiceName: NormalizeText(input.Name, "Authenticator"),
            Issuer: string.Empty,
            AccountLabel: string.Empty,
            Secret: normalizedSecret,
            Algorithm: NormalizeAlgorithm(input.Algorithm),
            Digits: NormalizeDigits(input.Digits),
            PeriodSeconds: NormalizePeriod(input.PeriodSeconds),
            RecoveryNotes: string.Empty,
            LastUsedAtUtc: string.IsNullOrWhiteSpace(lastUsedAtUtc) ? string.Empty : lastUsedAtUtc,
            KeyType: SerializeKeyType(input.KeyType),
            Counter: NormalizeCounter(input.Counter));
    }

    private static AuthenticatorEntry ToEntry(VaultItemHeader header, AuthenticatorPayload payload)
    {
        var name = FirstNonEmpty(payload.ServiceName, payload.Issuer, payload.AccountLabel, "Authenticator");

        return new AuthenticatorEntry(
            Id: header.Id,
            Name: name,
            Secret: payload.Secret,
            KeyType: NormalizeKeyType(payload.KeyType),
            Counter: NormalizeCounter(payload.Counter),
            Algorithm: NormalizeAlgorithm(payload.Algorithm),
            Digits: NormalizeDigits(payload.Digits),
            PeriodSeconds: NormalizePeriod(payload.PeriodSeconds),
            LastUsedAtUtc: payload.LastUsedAtUtc ?? string.Empty,
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc);
    }

    private static ulong CalculateTimeCounter(DateTimeOffset timestamp, int period)
        => (ulong)(timestamp.ToUnixTimeSeconds() / period);

    private static string GenerateCode(byte[] secretBytes, ulong counter, string algorithm, int digits)
    {
        var counterBytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(counterBytes, counter);

        using var hmac = CreateHmac(algorithm, secretBytes);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        var modulus = digits == 8 ? 100000000 : 1000000;
        var otp = binary % modulus;
        return otp.ToString(new string('0', digits));
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeText(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string NormalizeSecret(string? secret)
        => new string((secret ?? string.Empty)
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA1" or "HMAC-SHA1" => "HMAC-SHA1",
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => DefaultAlgorithm
        };
    }

    private static int NormalizeDigits(int digits)
        => digits == 8 ? 8 : DefaultDigits;

    private static int NormalizePeriod(int seconds)
        => seconds is >= 1 and <= 300 ? seconds : DefaultPeriod;

    private static long NormalizeCounter(long counter)
        => counter < 0 ? 0 : counter;

    private static AuthenticatorKeyType NormalizeKeyType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "counter-based" or "counter" or "hotp" => AuthenticatorKeyType.CounterBased,
            _ => AuthenticatorKeyType.TimeBased
        };
    }

    private static string SerializeKeyType(AuthenticatorKeyType keyType)
        => keyType == AuthenticatorKeyType.CounterBased ? "counter-based" : "time-based";

    private static HMAC CreateHmac(string algorithm, byte[] key)
        => NormalizeAlgorithm(algorithm) switch
        {
            "HMAC-SHA256" => new HMACSHA256(key),
            "HMAC-SHA512" => new HMACSHA512(key),
            _ => new HMACSHA1(key)
        };

    private static byte[] DecodeBase32(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Authenticator secret is required.");

        var buffer = new List<byte>(value.Length * 5 / 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var ch in value)
        {
            var mapped = ch switch
            {
                >= 'A' and <= 'Z' => ch - 'A',
                >= '2' and <= '7' => ch - '2' + 26,
                '=' => -1,
                _ => throw new InvalidOperationException("Authenticator secret must be valid Base32.")
            };

            if (mapped < 0)
                break;

            bitBuffer = (bitBuffer << 5) | mapped;
            bitCount += 5;

            while (bitCount >= 8)
            {
                bitCount -= 8;
                buffer.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        if (buffer.Count == 0)
            throw new InvalidOperationException("Authenticator secret must be valid Base32.");

        return buffer.ToArray();
    }
}
