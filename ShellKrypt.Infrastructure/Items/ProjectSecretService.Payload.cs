using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ProjectSecretService
{
    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, ProjectSecretPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static ProjectSecretPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => ProjectSecretPayloadCompatibility.Deserialize(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);
}
