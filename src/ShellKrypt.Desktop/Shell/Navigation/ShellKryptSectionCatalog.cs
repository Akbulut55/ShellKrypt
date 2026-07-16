namespace ShellKrypt.Desktop.Shell.Navigation;

public static class ShellKryptSectionKeys
{
    public const string Vault = "vault";
    public const string WebLogins = "web";
    public const string Cards = "cards";
    public const string ApiKeys = "api";
    public const string ProjectSecrets = "project_secrets";
    public const string Authenticator = "auth";
    public const string Notes = "notes";
    public const string CryptoTools = "crypto_tools";
    public const string Audit = "audit";
    public const string Backup = "backup";
    public const string Settings = "settings";
    public const string Activity = "activity";
}

public static class ShellKryptSectionGroups
{
    public const string Vault = "vault";
    public const string Items = "items";
    public const string Tools = "tools";
    public const string Security = "security";
    public const string Data = "data";
    public const string App = "app";
}

public sealed record ShellKryptSectionDescriptor(
    string Key,
    string Group,
    string Title,
    string ShortTitle,
    string Glyph,
    string Subtitle,
    bool SupportsAdd);

public static class ShellKryptSectionCatalog
{
    public static IReadOnlyList<ShellKryptSectionDescriptor> DesktopSections { get; } =
    [
        new(ShellKryptSectionKeys.Vault, ShellKryptSectionGroups.Vault, "All Items", "All", "AI", "All encrypted records in the active workspace.", false),
        new(ShellKryptSectionKeys.WebLogins, ShellKryptSectionGroups.Items, "Web Logins", "Logins", "WB", "Credentials, account URLs, and saved login details.", true),
        new(ShellKryptSectionKeys.Cards, ShellKryptSectionGroups.Items, "Credit Cards", "Cards", "CC", "Sensitive payment details protected in the vault.", true),
        new(ShellKryptSectionKeys.ApiKeys, ShellKryptSectionGroups.Items, "API Keys", "API", "AP", "API tokens, client secrets, project IDs, and provider metadata.", true),
        new(ShellKryptSectionKeys.ProjectSecrets, ShellKryptSectionGroups.Items, "Project Secrets", "Projects", "PS", ".env variables and local project secret workflows.", true),
        new(ShellKryptSectionKeys.Authenticator, ShellKryptSectionGroups.Items, "Authenticator", "Auth", "AU", "Desktop authenticator codes from QR screenshots or pasted secret keys.", true),
        new(ShellKryptSectionKeys.Notes, ShellKryptSectionGroups.Items, "Markdown Notes", "Notes", "SN", "Encrypted markdown notes and vault reference material.", true),
        new(ShellKryptSectionKeys.CryptoTools, ShellKryptSectionGroups.Tools, "Crypto Tools", "Crypto", "CT", "Generate passwords, hash text, and transform Base64 locally.", false),
        new(ShellKryptSectionKeys.Audit, ShellKryptSectionGroups.Security, "Security Audit", "Audit", "SE", "Audit reuse, age, and password risk across the vault.", false),
        new(ShellKryptSectionKeys.Backup, ShellKryptSectionGroups.Data, "Backup Center", "Backups", "BK", "Create, verify, restore, and export local vault data.", false),
        new(ShellKryptSectionKeys.Activity, ShellKryptSectionGroups.Data, "Activity Logs", "Logs", "AC", "Review vault activity events and plaintext report exports.", false),
        new(ShellKryptSectionKeys.Settings, ShellKryptSectionGroups.App, "Settings", "Settings", "ST", "Manage vault security and desktop behavior.", false)
    ];
}
