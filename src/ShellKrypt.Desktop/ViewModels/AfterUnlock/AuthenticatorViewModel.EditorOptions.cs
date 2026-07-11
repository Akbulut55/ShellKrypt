using System;
using System.Globalization;
using System.Linq;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    private AuthenticatorAlgorithmOption ResolveAlgorithmOption(string? value)
    {
        var normalized = NormalizeAlgorithm(value);
        return AlgorithmOptions.First(option => string.Equals(option.Value, normalized, StringComparison.Ordinal));
    }

    private AuthenticatorDigitsOption ResolveDigitsOption(int digits)
    {
        var normalized = digits == 8 ? 8 : 6;
        return DigitsOptions.First(option => option.Digits == normalized);
    }

    private int ResolveFormPeriodSeconds()
    {
        if (SelectedFormKeyType?.KeyType == AuthenticatorKeyType.CounterBased)
            return 30;

        if (!int.TryParse(FormPeriodSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 1 ||
            seconds > 300)
        {
            throw new InvalidOperationException("Period must be a whole number between 1 and 300 seconds.");
        }

        return seconds;
    }

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return algorithm?.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "HMAC-SHA256" => "HMAC-SHA256",
            "SHA512" or "HMAC-SHA512" => "HMAC-SHA512",
            _ => "HMAC-SHA1"
        };
    }

    private static string NormalizePeriodText(string? value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : "30";
    }
}
