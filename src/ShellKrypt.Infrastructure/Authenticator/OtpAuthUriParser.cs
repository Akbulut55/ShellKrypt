using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Infrastructure.Authenticator;

public sealed class OtpAuthUriParser : IOtpAuthUriParser
{
    public ParsedOtpAuthSecret Parse(string otpauthUri)
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
        var secret = AuthenticatorNormalization.Secret(GetQueryValue(query, "secret"));
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("QR code did not include a secret key.");

        var label = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        var issuer = (GetQueryValue(query, "issuer") ?? string.Empty).Trim();
        var name = DetermineName(label, issuer);
        var algorithm = AuthenticatorNormalization.Algorithm(GetQueryValue(query, "algorithm"));
        var digits = int.TryParse(GetQueryValue(query, "digits"), out var parsedDigits)
            ? AuthenticatorNormalization.Digits(parsedDigits)
            : AuthenticatorNormalization.DefaultDigits;
        var period = keyType == AuthenticatorKeyType.TimeBased && int.TryParse(GetQueryValue(query, "period"), out var parsedPeriod)
            ? AuthenticatorNormalization.Period(parsedPeriod)
            : AuthenticatorNormalization.DefaultPeriod;
        var counter = keyType == AuthenticatorKeyType.CounterBased && long.TryParse(GetQueryValue(query, "counter"), out var parsedCounter)
            ? AuthenticatorNormalization.Counter(parsedCounter)
            : 0;

        return new ParsedOtpAuthSecret(name, secret, keyType, counter, algorithm, digits, period);
    }

    private static AuthenticatorKeyType ParseKeyType(string host)
        => host.Trim().ToLowerInvariant() switch
        {
            "totp" => AuthenticatorKeyType.TimeBased,
            "hotp" => AuthenticatorKeyType.CounterBased,
            _ => throw new InvalidOperationException("QR code contained an unsupported OTP type.")
        };

    private static string DetermineName(string label, string issuer)
    {
        var normalizedLabel = label?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(issuer))
            return issuer;
        if (normalizedLabel.Contains(':'))
            return normalizedLabel.Split(':', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(normalizedLabel) ? "Authenticator" : normalizedLabel;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
}
