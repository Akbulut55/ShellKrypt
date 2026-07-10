using System.Security.Cryptography;
using System.Text;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static string? NormalizeLabelName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ComputeLabelLookupKey(string name)
    {
        var normalized = NormalizeLabelName(name) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }
}
