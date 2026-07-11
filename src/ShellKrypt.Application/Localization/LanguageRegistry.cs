namespace ShellKrypt.Application.Localization;

public static class LanguageRegistry
{
    private static readonly LanguageDefinition[] Languages =
    [
        new("en", "English", "English"),
        new("tr", "Turkish", "Türkçe")
    ];

    public static IReadOnlyList<LanguageDefinition> All => Languages;

    public static LanguageDefinition Default => Languages[0];

    public static LanguageDefinition GetById(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId))
            return Default;

        return Languages.FirstOrDefault(language =>
            string.Equals(language.Id, languageId.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
    }
}
