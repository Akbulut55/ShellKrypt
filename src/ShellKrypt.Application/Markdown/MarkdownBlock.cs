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
