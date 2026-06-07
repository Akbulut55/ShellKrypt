using System.Text.RegularExpressions;

namespace ShellKrypt.Application.Markdown;

public static partial class SimpleMarkdown
{
    public static string CollapseWhitespace(string? value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    public static string StripInline(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var value = text;
        value = MarkdownLinkRegex().Replace(value, "$1");
        value = StrongRegex().Replace(value, static match => GetFirstGroupValue(match, 1, 2));
        value = EmphasisRegex().Replace(value, static match => GetFirstGroupValue(match, 1, 2));
        value = InlineCodeRegex().Replace(value, "$1");
        value = value.Replace("\\*", "*", StringComparison.Ordinal)
                     .Replace("\\_", "_", StringComparison.Ordinal)
                     .Replace("\\`", "`", StringComparison.Ordinal);
        return CollapseWhitespace(value);
    }

    private static string GetFirstGroupValue(Match match, params int[] indexes)
    {
        foreach (var index in indexes)
        {
            if (match.Groups[index].Success)
                return match.Groups[index].Value;
        }

        return string.Empty;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"\*(.+?)\*|_(.+?)_")]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();
}
