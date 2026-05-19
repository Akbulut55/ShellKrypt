using System.Text;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Crypto;

public static class VaultPayloadProtector
{
    public static byte[] EncryptVaultKey(byte[] wrappingKey, byte[] vaultKey)
        => AesGcmBlob.Encrypt(wrappingKey, vaultKey, AesGcmBlob.CreateAssociatedData("vault-key", "v1"));

    public static byte[] DecryptVaultKey(byte[] wrappingKey, byte[] encryptedVaultKey)
        => AesGcmBlob.Decrypt(wrappingKey, encryptedVaultKey, AesGcmBlob.CreateAssociatedData("vault-key", "v1"));

    public static byte[] EncryptItemPayload(byte[] vaultKey, VaultItemHeader header, byte[] plaintext)
        => AesGcmBlob.Encrypt(vaultKey, plaintext, ItemAssociatedData(header.Id, header.Type));

    public static byte[] DecryptItemPayload(byte[] vaultKey, VaultItemHeader header, byte[] encryptedPayload)
        => AesGcmBlob.Decrypt(vaultKey, encryptedPayload, ItemAssociatedData(header.Id, header.Type));

    public static byte[] EncryptLabelName(byte[] vaultKey, string labelId, string name)
        => AesGcmBlob.Encrypt(vaultKey, Encoding.UTF8.GetBytes(name), LabelAssociatedData(labelId));

    public static string DecryptLabelName(byte[] vaultKey, string labelId, byte[]? encryptedName, string? legacyName)
    {
        if (encryptedName is { Length: > 0 })
            return Encoding.UTF8.GetString(AesGcmBlob.Decrypt(vaultKey, encryptedName, LabelAssociatedData(labelId)));

        return legacyName ?? string.Empty;
    }

    public static byte[] ItemAssociatedData(string itemId, ItemType itemType)
        => AesGcmBlob.CreateAssociatedData("item-payload", "v1", ((int)itemType).ToString(), itemId);

    public static byte[] LabelAssociatedData(string labelId)
        => AesGcmBlob.CreateAssociatedData("label-name", "v1", labelId);
}
