namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private static string BuildSearchText(params string?[] parts)
        => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);

    private static string FirstNonEmpty(params string?[] parts)
        => parts.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part))?.Trim() ?? string.Empty;

    private static string TrimSnippet(string? text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var value = text.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static string MaskCardNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return string.Empty;

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        return $"**** **** **** {digits[^4..]}";
    }

    private static string MaskApiKeyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Encrypted API field";

        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? "****" : $"**** **** {trimmed[^4..]}";
    }

    private static DateTimeOffset GetUpdatedSortValue(VaultItemSummary item)
        => DateTimeOffset.TryParse(item.UpdatedAtUtc, out var updated) ? updated : DateTimeOffset.MinValue;
}
