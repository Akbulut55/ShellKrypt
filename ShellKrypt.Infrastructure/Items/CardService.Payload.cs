using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class CardService
{
    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, CardPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static CardPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<CardPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);
}
