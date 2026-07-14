using System;
using System.IO;
using System.Threading.Tasks;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.ViewModels.App.QuickFill;
using ShellKrypt.Desktop.Views.App.QuickFill;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AttachQuickFillHotkey()
    {
        _globalHotkeyService.Start(_quickFill);
        OnPropertyChanged(nameof(QuickFillHotkeyStatus));
        OnPropertyChanged(nameof(CanConfigureQuickFillSystemShortcut));
        if (Current is ShellViewModel shell)
            shell.QuickFill.RefreshHotkeyStatus();
    }

    public void Shutdown()
    {
        _globalHotkeyService.Stop();
        Lock();
    }

    public void SaveQuickFillSettings()
    {
        _quickFill.Normalize();
        SaveSettingsAndSyncSessionSecurity();
        AttachQuickFillHotkey();
    }

    public void AcceptQuickFillAutoTypeAcknowledgement()
    {
        if (_quickFill.HasAutoTypeAcknowledgement)
            return;

        _quickFill.AutoTypeAcknowledgedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        SaveQuickFillSettings();
    }

    public IDisposable SuppressTransientFocusLoss() => _sessionSecurity.SuppressTransientFocusLoss();

    public QuickFillTargetContext CaptureQuickFillTarget() => _foregroundWindowService.Capture();

    public void ConfigureQuickFillSystemShortcut()
    {
        _globalHotkeyService.ConfigurePortalShortcut();
        OnPropertyChanged(nameof(QuickFillHotkeyStatus));
        OnPropertyChanged(nameof(CanConfigureQuickFillSystemShortcut));
        if (Current is ShellViewModel shell)
            shell.QuickFill.RefreshHotkeyStatus();
    }

    public void OpenQuickFillPopup()
    {
        var target = _foregroundWindowService.Capture();
        var suppression = SuppressTransientFocusLoss();
        var popup = new QuickFillPopupWindow();
        var vm = new QuickFillPopupViewModel(
            this,
            _vaultRegistryService,
            _vaultService,
            _quickFillEntryService,
            _webLoginService,
            _cardService,
            _apiKeyService,
            _authenticatorEntryService,
            _oneTimePasswordGenerator,
            _autoTypeService,
            target);

        vm.CloseRequested += (_, _) => popup.Close();
        popup.Closed += (_, _) => suppression.Dispose();
        popup.DataContext = vm;
        popup.Show();
        _ = vm.LoadAsync();
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
