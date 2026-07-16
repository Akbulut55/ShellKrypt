namespace ShellKrypt.Desktop.Shell.Dialogs;

public interface IDesktopDialogService
{
    Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName);
    Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName);
    Task<string?> PickFolderAsync(string title);
    Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText);
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);
    Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText);
    Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null);
    Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath);
}
