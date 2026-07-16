using System.IO;
using System.Linq;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel
{
    public string VaultName => string.IsNullOrWhiteSpace(_session.VaultPath)
        ? T(_localization, "Shell.VaultFallback")
        : Path.GetFileNameWithoutExtension(_session.VaultPath);
    public string VaultSubtitle => T(_localization, "Shell.VaultSubtitle");
    public string VaultMonogram
    {
        get
        {
            var letters = VaultName
                .Where(char.IsLetterOrDigit)
                .Take(2)
                .ToArray();

            return letters.Length == 0
                ? "VA"
                : new string(letters).ToUpperInvariant();
        }
    }
    public string VaultFooterLabel => T(_localization, "Shell.ActiveVault");
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed ? 96 : 236;
    public string SidebarToggleToolTip => IsSidebarCollapsed ? T(_localization, "Shell.ExpandSidebar") : T(_localization, "Shell.CollapseSidebar");
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Vault => T(_localization, "Sidebar.vault.Subtitle"),
        ShellKryptSectionKeys.WebLogins => T(_localization, "Sidebar.web.Subtitle"),
        ShellKryptSectionKeys.Notes => T(_localization, "Sidebar.notes.Subtitle"),
        ShellKryptSectionKeys.Cards => T(_localization, "Sidebar.cards.Subtitle"),
        ShellKryptSectionKeys.Audit => T(_localization, "Sidebar.audit.Subtitle"),
        ShellKryptSectionKeys.Backup => T(_localization, "Sidebar.backup.Subtitle"),
        ShellKryptSectionKeys.CryptoTools => T(_localization, "Sidebar.crypto_tools.Subtitle"),
        ShellKryptSectionKeys.QuickFill => T(_localization, "Sidebar.quick_fill.Subtitle"),
        ShellKryptSectionKeys.Authenticator => T(_localization, "Sidebar.auth.Subtitle"),
        ShellKryptSectionKeys.ApiKeys => T(_localization, "Sidebar.api.Subtitle"),
        ShellKryptSectionKeys.ProjectSecrets => T(_localization, "Sidebar.project_secrets.Subtitle"),
        ShellKryptSectionKeys.Settings => T(_localization, "Sidebar.settings.Subtitle"),
        ShellKryptSectionKeys.Activity => T(_localization, "Sidebar.activity.Subtitle"),
        _ => T(_localization, "Shell.LocalWorkspace")
    };
    public bool IsSettingsSelected => SelectedNav?.Key == ShellKryptSectionKeys.Settings;
    public bool ShowAddItemAction => SelectedNav?.Key is
        ShellKryptSectionKeys.WebLogins or
        ShellKryptSectionKeys.Cards or
        ShellKryptSectionKeys.ApiKeys or
        ShellKryptSectionKeys.ProjectSecrets or
        ShellKryptSectionKeys.Authenticator or
        ShellKryptSectionKeys.Notes;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Settings => T(_localization, "Shell.Search.Settings"),
        ShellKryptSectionKeys.Backup => T(_localization, "Shell.Search.Backup"),
        ShellKryptSectionKeys.Vault => T(_localization, "Shell.Search.AllItems"),
        ShellKryptSectionKeys.WebLogins => T(_localization, "Shell.Search.WebLogins"),
        ShellKryptSectionKeys.Notes => T(_localization, "Shell.Search.Notes"),
        ShellKryptSectionKeys.Cards => T(_localization, "Shell.Search.Cards"),
        ShellKryptSectionKeys.Audit => T(_localization, "Shell.Search.Audit"),
        ShellKryptSectionKeys.CryptoTools => T(_localization, "Shell.Search.CryptoTools"),
        ShellKryptSectionKeys.QuickFill => T(_localization, "Shell.Search.QuickFill"),
        ShellKryptSectionKeys.Authenticator => T(_localization, "Shell.Search.Authenticator"),
        ShellKryptSectionKeys.ApiKeys => T(_localization, "Shell.Search.ApiKeys"),
        ShellKryptSectionKeys.ProjectSecrets => T(_localization, "Shell.Search.ProjectSecrets"),
        ShellKryptSectionKeys.Activity => T(_localization, "Shell.Search.Activity"),
        _ => T(_localization, "Shell.Search.AllItems")
    };
}
