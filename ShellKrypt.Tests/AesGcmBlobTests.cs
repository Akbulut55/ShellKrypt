using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Infrastructure.Crypto;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class AesGcmBlobTests
{
    [Fact]
    public void Envelope_DecryptsWithMatchingAssociatedData()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var aad = AesGcmBlob.CreateAssociatedData("test", "v1", "item-1");
        var plaintext = Encoding.UTF8.GetBytes("secret payload");

        var encrypted = AesGcmBlob.Encrypt(key, plaintext, aad);
        var decrypted = AesGcmBlob.Decrypt(key, encrypted, aad);

        Assert.Equal(plaintext, decrypted);
        Assert.StartsWith("SKBLOB", Encoding.ASCII.GetString(encrypted, 0, 6));
    }

    [Fact]
    public void Envelope_RejectsTamperingWrongKeyWrongAssociatedDataAndTruncation()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = AesGcmBlob.Encrypt(
            key,
            Encoding.UTF8.GetBytes("secret payload"),
            AesGcmBlob.CreateAssociatedData("purpose", "one"));

        var tampered = encrypted.ToArray();
        tampered[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmBlob.Decrypt(key, tampered, AesGcmBlob.CreateAssociatedData("purpose", "one")));

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmBlob.Decrypt(RandomNumberGenerator.GetBytes(32), encrypted, AesGcmBlob.CreateAssociatedData("purpose", "one")));

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmBlob.Decrypt(key, encrypted, AesGcmBlob.CreateAssociatedData("purpose", "two")));

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmBlob.Decrypt(key, encrypted[..10], AesGcmBlob.CreateAssociatedData("purpose", "one")));
    }
}
