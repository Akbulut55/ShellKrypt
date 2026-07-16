namespace ShellKrypt.Desktop.Shell;

public partial class MainWindowViewModel
{
    public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
        => _dialogs.PickOpenFileAsync(title, extensions, fileTypeName);

    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
        => _dialogs.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);

    public Task<string?> PickFolderAsync(string title) => _dialogs.PickFolderAsync(title);

    public Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText)
        => _dialogs.ConfirmDangerousActionAsync(title, message, detail, confirmText);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false)
        => _dialogs.ConfirmAsync(title, message, confirmText, destructive);

    public Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText)
        => _dialogs.PromptPasswordAsync(title, message, detail, confirmText);

    public Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null)
        => _dialogs.ShowImportVaultDialogAsync(initialPath, initialDisplayName);

    public Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath)
        => _dialogs.ShowEditVaultDialogAsync(displayName, description, vaultPath);
}
