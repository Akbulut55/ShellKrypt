namespace ShellKrypt.UI.Shared.Search;

public static class ItemSearchMatcher
{
    public static bool Matches(string? query, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            return true;

        return tokens.All(token => fields.Any(field =>
            !string.IsNullOrWhiteSpace(field) &&
            field.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }
}
