namespace ShellKrypt.Infrastructure.Vaulting;

internal static partial class VaultCsvImportParser
{
    private static string TrimSnippet(string text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var trimmed = text.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static string NormalizeDuplicatePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();

    private static string Last4(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return digits;

        return digits[^4..];
    }
}
