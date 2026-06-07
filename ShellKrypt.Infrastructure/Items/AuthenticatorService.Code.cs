using System.Buffers.Binary;
using System.Security.Cryptography;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class AuthenticatorService
{
    public AuthenticatorCodeSnapshot GetCurrentCode(AuthenticatorEntry entry, DateTimeOffset? now = null)
    {
        try
        {
            var secretBytes = DecodeBase32(NormalizeSecret(entry.Secret));
            var digits = NormalizeDigits(entry.Digits);
            var counter = entry.KeyType == AuthenticatorKeyType.CounterBased
                ? (ulong)NormalizeCounter(entry.Counter)
                : CalculateTimeCounter(now ?? DateTimeOffset.UtcNow, NormalizePeriod(entry.PeriodSeconds));

            var code = GenerateCode(secretBytes, counter, NormalizeAlgorithm(entry.Algorithm), digits);
            if (entry.KeyType == AuthenticatorKeyType.CounterBased)
                return new AuthenticatorCodeSnapshot(code, 0, 0, true);

            var snapshotTime = now ?? DateTimeOffset.UtcNow;
            var period = NormalizePeriod(entry.PeriodSeconds);
            var unixSeconds = snapshotTime.ToUnixTimeSeconds();
            var remaining = period - (int)(unixSeconds % period);
            if (remaining <= 0)
                remaining = period;

            var progressPercent = ((period - remaining) / (double)period) * 100d;
            return new AuthenticatorCodeSnapshot(code, remaining, progressPercent, true);
        }
        catch
        {
            return new AuthenticatorCodeSnapshot("------", 0, 0, false);
        }
    }

    private static ulong CalculateTimeCounter(DateTimeOffset timestamp, int period)
        => (ulong)(timestamp.ToUnixTimeSeconds() / period);

    private static string GenerateCode(byte[] secretBytes, ulong counter, string algorithm, int digits)
    {
        var counterBytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(counterBytes, counter);

        using var hmac = CreateHmac(algorithm, secretBytes);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        var modulus = digits == 8 ? 100000000 : 1000000;
        var otp = binary % modulus;
        return otp.ToString(new string('0', digits));
    }

    private static HMAC CreateHmac(string algorithm, byte[] key)
        => NormalizeAlgorithm(algorithm) switch
        {
            "HMAC-SHA256" => new HMACSHA256(key),
            "HMAC-SHA512" => new HMACSHA512(key),
            _ => new HMACSHA1(key)
        };
}
