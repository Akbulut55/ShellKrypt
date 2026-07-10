using System.Security.Cryptography;
using System.Text;

namespace ShellKrypt.Infrastructure.Crypto;

public static class AesGcmBlob
{
    public const int NonceSize = 12;
    public const int TagSize = 16;

    private const byte Version = 1;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SKBLOB");

    public static byte[] Encrypt(byte[] key, byte[] plaintext, byte[]? associatedData = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return PackEnvelope(nonce, tag, ciphertext);
    }

    public static byte[] Decrypt(byte[] key, byte[] blob, byte[]? associatedData = null)
    {
        if (!HasEnvelope(blob))
            throw new CryptographicException("Invalid encrypted blob envelope.");

        UnpackEnvelope(blob, out var nonce, out var tag, out var ciphertext);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        return plaintext;
    }

    public static byte[] CreateAssociatedData(params string[] parts)
        => Encoding.UTF8.GetBytes(string.Join('\u001f', parts));

    private static byte[] PackEnvelope(byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var headerLength = Magic.Length + 3;
        var blob = new byte[headerLength + nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(Magic, 0, blob, 0, Magic.Length);
        blob[Magic.Length] = Version;
        blob[Magic.Length + 1] = (byte)nonce.Length;
        blob[Magic.Length + 2] = (byte)tag.Length;
        Buffer.BlockCopy(nonce, 0, blob, headerLength, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, headerLength + nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, blob, headerLength + nonce.Length + tag.Length, ciphertext.Length);
        return blob;
    }

    public static bool HasEnvelope(byte[] blob)
    {
        if (blob.Length < Magic.Length + 3)
            return false;

        for (var i = 0; i < Magic.Length; i++)
        {
            if (blob[i] != Magic[i])
                return false;
        }

        return true;
    }

    private static void UnpackEnvelope(byte[] blob, out byte[] nonce, out byte[] tag, out byte[] ciphertext)
    {
        if (!HasEnvelope(blob))
            throw new CryptographicException("Invalid encrypted blob envelope.");

        var version = blob[Magic.Length];
        var nonceLength = blob[Magic.Length + 1];
        var tagLength = blob[Magic.Length + 2];
        var headerLength = Magic.Length + 3;

        if (version != Version || nonceLength != NonceSize || tagLength != TagSize)
            throw new CryptographicException("Unsupported encrypted blob envelope.");

        if (blob.Length < headerLength + nonceLength + tagLength)
            throw new CryptographicException("Invalid ciphertext blob.");

        nonce = new byte[nonceLength];
        tag = new byte[tagLength];
        ciphertext = new byte[blob.Length - headerLength - nonceLength - tagLength];

        Buffer.BlockCopy(blob, headerLength, nonce, 0, nonce.Length);
        Buffer.BlockCopy(blob, headerLength + nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(blob, headerLength + nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);
    }

}
