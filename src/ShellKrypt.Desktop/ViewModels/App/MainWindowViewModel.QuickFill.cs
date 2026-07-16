using System;
using System.IO;
using System.Threading.Tasks;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AttachQuickFillHotkey()
    {
        _quickFillController.Start();
        OnPropertyChanged(nameof(QuickFillHotkeyStatus));
        OnPropertyChanged(nameof(CanConfigureQuickFillSystemShortcut));
        if (Current is ShellViewModel shell)
            shell.QuickFill.RefreshHotkeyStatus();
    }

    public void Shutdown()
    {
        _quickFillController.Stop();
        Lock();
    }

    public void SaveQuickFillSettings()
    {
        _quickFillController.SaveSettings();
    }

    public void AcceptQuickFillAutoTypeAcknowledgement()
    {
        _quickFillController.AcceptAutoTypeAcknowledgement();
    }

    public IDisposable SuppressTransientFocusLoss() => _sessionSecurity.SuppressTransientFocusLoss();

    public QuickFillTargetContext CaptureQuickFillTarget() => _quickFillController.CaptureTarget();

    public void ConfigureQuickFillSystemShortcut()
    {
        _quickFillController.ConfigureSystemShortcut();
    }

    public void OpenQuickFillPopup()
    {
        var target = _quickFillController.CaptureTarget();
        var suppression = SuppressTransientFocusLoss();
        _quickFillPopup.Open(this, target, suppression);
    }

    public void OpenQuickFillManager(QuickFillTargetContext? target = null)
    {
        if (Current is not ShellViewModel shell)
            return;

        shell.ShowQuickFill();
        if (target is not null)
            shell.QuickFill.PrepareEntryFromTarget(target);
    }

    public async Task<string?> UnlockFromQuickFillAsync(string vaultPath, string masterPassword)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
            return T(this, "QuickFill.Popup.Error.SelectVault");

        if (string.IsNullOrWhiteSpace(masterPassword))
            return T(this, "QuickFill.Popup.Error.EnterPassword");

        var targetPath = Path.GetFullPath(vaultPath);
        if (IsUnlocked && !string.Equals(VaultPath, targetPath, StringComparison.OrdinalIgnoreCase))
            Lock();

        SetVaultPath(targetPath);
        var result = await _vaultService.UnlockAsync(targetPath, masterPassword);
        if (!result.Success || result.VaultKey is null)
            return result.Error ?? T(this, "QuickFill.Popup.Error.UnlockFailed");

        OnUnlocked(result.VaultKey);
        return null;
    }
}
