using System.Text;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Crypto;

public static class VaultPayloadProtector
{
    public static byte[] EncryptVaultKey(byte[] wrappingKey, VaultKdfParams kdf, byte[] salt, byte[] vaultKey)
        => AesGcmBlob.Encrypt(wrappingKey, vaultKey, VaultKeyAssociatedData(kdf, salt));

    public static byte[] DecryptVaultKey(byte[] wrappingKey, VaultKdfParams kdf, byte[] salt, byte[] encryptedVaultKey)
        => AesGcmBlob.Decrypt(wrappingKey, encryptedVaultKey, VaultKeyAssociatedData(kdf, salt));

    public static byte[] EncryptItemPayload(byte[] vaultKey, VaultItemHeader header, byte[] plaintext)
        => AesGcmBlob.Encrypt(vaultKey, plaintext, ItemAssociatedData(header));

    public static byte[] DecryptItemPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => AesGcmBlob.Decrypt(vaultKey, encryptedPayload, ItemAssociatedData(header));

    public static byte[] EncryptLabelName(byte[] vaultKey, string labelId, string name)
        => AesGcmBlob.Encrypt(vaultKey, Encoding.UTF8.GetBytes(name), LabelAssociatedData(labelId));

    public static string DecryptLabelName(byte[] vaultKey, string labelId, byte[]? encryptedName, string? legacyName)
    {
        if (encryptedName is { Length: > 0 })
            return Encoding.UTF8.GetString(AesGcmBlob.Decrypt(vaultKey, encryptedName, LabelAssociatedData(labelId)));

        return legacyName ?? string.Empty;
    }

    public static byte[] VaultKeyAssociatedData(VaultKdfParams kdf, byte[] salt)
        => AesGcmBlob.CreateAssociatedData(
            "vault-key",
            "v2",
            kdf.MemoryKb.ToString(),
            kdf.Iterations.ToString(),
            kdf.Parallelism.ToString(),
            Convert.ToBase64String(salt));

    public static byte[] ItemAssociatedData(VaultItemHeader header)
        => AesGcmBlob.CreateAssociatedData(
            "item-payload",
            "v2",
            ((int)header.Type).ToString(),
            header.Id,
            header.Favorite ? "1" : "0",
            header.CreatedAtUtc,
            header.UpdatedAtUtc);

    public static byte[] LabelAssociatedData(string labelId)
        => AesGcmBlob.CreateAssociatedData("label-name", "v2", labelId);
}
