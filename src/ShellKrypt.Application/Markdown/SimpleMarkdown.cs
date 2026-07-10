namespace ShellKrypt.Application.Markdown;

public static partial class SimpleMarkdown
{
    public static IReadOnlyList<MarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraphLines = new List<string>();
        var listItems = new List<string>();
        var listOrdered = false;
        var codeLines = new List<string>();
        var inCodeBlock = false;

        foreach (var rawLine in Normalize(markdown).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (inCodeBlock)
            {
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    blocks.Add(new MarkdownBlock(MarkdownBlockKind.CodeBlock, string.Join('\n', codeLines)));
                    codeLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    codeLines.Add(line);
                }

                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraphLines);
                FlushList(blocks, listItems, listOrdered);
                inCodeBlock = true;
                codeLines.Clear();
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph(blocks, paragraphLines);
                FlushList(blocks, listItems, listOrdered);
                continue;
            }

            if (TryParseHeading(trimmed, out var headingKind, out var headingText))
            {
                FlushParagraph(blocks, paragraphLines);
                FlushList(blocks, listItems, listOrdered);
                blocks.Add(new MarkdownBlock(headingKind, StripInline(headingText)));
                continue;
            }

            if (TryParseListItem(trimmed, out var ordered, out var listText))
            {
                FlushParagraph(blocks, paragraphLines);
                if (listItems.Count > 0 && listOrdered != ordered)
                    FlushList(blocks, listItems, listOrdered);

                listOrdered = ordered;
                listItems.Add(StripInline(listText));
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraphLines);
                FlushList(blocks, listItems, listOrdered);
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Quote, StripInline(trimmed.TrimStart('>', ' '))));
                continue;
            }

            paragraphLines.Add(trimmed);
        }

        if (inCodeBlock && codeLines.Count > 0)
            blocks.Add(new MarkdownBlock(MarkdownBlockKind.CodeBlock, string.Join('\n', codeLines)));

        FlushParagraph(blocks, paragraphLines);
        FlushList(blocks, listItems, listOrdered);

        return blocks;
    }

    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var parts = Parse(markdown)
            .SelectMany(block => block.Items ?? [block.Text])
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return CollapseWhitespace(string.Join(' ', parts));
    }
}
