using ShellKrypt.Application.Vaulting;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Shell.Navigation;

internal sealed class DesktopNavigationService(
    IVaultSessionController session,
    VaultRegistryService vaultRegistry,
    SessionSecurityService sessionSecurity,
    ISecureClipboardService clipboard,
    IActivityRecorder activity,
    IAutomaticBackupController automaticBackups,
    Func<IDesktopNavigation, WelcomeViewModel> createWelcome,
    Func<IDesktopNavigation, CreateVaultViewModel> createVault,
    Func<IDesktopNavigation, UnlockViewModel> createUnlock,
    Func<IDesktopNavigation, ShellViewModel> createShell) : IDesktopNavigation
{
    public event EventHandler? CurrentChanged;
    public ViewModelBase Current { get; private set; } = null!;

    public void Initialize() => GoWelcome();
    public void GoWelcome() => ReplaceCurrent(createWelcome(this));
    public void GoCreateVault() => ReplaceCurrent(createVault(this));
    public void GoUnlock() => ReplaceCurrent(createUnlock(this));

    public void OnUnlocked(byte[] vaultKey)
    {
        session.SetVaultKey(vaultKey);
        if (!string.IsNullOrWhiteSpace(session.VaultPath))
            vaultRegistry.MarkOpened(session.VaultPath);

        activity.Log("vault", "Vault unlocked", $"Opened {DisplayName(session.VaultPath)}.", "success", session.VaultPath, DisplayName(session.VaultPath));
        sessionSecurity.SetUnlocked(true);
        ReplaceCurrent(createShell(this));
        automaticBackups.Start();
    }

    public void Lock()
    {
        var path = session.VaultPath;
        if (!string.IsNullOrWhiteSpace(path))
            activity.Log("vault", "Vault locked", $"Locked {DisplayName(path)}.", "info", path, DisplayName(path));

        automaticBackups.Stop();
        automaticBackups.ClearSessionPassphrase();
        sessionSecurity.SetUnlocked(false);
        _ = clipboard.ClearAsync();
        session.ClearSensitive();
        GoWelcome();
    }

    public void ReloadShell()
    {
        if (!session.IsUnlocked)
            return;
        ReplaceCurrent(createShell(this));
        sessionSecurity.RecordActivity();
    }

    private void ReplaceCurrent(ViewModelBase next)
    {
        if (Current is ShellViewModel shell)
            shell.Deactivate();
        Current = next;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string DisplayName(string? path)
        => string.IsNullOrWhiteSpace(path) ? "Vault" : Path.GetFileNameWithoutExtension(path);
}
