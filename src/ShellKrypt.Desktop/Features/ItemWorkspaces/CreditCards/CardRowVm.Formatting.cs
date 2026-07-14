using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;

public sealed partial class CardRowVm
{
    internal static string FormatCardNumber(string? number, int maxDigits = 19, bool includeTrailingSeparator = false)
    {
        var digits = DigitsOnly(number, maxDigits);
        if (digits.Length == 0)
            return "";

        var groups = new List<string>();
        for (var i = 0; i < digits.Length; i += 4)
        {
            var length = Math.Min(4, digits.Length - i);
            groups.Add(digits.Substring(i, length));
        }

        var formatted = string.Join(" ", groups);
        if (includeTrailingSeparator && digits.Length % 4 == 0 && digits.Length < maxDigits)
            return formatted + " ";

        return formatted;
    }

    internal static string DigitsOnly(string? value, int maxDigits)
        => new((value ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());

    internal static string DetectIssuer(string? number)
    {
        var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("4", StringComparison.Ordinal))
            return "Visa";
        if (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal))
            return "Amex";
        if (digits.Length >= 2 && int.TryParse(digits[..2], out var prefix2))
        {
            if (prefix2 is >= 51 and <= 55)
                return "Mastercard";
            if (prefix2 is 36 or 38 or 39)
                return "Diners Club";
            if (prefix2 == 62)
                return "UnionPay";
            if (prefix2 == 65)
                return "Discover";
        }
        if (digits.Length >= 3 && int.TryParse(digits[..3], out var prefix3))
        {
            if (prefix3 is >= 300 and <= 305)
                return "Diners Club";
            if (prefix3 is >= 644 and <= 649)
                return "Discover";
        }
        if (digits.StartsWith("35", StringComparison.Ordinal))
            return "JCB";
        if (digits.StartsWith("6011", StringComparison.Ordinal))
            return "Discover";
        if (digits.Length >= 4 && int.TryParse(digits[..4], out var prefix4) && prefix4 is >= 2221 and <= 2720)
            return "Mastercard";

        return "Card";
    }

    private static string MaskCardNumber(string? n)
    {
        if (string.IsNullOrWhiteSpace(n))
            return "";

        var digits = new string(n.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        var last4 = digits[^4..];
        return $"**** **** **** {last4}";
    }

    private static string FormatExpiryYear(string year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return "YY";

        var trimmed = year.Trim();
        return trimmed.Length >= 2 ? trimmed[^2..] : trimmed;
    }
}
