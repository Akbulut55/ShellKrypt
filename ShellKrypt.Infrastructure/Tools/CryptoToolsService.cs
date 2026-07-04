using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Core.Tools;

namespace ShellKrypt.Infrastructure.Tools;

public sealed class CryptoToolsService : ICryptoToolsService
{
    private static readonly char[] Lowercase = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
    private static readonly char[] Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Numbers = "0123456789".ToCharArray();
    private static readonly char[] Symbols = "!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();

    public string? GeneratePassword(PasswordGenerationOptions options)
    {
        var length = Math.Clamp(options.Length, 1, 100);
        var pools = new List<char[]>();
        if (options.IncludeLowercase) pools.Add(Lowercase);
        if (options.IncludeUppercase) pools.Add(Uppercase);
        if (options.IncludeNumbers) pools.Add(Numbers);
        if (options.IncludeSymbols) pools.Add(Symbols);

        if (pools.Count == 0)
            return null;

        var chars = new List<char>(length);

        if (length >= pools.Count)
        {
            foreach (var pool in pools)
                chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        var all = pools.SelectMany(pool => pool).ToArray();
        while (chars.Count < length)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    public PasswordStrengthAssessment AssessPasswordStrength(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new PasswordStrengthAssessment(0, 0, PasswordStrengthRating.None);

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigits = password.Any(char.IsDigit);
        var hasSymbols = password.Any(ch => !char.IsLetterOrDigit(ch));

        var poolSize = 0;
        var enabledClasses = 0;

        if (hasLower)
        {
            poolSize += Lowercase.Length;
            enabledClasses++;
        }

        if (hasUpper)
        {
            poolSize += Uppercase.Length;
            enabledClasses++;
        }

        if (hasDigits)
        {
            poolSize += Numbers.Length;
            enabledClasses++;
        }

        if (hasSymbols)
        {
            poolSize += Symbols.Length;
            enabledClasses++;
        }

        if (poolSize == 0)
            return new PasswordStrengthAssessment(0, 0, PasswordStrengthRating.None);

        var entropyBits = password.Length * Math.Log2(poolSize);
        var lengthScore = Math.Clamp(password.Length / 20.0 * 40.0, 0.0, 40.0);
        var diversityScore = enabledClasses / 4.0 * 25.0;
        var entropyScore = Math.Clamp(entropyBits / 110.0 * 35.0, 0.0, 35.0);
        var score = (int)Math.Clamp(Math.Round(lengthScore + diversityScore + entropyScore), 0, 100);

        var rating = score switch
        {
            >= 80 => PasswordStrengthRating.Secure,
            >= 60 => PasswordStrengthRating.Strong,
            >= 35 => PasswordStrengthRating.Fair,
            _ => PasswordStrengthRating.Weak
        };

        return new PasswordStrengthAssessment(entropyBits, score, rating);
    }

    public string ComputeSha256(string input)
        => ComputeHash(input, SHA256.HashData);

    public string ComputeSha512(string input)
        => ComputeHash(input, SHA512.HashData);

    public string EncodeBase64(string input)
    {
        if (input.Length == 0)
            return "";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }

    public string DecodeBase64(string input)
    {
        if (input.Trim().Length == 0)
            return "";

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(input.Trim()));
        }
        catch (FormatException)
        {
            return "";
        }
    }

    private static string ComputeHash(string input, Func<byte[], byte[]> hash)
    {
        if (input.Length == 0)
            return "";

        return Convert.ToHexString(hash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static void Shuffle(IList<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
