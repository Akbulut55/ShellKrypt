using System.Security.Cryptography;

namespace ShellKrypt.Infrastructure.Crypto;

public static class AesGcmBlob
{
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Pack(nonce, tag, ciphertext);
    }

    public static byte[] Decrypt(byte[] key, byte[] blob)
    {
        Unpack(blob, out var nonce, out var tag, out var ciphertext);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return(plaintext);
    }

    private static byte[] Pack(byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var blob = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, blob, nonce.Length + tag.Length, ciphertext.Length);
        return blob;
    }

    private static void Unpack(byte[] blob, out byte[] nonce, out byte[] tag, out byte[] ciphertext)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext blob.");

        nonce = new byte[NonceSize];
        tag = new byte[TagSize];
        ciphertext = new byte[blob.Length - NonceSize - TagSize];

        Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(blob, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(blob, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);
    }
}
