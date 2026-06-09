using ShellKrypt.Application.Localization;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void LanguageRegistry_DefinesUniqueLanguages()
    {
        Assert.All(LanguageRegistry.All, language =>
        {
            Assert.False(string.IsNullOrWhiteSpace(language.Id));
            Assert.False(string.IsNullOrWhiteSpace(language.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(language.NativeName));
        });

        Assert.Equal(
            LanguageRegistry.All.Count,
            LanguageRegistry.All.Select(language => language.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void LocalizationDictionaries_DefineSameKeys()
    {
        var service = new LocalizationService();
        var englishKeys = service.GetLanguageStrings("en").Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        foreach (var language in LanguageRegistry.All)
        {
            var languageKeys = service.GetLanguageStrings(language.Id).Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            Assert.Equal(englishKeys, languageKeys);
        }
    }

    [Fact]
    public void LocalizationService_NormalizesLanguageAndFormatsStrings()
    {
        var service = new LocalizationService();

        service.SetLanguage("tr");

        Assert.Equal("tr", service.CurrentLanguageId);
        Assert.Equal("Dil", service.Get("Settings.Language.Title"));
        Assert.Equal("Otomatik kilit açık - 5 Dakika", service.Get("Settings.SecurityStatus.AutoLockEnabled", "5 Dakika"));

        service.SetLanguage("missing");

        Assert.Equal("en", service.CurrentLanguageId);
        Assert.Equal("Missing.Key", service.Get("Missing.Key"));
    }
}
