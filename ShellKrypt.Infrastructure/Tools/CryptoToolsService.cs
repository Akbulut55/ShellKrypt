using ShellKrypt.Core.Tools;

namespace ShellKrypt.Infrastructure.Tools;

public sealed partial class CryptoToolsService : ICryptoToolsService
{
    private static readonly char[] Lowercase = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
    private static readonly char[] Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Numbers = "0123456789".ToCharArray();
    private static readonly char[] Symbols = "!@#$%^&*()-_=+[]{};:,.?/".ToCharArray();
}
