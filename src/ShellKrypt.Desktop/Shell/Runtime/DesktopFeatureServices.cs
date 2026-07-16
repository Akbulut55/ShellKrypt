using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed record DesktopFeatureServices(
    IVaultSessionController Session,
    LocalizationService Localization,
    IActivityRecorder Activity,
    ISecureClipboardService Clipboard,
    IDesktopDialogService Dialogs,
    IDesktopSettingsController Settings,
    IQuickFillController QuickFillController)
{
    public event EventHandler? ActivityChanged
    {
        add => Activity.Changed += value;
        remove => Activity.Changed -= value;
    }
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
    public bool IsUnlocked => Session.IsUnlocked;
    public bool AutoLockEnabled { get => Settings.AutoLockEnabled; set => Settings.AutoLockEnabled = value; }
    public int AutoLockMinutes { get => Settings.AutoLockMinutes; set => Settings.AutoLockMinutes = value; }
    public bool LockOnDeactivate { get => Settings.LockOnDeactivate; set => Settings.LockOnDeactivate = value; }
    public int LockOnDeactivateSeconds { get => Settings.LockOnDeactivateSeconds; set => Settings.LockOnDeactivateSeconds = value; }
    public int ClipboardClearSeconds { get => Settings.ClipboardClearSeconds; set => Settings.ClipboardClearSeconds = value; }
    public bool ClipboardCopyEnabled { get => Settings.ClipboardCopyEnabled; set => Settings.ClipboardCopyEnabled = value; }
    public bool CloseToTrayEnabled { get => Settings.CloseToTrayEnabled; set => Settings.CloseToTrayEnabled = value; }
    public int MarkdownAutoSaveSeconds { get => Settings.MarkdownAutoSaveSeconds; set => Settings.MarkdownAutoSaveSeconds = value; }
    public string ThemeId { get => Settings.ThemeId; set => Settings.ThemeId = value; }
    public string LanguageId { get => Settings.LanguageId; set => Settings.LanguageId = value; }
    public void SetVaultPath(string? path) => Session.SetVaultPath(path);
    public ShellKrypt.Application.Settings.QuickFillSettings QuickFill => QuickFillController.Settings;
    public string QuickFillHotkeyStatus => QuickFillController.HotkeyStatus;
    public bool CanConfigureQuickFillSystemShortcut => QuickFillController.CanConfigureSystemShortcut;

    public Task CopyToClipboardAsync(string value) => Clipboard.CopyAsync(value);
    public Task ClearClipboardAsync() => Clipboard.ClearAsync();
    public Task<Avalonia.Media.Imaging.Bitmap?> TryGetClipboardBitmapAsync() => Clipboard.TryGetBitmapAsync();
    public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName) => Dialogs.PickOpenFileAsync(title, extensions, fileTypeName);
    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName) => Dialogs.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);
    public Task<string?> PickFolderAsync(string title) => Dialogs.PickFolderAsync(title);
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) => Dialogs.ConfirmAsync(title, message, confirmText, destructive);
    public Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText) => Dialogs.ConfirmDangerousActionAsync(title, message, detail, confirmText);
    public void ConfigureQuickFillSystemShortcut() => QuickFillController.ConfigureSystemShortcut();
    public void AcceptQuickFillAutoTypeAcknowledgement() => QuickFillController.AcceptAutoTypeAcknowledgement();
    public void LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null)
        => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}
