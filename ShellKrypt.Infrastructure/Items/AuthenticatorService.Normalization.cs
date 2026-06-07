using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService
{
    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeText(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string NormalizeSecret(string? secret)
        => new string((secret ?? string.Empty)
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA1" or "HMAC-SHA1" => "HMAC-SHA1",
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => DefaultAlgorithm
        };
    }

    private static int NormalizeDigits(int digits)
        => digits == 8 ? 8 : DefaultDigits;

    private static int NormalizePeriod(int seconds)
        => seconds is >= 1 and <= 300 ? seconds : DefaultPeriod;

    private static long NormalizeCounter(long counter)
        => counter < 0 ? 0 : counter;

    private static AuthenticatorKeyType NormalizeKeyType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "counter-based" or "counter" or "hotp" => AuthenticatorKeyType.CounterBased,
            _ => AuthenticatorKeyType.TimeBased
        };
    }

    private static string SerializeKeyType(AuthenticatorKeyType keyType)
        => keyType == AuthenticatorKeyType.CounterBased ? "counter-based" : "time-based";

    private static byte[] DecodeBase32(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Authenticator secret is required.");

        var buffer = new List<byte>(value.Length * 5 / 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var ch in value)
        {
            var mapped = ch switch
            {
                >= 'A' and <= 'Z' => ch - 'A',
                >= '2' and <= '7' => ch - '2' + 26,
                '=' => -1,
                _ => throw new InvalidOperationException("Authenticator secret must be valid Base32.")
            };

            if (mapped < 0)
                break;

            bitBuffer = (bitBuffer << 5) | mapped;
            bitCount += 5;

            while (bitCount >= 8)
            {
                bitCount -= 8;
                buffer.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        if (buffer.Count == 0)
            throw new InvalidOperationException("Authenticator secret must be valid Base32.");

        return buffer.ToArray();
    }
}
