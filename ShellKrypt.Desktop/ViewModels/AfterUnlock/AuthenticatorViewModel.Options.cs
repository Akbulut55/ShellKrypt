using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed class AuthenticatorKeyTypeOption
{
    public AuthenticatorKeyTypeOption(AuthenticatorKeyType keyType, string label)
    {
        KeyType = keyType;
        Label = label;
    }

    public AuthenticatorKeyType KeyType { get; }
    public string Label { get; }
}

public sealed class AuthenticatorAlgorithmOption
{
    public AuthenticatorAlgorithmOption(string value, string label, string shortLabel)
    {
        Value = value;
        Label = label;
        ShortLabel = shortLabel;
    }

    public string Value { get; }
    public string Label { get; }
    public string ShortLabel { get; }
}

public sealed class AuthenticatorDigitsOption
{
    public AuthenticatorDigitsOption(int digits, string label)
    {
        Digits = digits;
        Label = label;
    }

    public int Digits { get; }
    public string Label { get; }
}
