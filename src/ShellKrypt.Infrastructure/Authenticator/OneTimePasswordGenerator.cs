using System.Buffers.Binary;
using System.Security.Cryptography;
using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Infrastructure.Authenticator;

public sealed class OneTimePasswordGenerator : IOneTimePasswordGenerator
{
    public AuthenticatorCodeSnapshot GetCurrentCode(AuthenticatorEntry entry, DateTimeOffset? now = null)
    {
        try
        {
            var secretBytes = Base32Codec.Decode(AuthenticatorNormalization.Secret(entry.Secret));
            var digits = AuthenticatorNormalization.Digits(entry.Digits);
            var snapshotTime = now ?? DateTimeOffset.UtcNow;
            var counter = entry.KeyType == AuthenticatorKeyType.CounterBased
                ? (ulong)AuthenticatorNormalization.Counter(entry.Counter)
                : CalculateTimeCounter(snapshotTime, AuthenticatorNormalization.Period(entry.PeriodSeconds));

            var code = GenerateCode(secretBytes, counter, AuthenticatorNormalization.Algorithm(entry.Algorithm), digits);
            if (entry.KeyType == AuthenticatorKeyType.CounterBased)
                return new AuthenticatorCodeSnapshot(code, 0, 0, true);

            var period = AuthenticatorNormalization.Period(entry.PeriodSeconds);
            var remaining = period - (int)(snapshotTime.ToUnixTimeSeconds() % period);
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
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(counterBytes, counter);

        using var hmac = CreateHmac(algorithm, secretBytes);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        var modulus = digits == 8 ? 100000000 : 1000000;
        return (binary % modulus).ToString(new string('0', digits));
    }

    private static HMAC CreateHmac(string algorithm, byte[] key)
        => algorithm switch
        {
            "HMAC-SHA256" => new HMACSHA256(key),
            "HMAC-SHA512" => new HMACSHA512(key),
            _ => new HMACSHA1(key)
        };
}
