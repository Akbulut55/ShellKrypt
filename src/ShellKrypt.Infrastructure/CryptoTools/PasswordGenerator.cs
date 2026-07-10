using System.Security.Cryptography;
using ShellKrypt.Core.CryptoTools;

namespace ShellKrypt.Infrastructure.CryptoTools;

public sealed class PasswordGenerator : IPasswordGenerator
{
    public string? GeneratePassword(PasswordGenerationOptions options)
    {
        var length = Math.Clamp(options.Length, 1, 100);
        var pools = new List<char[]>();
        if (options.IncludeLowercase) pools.Add(PasswordCharacterSets.Lowercase);
        if (options.IncludeUppercase) pools.Add(PasswordCharacterSets.Uppercase);
        if (options.IncludeNumbers) pools.Add(PasswordCharacterSets.Numbers);
        if (options.IncludeSymbols) pools.Add(PasswordCharacterSets.Symbols);

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

    private static void Shuffle(IList<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
