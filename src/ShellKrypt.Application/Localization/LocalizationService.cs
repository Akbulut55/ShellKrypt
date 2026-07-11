using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ShellKrypt.Application.Localization;

public sealed class LocalizationService
{
    private const string ResourcePrefix = "ShellKrypt.Application.Localization";
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService()
    {
        foreach (var language in LanguageRegistry.All)
            _translations[language.Id] = LoadLanguage(language.Id);

        CurrentLanguageId = LanguageRegistry.Default.Id;
    }

    public event EventHandler? LanguageChanged;

    public string CurrentLanguageId { get; private set; }

    public void SetLanguage(string? languageId)
    {
        var normalized = LanguageRegistry.GetById(languageId).Id;
        if (string.Equals(CurrentLanguageId, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        CurrentLanguageId = normalized;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key, params object[] args)
    {
        var value = GetRaw(key);
        return args.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    public IReadOnlyDictionary<string, string> GetCurrentStrings()
    {
        var english = GetDictionary(LanguageRegistry.Default.Id);
        var current = GetDictionary(CurrentLanguageId);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in english)
            merged[pair.Key] = current.TryGetValue(pair.Key, out var value) ? value : pair.Value;

        return merged;
    }

    public IReadOnlyDictionary<string, string> GetLanguageStrings(string languageId)
        => GetDictionary(LanguageRegistry.GetById(languageId).Id);

    private string GetRaw(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        if (GetDictionary(CurrentLanguageId).TryGetValue(key, out var value))
            return value;

        return GetDictionary(LanguageRegistry.Default.Id).TryGetValue(key, out var fallback)
            ? fallback
            : key;
    }

    private IReadOnlyDictionary<string, string> GetDictionary(string languageId)
        => _translations.TryGetValue(languageId, out var dictionary)
            ? dictionary
            : _translations[LanguageRegistry.Default.Id];

    private static IReadOnlyDictionary<string, string> LoadLanguage(string languageId)
    {
        var assembly = typeof(LocalizationService).Assembly;
        var primaryResourceName = $"{ResourcePrefix}.{languageId}.json";
        var baseLanguageSuffix = $".{languageId}.json";
        var fragmentLanguageSuffix = $"-{languageId}.json";
        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(resourceName =>
                string.Equals(resourceName, primaryResourceName, StringComparison.Ordinal) ||
                resourceName.EndsWith(baseLanguageSuffix, StringComparison.Ordinal) ||
                resourceName.EndsWith(fragmentLanguageSuffix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(resourceName => string.Equals(resourceName, primaryResourceName, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(resourceName => resourceName, StringComparer.Ordinal)
            .ToArray();

        if (resourceNames.Length == 0)
            throw new InvalidOperationException($"Missing localization resource: {primaryResourceName}");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing localization resource: {resourceName}");

            var partialValues = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException($"Invalid localization resource: {resourceName}");

            foreach (var pair in partialValues)
                values[pair.Key] = pair.Value;
        }

        return values;
    }
}
