using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class WebLoginService
{
    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, WebPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static WebPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<WebPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);
}
