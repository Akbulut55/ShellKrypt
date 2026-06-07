using System.IO;
using System.Linq;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel
{
    public string VaultName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);
    public string VaultSubtitle => "Current encrypted workspace";
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
    public string VaultFooterLabel => "ACTIVE VAULT";
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed ? 96 : 236;
    public string SidebarToggleToolTip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Vault => "All encrypted records in the active workspace.",
        ShellKryptSectionKeys.WebLogins => "Credentials, account URLs, and saved login details.",
        ShellKryptSectionKeys.Notes => "Encrypted markdown notes and vault reference material.",
        ShellKryptSectionKeys.Cards => "Sensitive payment details protected in the vault.",
        ShellKryptSectionKeys.Audit => "Audit reuse, age, and password risk across the vault.",
        ShellKryptSectionKeys.Generator => "Generate and transform local secrets without leaving the vault.",
        ShellKryptSectionKeys.Authenticator => "Desktop authenticator codes from QR screenshots or pasted secret keys.",
        ShellKryptSectionKeys.ApiKeys => "API tokens, client secrets, project IDs, and provider metadata.",
        ShellKryptSectionKeys.Settings => "Manage vault security, import/export, and desktop behavior.",
        ShellKryptSectionKeys.Activity => "Review vault activity events and plaintext report exports.",
        _ => "Local encrypted vault workspace."
    };
    public bool IsSettingsSelected => SelectedNav?.Key == ShellKryptSectionKeys.Settings;
    public bool ShowAddItemAction => !IsSettingsSelected;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Settings => "Search settings...",
        ShellKryptSectionKeys.Vault => "Search all items...",
        ShellKryptSectionKeys.WebLogins => "Search web logins...",
        ShellKryptSectionKeys.Notes => "Search markdown notes...",
        ShellKryptSectionKeys.Cards => "Search credit cards...",
        ShellKryptSectionKeys.Audit => "Search security audit...",
        ShellKryptSectionKeys.Generator => "Search generator tools...",
        ShellKryptSectionKeys.Authenticator => "Search authenticator codes...",
        ShellKryptSectionKeys.ApiKeys => "Search API keys...",
        ShellKryptSectionKeys.Activity => "Search activity...",
        _ => "Search all items..."
    };
}
