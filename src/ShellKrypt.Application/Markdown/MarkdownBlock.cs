namespace ShellKrypt.Application.Markdown;

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

public record MarkdownBlock(
    MarkdownBlockKind Kind,
    string Text,
    IReadOnlyList<string>? Items = null)
{
    public IReadOnlyList<string> DisplayItems =>
        Items is null
            ? Array.Empty<string>()
            : Kind == MarkdownBlockKind.OrderedList
                ? Items.Select((item, index) => $"{index + 1}. {item}").ToArray()
                : Items.Select(item => $"- {item}").ToArray();
}

public sealed record MarkdownHeading1Block : MarkdownBlock
{
    public MarkdownHeading1Block(string text) : base(MarkdownBlockKind.Heading1, text) { }
}

public sealed record MarkdownHeading2Block : MarkdownBlock
{
    public MarkdownHeading2Block(string text) : base(MarkdownBlockKind.Heading2, text) { }
}

public sealed record MarkdownHeading3Block : MarkdownBlock
{
    public MarkdownHeading3Block(string text) : base(MarkdownBlockKind.Heading3, text) { }
}

public sealed record MarkdownParagraphBlock : MarkdownBlock
{
    public MarkdownParagraphBlock(string text) : base(MarkdownBlockKind.Paragraph, text) { }
}

public sealed record MarkdownQuoteBlock : MarkdownBlock
{
    public MarkdownQuoteBlock(string text) : base(MarkdownBlockKind.Quote, text) { }
}

public sealed record MarkdownCodeBlock : MarkdownBlock
{
    public MarkdownCodeBlock(string text) : base(MarkdownBlockKind.CodeBlock, text) { }
}

public sealed record MarkdownListBlock : MarkdownBlock
{
    public MarkdownListBlock(bool ordered, IReadOnlyList<string> items)
        : base(ordered ? MarkdownBlockKind.OrderedList : MarkdownBlockKind.UnorderedList, string.Empty, items) { }
}
