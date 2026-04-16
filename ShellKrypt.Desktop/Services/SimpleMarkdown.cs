using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ShellKrypt.Desktop.Services;

public enum MarkdownBlockKind
{
    Heading1,
    Heading2,
    Heading3,
    Paragraph,
    UnorderedList,
    OrderedList,
    Quote,
    CodeBlock
}

public sealed record MarkdownBlock(
    MarkdownBlockKind Kind,
    string Text,
    IReadOnlyList<string>? Items = null)
{
    public bool IsHeading1 => Kind == MarkdownBlockKind.Heading1;
    public bool IsHeading2 => Kind == MarkdownBlockKind.Heading2;
    public bool IsHeading3 => Kind == MarkdownBlockKind.Heading3;
    public bool IsParagraph => Kind == MarkdownBlockKind.Paragraph;
    public bool IsQuote => Kind == MarkdownBlockKind.Quote;
    public bool IsCodeBlock => Kind == MarkdownBlockKind.CodeBlock;
    public bool IsList => Kind is MarkdownBlockKind.UnorderedList or MarkdownBlockKind.OrderedList;

    public IReadOnlyList<string> DisplayItems =>
        Items is null
            ? Array.Empty<string>()
            : Kind == MarkdownBlockKind.OrderedList
                ? Items.Select((item, index) => $"{index + 1}. {item}").ToArray()
                : Items.Select(item => $"- {item}").ToArray();
}

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

    public static string CollapseWhitespace(string? value)
    {
        return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
    }

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
            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, text));

        paragraphLines.Clear();
    }

    private static void FlushList(List<MarkdownBlock> blocks, List<string> items, bool ordered)
    {
        if (items.Count == 0)
            return;

        blocks.Add(new MarkdownBlock(
            ordered ? MarkdownBlockKind.OrderedList : MarkdownBlockKind.UnorderedList,
            string.Empty,
            items.ToArray()));

        items.Clear();
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

    [GeneratedRegex(@"^\d+\.\s+(.+)$")]
    private static partial Regex OrderedListRegex();
}
