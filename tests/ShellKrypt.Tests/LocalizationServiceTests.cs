using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Features.VaultAccess;
using ShellKrypt.Desktop.Bootstrap;
using ShellKrypt.Infrastructure.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
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

    [Fact]
    public void DesktopLocalizationReferences_ExistInDictionaries()
    {
        var service = new LocalizationService();
        var englishKeys = service.GetLanguageStrings("en").Keys.ToHashSet(StringComparer.Ordinal);
        var referencedKeys = EnumerateDesktopLocalizationReferences().ToArray();

        var missing = referencedKeys
            .Where(key => !englishKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void DesktopViewModelLocalizationKeyLiterals_ExistInDictionaries()
    {
        var service = new LocalizationService();
        var englishKeys = service.GetLanguageStrings("en").Keys.ToHashSet(StringComparer.Ordinal);
        var referencedKeys = EnumerateDesktopViewModelLocalizationKeyLiterals().ToArray();

        var missing = referencedKeys
            .Where(key => !englishKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void TurkishTranslations_CoverRepresentativeDesktopScreens()
    {
        var service = new LocalizationService();

        service.SetLanguage("tr");

        Assert.Equal("Kasa Oluştur", service.Get("Welcome.Button.CreateVault"));
        Assert.Equal("Kasayı Aç", service.Get("Unlock.Button.Unlock"));
        Assert.Equal("Web Girişleri", service.Get("WebLogins.Title"));
        Assert.Equal("Kredi Kartları", service.Get("Cards.Title"));
        Assert.Equal("API Anahtarları", service.Get("ApiKeys.Title"));
        Assert.Equal("Doğrulayıcı", service.Get("Authenticator.Title"));
        Assert.Equal("Markdown Notları", service.Get("Notes.Title"));
        Assert.Equal("Kripto Araçları", service.Get("CryptoTools.Password.Title"));
        Assert.Equal("Güvenlik Denetimi", service.Get("SecurityAudit.Title"));
        Assert.Equal("Backup Center", service.Get("BackupCenter.Title"));
        Assert.NotEqual("BackupCenter.Health.Title", service.Get("BackupCenter.Health.Title"));
        Assert.Equal("Etkinlik Kayıtları", service.Get("Activity.Title"));
        Assert.Equal("1 kullanılabilir", service.Get("Welcome.Stats.AvailableVaultOne", 1));
        Assert.Equal("Üretim", service.Get("ApiKeys.Environment.Default"));
    }

    [Fact]
    public void ViewModelComputedStrings_UpdateAfterLanguageChange()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var root = DesktopBootstrap.CreateMainWindowViewModel();
        var welcome = Assert.IsType<WelcomeViewModel>(root.Current);

        Assert.Equal("No vaults yet", welcome.EmptyStateTitle);

        root.Localization.SetLanguage("tr");

        Assert.Equal("Henüz kasa yok", welcome.EmptyStateTitle);
    }

    private static IEnumerable<string> EnumerateDesktopLocalizationReferences()
    {
        var desktopRoot = Path.Combine(FindRepositoryRoot(), "src", "ShellKrypt.Desktop");
        var regex = new Regex(
            @"Loc\.([A-Za-z0-9_.]+)|T\(_root,\s*""([^""]+)""|T\(""([^""]+)""",
            RegexOptions.Compiled);

        foreach (var filePath in Directory.EnumerateFiles(desktopRoot, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".axaml.cs", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(filePath);
            foreach (Match match in regex.Matches(text))
            {
                var key = match.Groups[1].Success
                    ? match.Groups[1].Value
                    : match.Groups[2].Success
                        ? match.Groups[2].Value
                        : match.Groups[3].Value;

                if (!string.IsNullOrWhiteSpace(key))
                    yield return key;
            }
        }
    }

    private static IEnumerable<string> EnumerateDesktopViewModelLocalizationKeyLiterals()
    {
        var desktopRoot = Path.Combine(FindRepositoryRoot(), "src", "ShellKrypt.Desktop");
        var regex = new Regex(
            "\"([A-Z][A-Za-z0-9]*(?:\\.[A-Za-z0-9]+){1,})\"",
            RegexOptions.Compiled);

        foreach (var filePath in Directory.EnumerateFiles(desktopRoot, "*ViewModel*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(filePath);
            foreach (Match match in regex.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(key))
                    yield return key;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "ShellKrypt.slnx")))
                return directory;

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class AppRootScope : IDisposable
    {
        private readonly string? _previousRoot;

        public AppRootScope(string appRoot)
        {
            _previousRoot = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, appRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, _previousRoot);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.Localization.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
