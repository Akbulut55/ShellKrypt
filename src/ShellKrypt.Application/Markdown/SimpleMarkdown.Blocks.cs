using System.Text.RegularExpressions;

namespace ShellKrypt.Application.Markdown;

public static partial class SimpleMarkdown
{
    private static string Normalize(string? markdown)
        => (markdown ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal);

    private static bool TryParseHeading(string line, out MarkdownBlockKind kind, out string text)
    {
        kind = MarkdownBlockKind.Paragraph;
        text = string.Empty;

        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            kind = MarkdownBlockKind.Heading3;
            text = line[4..];
            return true;
        }

        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            kind = MarkdownBlockKind.Heading2;
            text = line[3..];
            return true;
        }

        if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            kind = MarkdownBlockKind.Heading1;
            text = line[2..];
            return true;
        }

        return false;
    }

    private static bool TryParseListItem(string line, out bool ordered, out string text)
    {
        ordered = false;
        text = string.Empty;

        if (line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("* ", StringComparison.Ordinal) ||
            line.StartsWith("+ ", StringComparison.Ordinal))
        {
            text = line[2..];
            return true;
        }

        var match = OrderedListRegex().Match(line);
        if (match.Success)
        {
            ordered = true;
            text = match.Groups[1].Value;
            return true;
        }

        return false;
    }

    private static void FlushParagraph(List<MarkdownBlock> blocks, List<string> paragraphLines)
    {
        if (paragraphLines.Count == 0)
            return;

        var text = StripInline(string.Join(' ', paragraphLines));
        if (!string.IsNullOrWhiteSpace(text))
            blocks.Add(new MarkdownParagraphBlock(text));

        paragraphLines.Clear();
    }

    private static void FlushList(List<MarkdownBlock> blocks, List<string> items, bool ordered)
    {
        if (items.Count == 0)
            return;

        blocks.Add(new MarkdownListBlock(ordered, items.ToArray()));

        items.Clear();
    }

    private static MarkdownBlock CreateTextBlock(MarkdownBlockKind kind, string text) => kind switch
    {
        MarkdownBlockKind.Heading1 => new MarkdownHeading1Block(text),
        MarkdownBlockKind.Heading2 => new MarkdownHeading2Block(text),
        MarkdownBlockKind.Heading3 => new MarkdownHeading3Block(text),
        _ => new MarkdownParagraphBlock(text)
    };

    [GeneratedRegex(@"^\d+\.\s+(.+)$")]
    private static partial Regex OrderedListRegex();
}
