namespace ShellKrypt.UI.Shared.Navigation;

public static class ShellKryptSectionKeys
{
    public const string Vault = "vault";
    public const string WebLogins = "web";
    public const string Cards = "cards";
    public const string ApiKeys = "api";
    public const string Authenticator = "auth";
    public const string Notes = "notes";
    public const string Generator = "generator";
    public const string Audit = "audit";
    public const string Emergency = "emergency";
    public const string Backup = "backup";
    public const string Settings = "settings";
    public const string Activity = "activity";
}

public sealed record ShellKryptSectionDescriptor(
    string Key,
    string Title,
    string ShortTitle,
    string Glyph,
    string Subtitle,
    bool SupportsAdd);

public static class ShellKryptSectionCatalog
{
    public static IReadOnlyList<ShellKryptSectionDescriptor> DesktopSections { get; } =
    [
        new(ShellKryptSectionKeys.Vault, "All Items", "All", "AI", "All encrypted records in the active workspace.", false),
        new(ShellKryptSectionKeys.WebLogins, "Web Logins", "Logins", "WB", "Credentials, account URLs, and saved login details.", true),
        new(ShellKryptSectionKeys.Cards, "Credit Cards", "Cards", "CC", "Sensitive payment details protected in the vault.", true),
        new(ShellKryptSectionKeys.ApiKeys, "API Keys", "API", "AP", "API tokens, client secrets, project IDs, and provider metadata.", true),
        new(ShellKryptSectionKeys.Authenticator, "Authenticator", "Auth", "AU", "Desktop authenticator codes from QR screenshots or pasted secret keys.", true),
        new(ShellKryptSectionKeys.Notes, "Markdown Notes", "Notes", "SN", "Encrypted markdown notes and vault reference material.", true),
        new(ShellKryptSectionKeys.Generator, "Generator", "Generator", "GE", "Generate and transform local secrets without leaving the vault.", false),
        new(ShellKryptSectionKeys.Audit, "Security Audit", "Audit", "SE", "Audit reuse, age, and password risk across the vault.", false),
        new(ShellKryptSectionKeys.Emergency, "Emergency Kit", "Kit", "EK", "Prepare recovery steps before a lockout or device loss.", false),
        new(ShellKryptSectionKeys.Backup, "Backup Center", "Backups", "BK", "Create, verify, restore, and export local vault data.", false),
        new(ShellKryptSectionKeys.Settings, "Settings", "Settings", "ST", "Manage vault security and desktop behavior.", false),
        new(ShellKryptSectionKeys.Activity, "Activity Logs", "Logs", "AC", "Review vault activity events and plaintext report exports.", false)
    ];

    public static IReadOnlyList<ShellKryptSectionDescriptor> MobileSections { get; } =
    [
        new(ShellKryptSectionKeys.Vault, "All Items", "All", "AI", "All encrypted records in this vault.", false),
        new(ShellKryptSectionKeys.WebLogins, "Web Logins", "Logins", "WB", "Credentials and account URLs.", true),
        new(ShellKryptSectionKeys.Cards, "Credit Cards", "Cards", "CC", "Payment cards and expiry details.", true),
        new(ShellKryptSectionKeys.ApiKeys, "API Keys", "API", "AP", "Tokens, client secrets, and provider fields.", true),
        new(ShellKryptSectionKeys.Authenticator, "Authenticator", "Auth", "AU", "TOTP and HOTP codes.", true),
        new(ShellKryptSectionKeys.Notes, "Markdown Notes", "Notes", "SN", "Encrypted markdown notes.", true),
        new(ShellKryptSectionKeys.Audit, "Security Audit", "Audit", "SE", "Password risk and remediation.", false),
        new(ShellKryptSectionKeys.Activity, "Activity Logs", "Logs", "AC", "Vault activity events.", false),
        new(ShellKryptSectionKeys.Settings, "Settings", "Settings", "ST", "Security and app behavior.", false)
    ];
}
