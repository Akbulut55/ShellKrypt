using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Infrastructure.Authenticator;

internal static class AuthenticatorNormalization
{
    internal const string DefaultAlgorithm = "HMAC-SHA1";
    internal const int DefaultDigits = 6;
    internal const int DefaultPeriod = 30;

    internal static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    internal static string Text(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    internal static string Secret(string? secret)
        => new string((secret ?? string.Empty)
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    internal static string Algorithm(string? algorithm)
        => algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA1" or "HMAC-SHA1" => "HMAC-SHA1",
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => DefaultAlgorithm
        };

    internal static int Digits(int digits) => digits == 8 ? 8 : DefaultDigits;
    internal static int Period(int seconds) => seconds is >= 1 and <= 300 ? seconds : DefaultPeriod;
    internal static long Counter(long counter) => counter < 0 ? 0 : counter;

    internal static AuthenticatorKeyType KeyType(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "counter-based" or "counter" or "hotp" => AuthenticatorKeyType.CounterBased,
            _ => AuthenticatorKeyType.TimeBased
        };

    internal static string KeyType(AuthenticatorKeyType keyType)
        => keyType == AuthenticatorKeyType.CounterBased ? "counter-based" : "time-based";
}
