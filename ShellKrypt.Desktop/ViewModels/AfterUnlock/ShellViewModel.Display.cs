using System.IO;
using System.Linq;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel
{
    public string VaultName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? T(_root, "Shell.VaultFallback")
        : Path.GetFileNameWithoutExtension(_root.VaultPath);
    public string VaultSubtitle => T(_root, "Shell.VaultSubtitle");
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
    public string VaultFooterLabel => T(_root, "Shell.ActiveVault");
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed ? 96 : 236;
    public string SidebarToggleToolTip => IsSidebarCollapsed ? T(_root, "Shell.ExpandSidebar") : T(_root, "Shell.CollapseSidebar");
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Vault => T(_root, "Sidebar.vault.Subtitle"),
        ShellKryptSectionKeys.WebLogins => T(_root, "Sidebar.web.Subtitle"),
        ShellKryptSectionKeys.Notes => T(_root, "Sidebar.notes.Subtitle"),
        ShellKryptSectionKeys.Cards => T(_root, "Sidebar.cards.Subtitle"),
        ShellKryptSectionKeys.Audit => T(_root, "Sidebar.audit.Subtitle"),
        ShellKryptSectionKeys.Emergency => T(_root, "Sidebar.emergency.Subtitle"),
        ShellKryptSectionKeys.Backup => T(_root, "Sidebar.backup.Subtitle"),
        ShellKryptSectionKeys.Generator => T(_root, "Sidebar.generator.Subtitle"),
        ShellKryptSectionKeys.QuickFill => T(_root, "Sidebar.quick_fill.Subtitle"),
        ShellKryptSectionKeys.Authenticator => T(_root, "Sidebar.auth.Subtitle"),
        ShellKryptSectionKeys.ApiKeys => T(_root, "Sidebar.api.Subtitle"),
        ShellKryptSectionKeys.Settings => T(_root, "Sidebar.settings.Subtitle"),
        ShellKryptSectionKeys.Activity => T(_root, "Sidebar.activity.Subtitle"),
        _ => T(_root, "Shell.LocalWorkspace")
    };
    public bool IsSettingsSelected => SelectedNav?.Key == ShellKryptSectionKeys.Settings;
    public bool ShowAddItemAction => SelectedNav?.Key is
        ShellKryptSectionKeys.WebLogins or
        ShellKryptSectionKeys.Cards or
        ShellKryptSectionKeys.ApiKeys or
        ShellKryptSectionKeys.Authenticator or
        ShellKryptSectionKeys.Notes;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Settings => T(_root, "Shell.Search.Settings"),
        ShellKryptSectionKeys.Emergency => T(_root, "Shell.Search.Emergency"),
        ShellKryptSectionKeys.Backup => T(_root, "Shell.Search.Backup"),
        ShellKryptSectionKeys.Vault => T(_root, "Shell.Search.AllItems"),
        ShellKryptSectionKeys.WebLogins => T(_root, "Shell.Search.WebLogins"),
        ShellKryptSectionKeys.Notes => T(_root, "Shell.Search.Notes"),
        ShellKryptSectionKeys.Cards => T(_root, "Shell.Search.Cards"),
        ShellKryptSectionKeys.Audit => T(_root, "Shell.Search.Audit"),
        ShellKryptSectionKeys.Generator => T(_root, "Shell.Search.Generator"),
        ShellKryptSectionKeys.QuickFill => T(_root, "Shell.Search.QuickFill"),
        ShellKryptSectionKeys.Authenticator => T(_root, "Shell.Search.Authenticator"),
        ShellKryptSectionKeys.ApiKeys => T(_root, "Shell.Search.ApiKeys"),
        ShellKryptSectionKeys.Activity => T(_root, "Shell.Search.Activity"),
        _ => T(_root, "Shell.Search.AllItems")
    };
}
