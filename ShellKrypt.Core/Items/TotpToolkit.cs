using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace ShellKrypt.Core.Items;

public static class TotpToolkit
{
    private const int DefaultDigits = 6;
    private const int DefaultPeriodSeconds = 30;

    public sealed record TotpConfig(
        string Secret,
        int Digits,
        int PeriodSeconds,
        string Algorithm);

    public static bool TryParse(string? input, out TotpConfig? config, out string error)
    {
        config = null;
        error = "";

        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "TOTP secret is required.";
            return false;
        }

        if (value.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            return TryParseOtpauthUri(value, out config, out error);

        if (!TryNormalizeBase32Secret(value, out var secret, out error))
            return false;

        config = new TotpConfig(secret, DefaultDigits, DefaultPeriodSeconds, "SHA1");
        return true;
    }

    public static bool TryGenerateCode(string? input, DateTimeOffset utcNow, out string code, out int secondsRemaining, out string error)
    {
        code = "";
        secondsRemaining = 0;
        error = "";

        if (!TryParse(input, out var config, out error) || config is null)
            return false;

        try
        {
            code = GenerateCode(config, utcNow, out secondsRemaining);
            return true;
        }
        catch (Exception ex)
        {
            code = "";
            secondsRemaining = 0;
            error = ex.Message;
            return false;
        }
    }

    public static string GenerateCode(string? input, DateTimeOffset utcNow)
    {
        if (!TryParse(input, out var config, out var error) || config is null)
            throw new ArgumentException(error, nameof(input));

        return GenerateCode(config, utcNow, out _);
    }

    private static string GenerateCode(TotpConfig config, DateTimeOffset utcNow, out int secondsRemaining)
    {
        if (config.Digits < 6 || config.Digits > 10)
            throw new ArgumentOutOfRangeException(nameof(config.Digits), "Digits must be between 6 and 10.");

        if (config.PeriodSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(config.PeriodSeconds), "Period must be at least 1 second.");

        var secretBytes = DecodeBase32(config.Secret);
        var counter = utcNow.ToUnixTimeSeconds() / config.PeriodSeconds;

        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = CreateHmac(config.Algorithm, secretBytes);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;

        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var modulo = 1;
        for (var i = 0; i < config.Digits; i++)
            modulo *= 10;

        var otp = binary % modulo;
        secondsRemaining = config.PeriodSeconds - (int)(utcNow.ToUnixTimeSeconds() % config.PeriodSeconds);
        if (secondsRemaining == 0)
            secondsRemaining = config.PeriodSeconds;

        return otp.ToString(CultureInfo.InvariantCulture).PadLeft(config.Digits, '0');
    }

    private static bool TryParseOtpauthUri(string value, out TotpConfig? config, out string error)
    {
        config = null;
        error = "";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "totp", StringComparison.OrdinalIgnoreCase))
        {
            error = "TOTP input must be a raw base32 secret or an otpauth://totp URI.";
            return false;
        }

        var query = ParseQuery(uri.Query);

        if (!query.TryGetValue("secret", out var secretValue) || string.IsNullOrWhiteSpace(secretValue))
        {
            error = "TOTP URI is missing a secret value.";
            return false;
        }

        if (!TryNormalizeBase32Secret(secretValue, out var secret, out error))
            return false;

        var digits = ParseInt(query.TryGetValue("digits", out var digitsValue) ? digitsValue : null, DefaultDigits, 6, 10);
        var period = ParseInt(query.TryGetValue("period", out var periodValue) ? periodValue : null, DefaultPeriodSeconds, 1, 300);
        var algorithm = NormalizeAlgorithm(query.TryGetValue("algorithm", out var algorithmValue) ? algorithmValue : "SHA1");

        config = new TotpConfig(secret, digits, period, algorithm);
        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = query.StartsWith('?') ? query[1..] : query;

        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            var key = idx >= 0 ? pair[..idx] : pair;
            var value = idx >= 0 ? pair[(idx + 1)..] : "";

            key = Uri.UnescapeDataString(key).Trim();
            value = Uri.UnescapeDataString(value).Trim();

            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static int ParseInt(string? value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return fallback;

        return parsed >= min && parsed <= max ? parsed : fallback;
    }

    private static string NormalizeAlgorithm(string? algorithm)
    {
        var value = string.IsNullOrWhiteSpace(algorithm) ? "SHA1" : algorithm.Trim();
        return value.ToUpperInvariant() switch
        {
            "SHA1" => "SHA1",
            "SHA256" => "SHA256",
            "SHA512" => "SHA512",
            _ => "SHA1"
        };
    }

    private static bool TryNormalizeBase32Secret(string value, out string secret, out string error)
    {
        error = "";
        secret = "";

        var normalized = value
            .Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("=", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "TOTP secret cannot be empty.";
            return false;
        }

        foreach (var ch in normalized)
        {
            if (ch is < 'A' or > 'Z')
            {
                if (ch is < '2' or > '7')
                {
                    error = "TOTP secret must use base32 characters A-Z and 2-7.";
                    return false;
                }
            }
        }

        secret = normalized;
        return true;
    }

    private static byte[] DecodeBase32(string value)
    {
        var bytes = new List<byte>();
        var current = 0;
        var bits = 0;

        foreach (var ch in value)
        {
            var index = ch switch
            {
                >= 'A' and <= 'Z' => ch - 'A',
                >= '2' and <= '7' => ch - '2' + 26,
                _ => throw new FormatException("Invalid base32 character in TOTP secret.")
            };

            current = (current << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((current >> bits) & 0xFF));
            }
        }

        return bytes.ToArray();
    }

    private static HMAC CreateHmac(string algorithm, byte[] key)
        => algorithm switch
        {
            "SHA256" => new HMACSHA256(key),
            "SHA512" => new HMACSHA512(key),
            _ => new HMACSHA1(key)
        };
}
