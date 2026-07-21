using Avalonia.Media.Imaging;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Shell.Runtime;

public interface ILocalizedRuntime { LocalizationService Localization { get; } }

public sealed record ItemWorkspaceRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard) : ILocalizedRuntime;
public sealed record AuthenticatorRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard, IDesktopDialogService Dialogs) : ILocalizedRuntime;
public sealed record CryptoToolsRuntime(LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard) : ILocalizedRuntime;
public sealed record BackupCenterRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard, IDesktopDialogService Dialogs, IDesktopFileService Files) : ILocalizedRuntime;
public sealed record AllItemsRuntime(IVaultSessionController Session, LocalizationService Localization) : ILocalizedRuntime
{
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
}

public sealed record ProjectSecretsRuntime(IVaultSessionController Session, IActivityRecorder Activity, ISecureClipboardService Clipboard, IDesktopDialogService Dialogs)
{
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
    public Task CopyToClipboardAsync(string value) => Clipboard.CopyAsync(value);
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) => Dialogs.ConfirmAsync(title, message, confirmText, destructive);
    public Task<string?> PickFolderAsync(string title) => Dialogs.PickFolderAsync(title);
    public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName) => Dialogs.PickOpenFileAsync(title, extensions, fileTypeName);
    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName) => Dialogs.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);
    public void LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}

public sealed record MarkdownNotesRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard, int AutoSaveSeconds, IDesktopDialogService Dialogs) : ILocalizedRuntime
{
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
    public int MarkdownAutoSaveSeconds => AutoSaveSeconds;
    public Task CopyToClipboardAsync(string value) => Clipboard.CopyAsync(value);
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) => Dialogs.ConfirmAsync(title, message, confirmText, destructive);
    public Task<UnsavedChangesChoice> ResolveUnsavedChangesAsync(string title, string message, string saveText, string discardText) => Dialogs.ResolveUnsavedChangesAsync(title, message, saveText, discardText);
    public void LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}

public sealed record ActivityLogsRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, IDesktopDialogService Dialogs) : ILocalizedRuntime
{
    public event EventHandler<ActivityRecorderChangedEventArgs>? ActivityChanged { add => Activity.Changed += value; remove => Activity.Changed -= value; }
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
    public bool IsUnlocked => Session.IsUnlocked;
    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName) => Dialogs.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);
    public Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText) => Dialogs.ConfirmDangerousActionAsync(title, message, detail, confirmText);
    public ActivityLogOperationResult LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}

public sealed record SecurityAuditRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, IDesktopSettingsController Settings) : ILocalizedRuntime
{
    public string? VaultPath => Session.VaultPath;
    public byte[] VaultKey => Session.VaultKey;
    public bool AutoLockEnabled => Settings.AutoLockEnabled;
    public bool LockOnDeactivate => Settings.LockOnDeactivate;
    public int ClipboardClearSeconds => Settings.ClipboardClearSeconds;
    public bool ClipboardCopyEnabled => Settings.ClipboardCopyEnabled;
    public void LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}

public sealed record SettingsRuntime(IVaultSessionController Session, LocalizationService Localization, IActivityRecorder Activity, ISecureClipboardService Clipboard, IDesktopSettingsController Settings, IDesktopFileService Files) : ILocalizedRuntime
{
    public string? VaultPath => Session.VaultPath;
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
    public Task ClearClipboardAsync() => Clipboard.ClearAsync();
    public void LogActivity(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null) => Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}
