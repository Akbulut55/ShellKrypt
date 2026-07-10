using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Core.CryptoTools;

namespace ShellKrypt.Infrastructure.CryptoTools;

public sealed class HashService : IHashService
{
    public string ComputeSha256(string input)
        => ComputeHash(input, SHA256.HashData);

    public string ComputeSha512(string input)
        => ComputeHash(input, SHA512.HashData);

    private static string ComputeHash(string input, Func<byte[], byte[]> hash)
    {
        if (input.Length == 0)
            return "";

        return Convert.ToHexString(hash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
