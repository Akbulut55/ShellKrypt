namespace ShellKrypt.Desktop.Shell.Navigation;

public interface IDesktopNavigation
{
    void GoWelcome();
    void GoCreateVault();
    void GoUnlock();
    void OnUnlocked(byte[] vaultKey);
    void Lock();
    void ReloadShell();
}
