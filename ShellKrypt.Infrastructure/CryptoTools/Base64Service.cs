using System.Text;
using ShellKrypt.Core.CryptoTools;

namespace ShellKrypt.Infrastructure.CryptoTools;

public sealed class Base64Service : IBase64Service
{
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
}
