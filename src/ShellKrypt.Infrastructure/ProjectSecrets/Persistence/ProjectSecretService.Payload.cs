using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed partial class ProjectSecretService
{
    private static byte[] EncryptPayload(byte[] vaultKey, VaultItemHeader header, ProjectSecretPayload payload)
        => VaultPayloadProtector.EncryptItemPayload(vaultKey, header, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));

    private static ProjectSecretPayload? DecryptPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => JsonSerializer.Deserialize<ProjectSecretPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, header, encryptedPayload), JsonOpts);
}
