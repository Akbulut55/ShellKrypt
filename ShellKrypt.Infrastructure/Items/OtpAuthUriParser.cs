using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed record ParsedOtpAuthSecret(
    string Name,
    string Secret,
    AuthenticatorKeyType KeyType,
    long Counter,
    string Algorithm,
    int Digits,
    int PeriodSeconds);

public static class OtpAuthUriParser
{
    public static ParsedOtpAuthSecret Parse(string otpauthUri)
    {
        if (string.IsNullOrWhiteSpace(otpauthUri))
            throw new InvalidOperationException("QR code did not contain an otpauth link.");

        if (!Uri.TryCreate(otpauthUri.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("QR code did not contain a valid otpauth link.");
        }

        var keyType = ParseKeyType(uri.Host);
        var query = ParseQuery(uri.Query);
        var secret = NormalizeSecret(GetQueryValue(query, "secret"));
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("QR code did not include a secret key.");

        var label = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        var issuer = (GetQueryValue(query, "issuer") ?? string.Empty).Trim();
        var name = DetermineName(label, issuer);
        var algorithm = NormalizeAlgorithm(GetQueryValue(query, "algorithm"));
        var digits = NormalizeDigits(GetQueryValue(query, "digits"));
        var period = keyType == AuthenticatorKeyType.TimeBased
            ? NormalizePeriod(GetQueryValue(query, "period"))
            : 30;
        var counter = keyType == AuthenticatorKeyType.CounterBased
            ? NormalizeCounter(GetQueryValue(query, "counter"))
            : 0;

        return new ParsedOtpAuthSecret(name, secret, keyType, counter, algorithm, digits, period);
    }

    private static AuthenticatorKeyType ParseKeyType(string host)
    {
        return host.Trim().ToLowerInvariant() switch
        {
            "totp" => AuthenticatorKeyType.TimeBased,
            "hotp" => AuthenticatorKeyType.CounterBased,
            _ => throw new InvalidOperationException("QR code contained an unsupported OTP type.")
        };
    }

    private static string DetermineName(string label, string issuer)
    {
        var normalizedLabel = label?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(issuer))
            return issuer;

        if (normalizedLabel.Contains(':'))
            return normalizedLabel.Split(':', 2)[0].Trim();

        return string.IsNullOrWhiteSpace(normalizedLabel) ? "Authenticator" : normalizedLabel;
    }

    private static string NormalizeSecret(string? secret)
        => new string((secret ?? string.Empty)
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return result;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> query, string key)
        => query.TryGetValue(key, out var value) ? value : null;

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => "HMAC-SHA1"
        };
    }

    private static int NormalizeDigits(string? digits)
        => int.TryParse(digits, out var parsed) && parsed == 8 ? 8 : 6;

    private static int NormalizePeriod(string? period)
        => int.TryParse(period, out var parsed) && parsed is >= 1 and <= 300 ? parsed : 30;

    private static long NormalizeCounter(string? counter)
        => long.TryParse(counter, out var parsed) && parsed > 0 ? parsed : 0;
}
