using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService
{
    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, AuthenticatorPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static AuthenticatorPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<AuthenticatorPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);

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
}
