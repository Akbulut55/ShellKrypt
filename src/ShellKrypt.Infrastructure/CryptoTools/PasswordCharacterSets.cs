namespace ShellKrypt.Infrastructure.CryptoTools;

internal static class PasswordCharacterSets
{
    internal static readonly char[] Lowercase = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
    internal static readonly char[] Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    internal static readonly char[] Numbers = "0123456789".ToCharArray();
    internal static readonly char[] Symbols = "!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();
}
