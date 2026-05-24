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
        new(ShellKryptSectionKeys.Settings, "Settings", "Settings", "ST", "Security, backup, and app behavior.", false)
    ];
}
