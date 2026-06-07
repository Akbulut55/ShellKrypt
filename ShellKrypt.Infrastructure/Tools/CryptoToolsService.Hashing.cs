using System.Security.Cryptography;
using System.Text;

namespace ShellKrypt.Infrastructure.Tools;

public sealed partial class CryptoToolsService
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
